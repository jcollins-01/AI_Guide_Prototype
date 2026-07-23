using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class InteractionLogEntry
{
    // For tracking participant sessions
    public string participantID;
    public string sessionID;
    public string sceneName;

    public int queryNumber;
    public string timestampQuerySent;
    public string timestampGuideReplied;
    public string userQuery;
    public string guideResponse;

    // Time metrics (in seconds)
    public float timeSinceLastQuery;
    public float guideReplyLatency;
    public float guideReplyDuration;

    // VR Context Metrics
    public string guideRole;
    public string activeGuideType;
    public Vector3 playerHeadsetPosition;
    public Vector3 playerHeadsetRotation;
    public Vector3 playerHandPosition;
    public Vector3 guidePosition;
    public float distanceToGuide;
    public string triggeredToolCall;
}

public class InteractionLogger : MonoBehaviour
{
    public static InteractionLogger Instance { get; private set; }

    [Header("File Settings")]
    [Tooltip("Enter the Participant ID before pressing Play (e.g., P1, P2, O1, O2)")]
    public string participantID = "Test";
    [Tooltip("Enter the Session ID before pressing Play (e.g., 1, 2)")]
    public string sessionID = "1";
    [SerializeField] private string logFolderName = "Logs";

    // Hold session logs internally until application quits
    private List<InteractionLogEntry> sessionLogs = new List<InteractionLogEntry>();

    // Tracking state variables
    private int currentQueryCount = 0;

    // Timestamp checkpoints 
    private float timeUserSentQuery = -1f;
    private float timeUserFinishedSpeaking = -1f;
    private float timeGuideStartedSpeaking = -1f;
    private float timeGuideFinishedSpeaking = -1f;
    private float previousGuideFinishedTime = -1f;

    // Active log data under construction
    private InteractionLogEntry currentEntry;
    private bool isTrackingQuery = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Call when the participant sends a new query (button pressed or voice command initiated).
    /// </summary>
    public void OnUserQueryInitiated()
    {
        currentQueryCount++;
        timeUserSentQuery = Time.realtimeSinceStartup;

        float timeSinceLast = (previousGuideFinishedTime > 0f)
            ? (timeUserSentQuery - previousGuideFinishedTime)
            : 0f;

        currentEntry = new InteractionLogEntry
        {
            participantID = string.IsNullOrEmpty(participantID) ? "UNKNOWN_PARTICIPANT" : participantID,
            sessionID = string.IsNullOrEmpty(sessionID) ? "UNKNOWN_SESSION" : sessionID,
            sceneName = SceneManager.GetActiveScene().name,
            queryNumber = currentQueryCount,
            timestampQuerySent = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            timeSinceLastQuery = timeSinceLast
        };

        isTrackingQuery = true;
    }

    /// <summary>
    /// Call when the user stops speaking (VAD ends) and participant's speech is done being transcribed, then post-hoc update with the transcription
    /// </summary>
    public void OnUserFinishedSpeaking(string userTranscript = "")
    {
        if (!isTrackingQuery) return;

        // Only capture the end-of-speech time once (when the user releases the mic)
        if (timeUserFinishedSpeaking < 0f)
        {
            timeUserFinishedSpeaking = Time.realtimeSinceStartup;
        }

        // If we received actual text (from the server event), update the entry
        if (currentEntry != null && !string.IsNullOrEmpty(userTranscript))
        {
            currentEntry.userQuery = userTranscript;
        }
    }

    /// <summary>
    /// Call as soon as the guide starts producing/playing audio or first transcript chunk.
    /// </summary>
    public void OnGuideStartedSpeaking()
    {
        if (!isTrackingQuery || timeGuideStartedSpeaking > 0f) return;

        timeGuideStartedSpeaking = Time.realtimeSinceStartup;

        if (currentEntry != null)
        {
            currentEntry.timestampGuideReplied = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            float endOfSpeech = (timeUserFinishedSpeaking > 0f) ? timeUserFinishedSpeaking : timeUserSentQuery;
            currentEntry.guideReplyLatency = timeGuideStartedSpeaking - endOfSpeech;
        }
    }

    /// <summary>
    /// Call when the guide completes speech playback and finishes response.
    /// </summary>
    public void OnGuideFinishedSpeaking(string fullResponseText, string triggeredTool = "")
    {
        if (!isTrackingQuery) return;

        timeGuideFinishedSpeaking = Time.realtimeSinceStartup;
        previousGuideFinishedTime = timeGuideFinishedSpeaking;

        if (currentEntry != null)
        {
            currentEntry.guideResponse = fullResponseText;
            currentEntry.triggeredToolCall = triggeredTool;

            currentEntry.guideReplyDuration = (timeGuideStartedSpeaking > 0f)
                ? (timeGuideFinishedSpeaking - timeGuideStartedSpeaking)
                : 0f;

            // Save VR Context Metrics
            CaptureVRContext(currentEntry);

            // Add to the list to be exported at the end of the session
            sessionLogs.Add(currentEntry);
            Debug.Log($"[InteractionLogger] Successfully recorded query #{currentEntry.queryNumber}");
        }

        ResetQueryTimers();
    }

    private void CaptureVRContext(InteractionLogEntry entry)
    {
        // Update active scene in case of dynamic scene changes during the session
        entry.sceneName = SceneManager.GetActiveScene().name;

        AIGuide guide = FindObjectOfType<AIGuide>();
        SwitchTools tools = FindObjectOfType<SwitchTools>();
        SpatialPerceptionSensor sensor = FindObjectOfType<SpatialPerceptionSensor>();

        // Prioritize the SpatialPerceptionSensor for reliable positions
        if (sensor != null)
        {
            if (sensor.playerHeadset != null)
            {
                entry.playerHeadsetPosition = sensor.playerHeadset.position;
                entry.playerHeadsetRotation = sensor.playerHeadset.eulerAngles;
            }
            if (sensor.playerHandRight != null)
            {
                entry.playerHandPosition = sensor.playerHandRight.position;
            }
        }
        else if (guide != null && guide.headsetTransform != null) // Fallback to guide
        {
            entry.playerHeadsetPosition = guide.headsetTransform.position;
            entry.playerHeadsetRotation = guide.headsetTransform.eulerAngles;
        }

        if (guide != null)
        {
            entry.guidePosition = guide.transform.position;
            // Ensure distance is accurately measured from headset to guide
            entry.distanceToGuide = Vector3.Distance(entry.playerHeadsetPosition, guide.transform.position);
            entry.guideRole = guide.role.ToString();
        }

        if (tools != null)
        {
            entry.activeGuideType = tools.activeGuideType.ToString();
        }
    }

    private void ResetQueryTimers()
    {
        timeUserSentQuery = -1f;
        timeUserFinishedSpeaking = -1f;
        timeGuideStartedSpeaking = -1f;
        timeGuideFinishedSpeaking = -1f;
        isTrackingQuery = false;
    }

    /// <summary>
    /// Executes when the game quits, writing the session's logs to a CSV.
    /// </summary>
    private void OnApplicationQuit()
    {
        ExportToCSV();
    }

    private void ExportToCSV()
    {
        if (sessionLogs.Count == 0)
        {
            Debug.Log("[InteractionLogger] No queries logged this session. Skipping CSV export.");
            return;
        }

        string dirPath = Path.Combine(Application.persistentDataPath, logFolderName);
        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }

        string cleanParticipant = string.IsNullOrEmpty(participantID) ? "P_Unknown" : participantID;
        string cleanSession = string.IsNullOrEmpty(sessionID) ? "S_Unknown" : sessionID;

        string fileName = $"{cleanParticipant}_{cleanSession}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
        string filePath = Path.Combine(dirPath, fileName);

        StringBuilder csv = new StringBuilder();

        // Write the Header Row
        csv.AppendLine("ParticipantID,SessionID,SceneName,QueryNumber,TimestampSent,TimestampReplied,TimeSinceLastQuery(s),GuideReplyLatency(s),GuideReplyDuration(s),GuideRole,ActiveGuideType,TriggeredToolCall,PlayerHeadsetPos,PlayerHeadsetRot,PlayerHandPos,GuidePos,DistanceToGuide(m),UserQuery,GuideResponse");
        
        // Write each Entry
        foreach (var entry in sessionLogs)
        {
            // Format vectors cleanly
            string headPos = $"({entry.playerHeadsetPosition.x:F2} {entry.playerHeadsetPosition.y:F2} {entry.playerHeadsetPosition.z:F2})";
            string headRot = $"({entry.playerHeadsetRotation.x:F2} {entry.playerHeadsetRotation.y:F2} {entry.playerHeadsetRotation.z:F2})";
            string handPos = $"({entry.playerHandPosition.x:F2} {entry.playerHandPosition.y:F2} {entry.playerHandPosition.z:F2})";
            string guidePos = $"({entry.guidePosition.x:F2} {entry.guidePosition.y:F2} {entry.guidePosition.z:F2})";

            csv.Append($"{EscapeForCSV(entry.participantID)},");
            csv.Append($"{EscapeForCSV(entry.sessionID)},");
            csv.Append($"{EscapeForCSV(entry.sceneName)},");
            csv.Append($"{entry.queryNumber},");
            csv.Append($"{EscapeForCSV(entry.timestampQuerySent)},");
            csv.Append($"{EscapeForCSV(entry.timestampGuideReplied)},");
            csv.Append($"{entry.timeSinceLastQuery:F3},");
            csv.Append($"{entry.guideReplyLatency:F3},");
            csv.Append($"{entry.guideReplyDuration:F3},");
            csv.Append($"{EscapeForCSV(entry.guideRole)},");
            csv.Append($"{EscapeForCSV(entry.activeGuideType)},");
            csv.Append($"{EscapeForCSV(entry.triggeredToolCall)},");
            csv.Append($"{EscapeForCSV(headPos)},");
            csv.Append($"{EscapeForCSV(headRot)},");
            csv.Append($"{EscapeForCSV(handPos)},");
            csv.Append($"{EscapeForCSV(guidePos)},");
            csv.Append($"{entry.distanceToGuide:F3},");
            csv.Append($"{EscapeForCSV(entry.userQuery)},");
            csv.AppendLine($"{EscapeForCSV(entry.guideResponse)}"); // AppendLine for the final column
        }

        try
        {
            File.WriteAllText(filePath, csv.ToString());
            Debug.Log($"[InteractionLogger] Session ended. Successfully exported CSV to: {filePath}");

#if UNITY_EDITOR
            // Refresh Unity Asset Database so the CSV instantly appears in the Project folder window
            AssetDatabase.Refresh();
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"[InteractionLogger] Failed to export CSV: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper function to safely wrap strings in quotes and handle internal quotes or commas for CSV format.
    /// </summary>
    private string EscapeForCSV(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // If the string has quotes, commas, or newlines, we need to wrap it in quotes and double the internal quotes.
        if (text.Contains("\"") || text.Contains(",") || text.Contains("\n") || text.Contains("\r"))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }

        return text;
    }
}
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class MemoryManager : MonoBehaviour
{
    // Mark classes as Serializable so Unity's JSON utility can parse them
    [System.Serializable]
    public class ChatMessage
    {
        public string role; // "user", "assistant", or "system"
        public string content;
    }

    [System.Serializable]
    public class GuideSessionMemory
    {
        public string sessionDate;
        // The actual conversation history
        public List<ChatMessage> conversationHistory = new List<ChatMessage>();
        // A list of permanent semantic anchors the guide discovered last time
        public List<string> discoveredEnvironmentFeatures = new List<string>();
    }

    // Vars for handling saving the memory of sessions
    private string saveFilePath;
    public GuideSessionMemory currentSession = new GuideSessionMemory();
    public bool statefulSession = false;

    void Awake()
    {
        saveFilePath = Path.Combine(Application.persistentDataPath, "guide_memory.json");

        // Only load the session if we are in stateful mode
        if (statefulSession)
            LoadSession();
        else
            DeleteSession(); // otherwise, a stateless session will delete the last conversation history
    }

    public void LogConversationTurn(string role, string content)
    {
        currentSession.conversationHistory.Add(new ChatMessage { role = role, content = content });
    }

    void OnApplicationQuit()
    {
        SaveSession();
    }

    public void SaveSession()
    {
        currentSession.sessionDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        // Convert the object to a JSON string
        string json = JsonUtility.ToJson(currentSession, true); // true = pretty print

        // Write it to disk
        File.WriteAllText(saveFilePath, json);
        Debug.Log($"[Memory Manager] Saved guide session to {saveFilePath}");
    }

    public void LoadSession()
    {
        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            currentSession = JsonUtility.FromJson<GuideSessionMemory>(json);
            Debug.Log($"[Memory Manager] Welcome back! Loaded {currentSession.conversationHistory.Count} past messages.");
        }
        else
        {
            Debug.Log("[Memory Manager] No previous memory found. Starting a fresh session.");
            currentSession = new GuideSessionMemory();
        }
    }

    public void DeleteSession()
    {
        if (File.Exists(saveFilePath))
        {
            File.Delete(saveFilePath);
            Debug.Log("[Memory Manager] Stateless session active. Deleting past conversation history.");
        }
    }

    public string GetFormattedSessionHistory()
    {
        if (currentSession == null || currentSession.conversationHistory.Count == 0)
        {
            return null; // No memory to load
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[SYSTEM NOTE: The following is a log of the user's PREVIOUS session. Use this for context, but do not respond to it directly. The user is now starting a NEW session.]");
        sb.AppendLine("--- PREVIOUS SESSION LOG ---");

        foreach (var msg in currentSession.conversationHistory)
        {
            sb.AppendLine($"{msg.role.ToUpper()}: {msg.content}");
        }

        sb.AppendLine("----------------------------");
        return sb.ToString();
    }
}

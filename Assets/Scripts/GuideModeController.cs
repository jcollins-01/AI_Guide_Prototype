using UnityEngine;

/// <summary>
/// Study condition switcher: Human Guide, Baseline AI Guide, or Improved AI Guide.
/// Wire the bools or public methods to UI buttons in the Inspector.
/// </summary>
[DefaultExecutionOrder(100)]
public class GuideModeController : MonoBehaviour
{
    [Header("Guide mode")]
    public bool humanGuideOn;
    public bool baselineAIGuideOn = true;
    public bool improvedAIGuideOn;

    public enum HumanNetworkRole
    {
        Auto,
        Guide,
        Participant
    }

    [Header("Human guide")]
    [Tooltip("Your role in a networked human-guide session. Auto assigns by join order when a second player connects.")]
    public HumanNetworkRole humanNetworkRole = HumanNetworkRole.Participant;

    [Header("Improved AI guide")]
    public bool useImprovedPlaceholderPrompt = true;
    [TextArea(4, 12)]
    public string improvedGuidePlaceholderPrompt =
        "You are Giddy, an improved sighted guide for a blind player. " +
        "PLACEHOLDER: Replace this with intent-based guideline switching.";

    private SharedMovement sharedMovement;
    private AIGuide aiGuide;
    private RealtimeGuideClient realtimeClient;
    private OpenAIQueries openAIQueries;
    private Behaviour[] aiGuideBehaviours;
    private GameObject aiGuideRoot;
    private GuideMode lastAppliedMode = GuideMode.None;

    private enum GuideMode
    {
        None,
        Human,
        BaselineAI,
        ImprovedAI
    }

    private void Start()
    {
        CacheReferences();
        ApplyActiveMode();
    }

    private void Update()
    {
        GuideMode desired = GetDesiredMode();
        if (desired != lastAppliedMode)
            ApplyActiveMode();
    }

    public void SetHumanGuideOn()
    {
        humanGuideOn = true;
        baselineAIGuideOn = false;
        improvedAIGuideOn = false;
        ApplyActiveMode();
    }

    public void SetHumanNetworkRoleGuide()
    {
        humanNetworkRole = HumanNetworkRole.Guide;
        if (humanGuideOn)
            ApplyActiveMode();
    }

    public void SetHumanNetworkRoleParticipant()
    {
        humanNetworkRole = HumanNetworkRole.Participant;
        if (humanGuideOn)
            ApplyActiveMode();
    }

    public void SetBaselineAIGuideOn()
    {
        humanGuideOn = false;
        baselineAIGuideOn = true;
        improvedAIGuideOn = false;
        ApplyActiveMode();
    }

    public void SetImprovedAIGuideOn()
    {
        humanGuideOn = false;
        baselineAIGuideOn = false;
        improvedAIGuideOn = true;
        ApplyActiveMode();
    }

    public void ApplyActiveMode()
    {
        CacheReferences();

        GuideMode mode = GetDesiredMode();
        if (mode == GuideMode.None)
        {
            Debug.LogWarning("[GuideModeController] No guide mode bool is enabled. Defaulting to Baseline AI.");
            SetBaselineAIGuideOn();
            return;
        }

        switch (mode)
        {
            case GuideMode.Human:
                ApplyHumanGuideMode();
                break;
            case GuideMode.ImprovedAI:
                ApplyAIGuideMode(BuildImprovedPrompt());
                break;
            default:
                ApplyAIGuideMode(BuildBaselinePrompt());
                break;
        }

        lastAppliedMode = mode;
    }

    private GuideMode GetDesiredMode()
    {
        int enabled = (humanGuideOn ? 1 : 0) + (baselineAIGuideOn ? 1 : 0) + (improvedAIGuideOn ? 1 : 0);
        if (enabled > 1)
            Debug.LogWarning("[GuideModeController] Multiple guide mode bools are true; Human > Improved > Baseline priority.");

        if (humanGuideOn)
            return GuideMode.Human;
        if (improvedAIGuideOn)
            return GuideMode.ImprovedAI;
        if (baselineAIGuideOn)
            return GuideMode.BaselineAI;
        return GuideMode.None;
    }

    private void ApplyHumanGuideMode()
    {
        SetAIGuideActive(false);

        RoomManager roomManager = FindObjectOfType<RoomManager>();
        if (roomManager != null)
            roomManager.AssignNetworkRoles();
        else
            Debug.LogWarning("[GuideModeController] RoomManager not found in scene.");

        Debug.Log("[GuideModeController] Human guide mode active as " + humanNetworkRole + ".");
    }

    private void ApplyAIGuideMode(string prompt)
    {
        SetAIGuideActive(true);

        if (sharedMovement != null && aiGuideRoot != null)
        {
            sharedMovement.theGuide = aiGuideRoot;
            ConfigureGuidePhysics(sharedMovement);
        }

        PushPromptToOpenAI(prompt);
        Debug.Log("[GuideModeController] AI guide mode active. Prompt length: " + prompt.Length);
    }

    private void SetAIGuideActive(bool active)
    {
        if (aiGuideBehaviours == null)
            return;

        foreach (Behaviour behaviour in aiGuideBehaviours)
        {
            if (behaviour != null)
                behaviour.enabled = active;
        }
    }

    private void ConfigureGuidePhysics(SharedMovement movement)
    {
        if (movement.theGuide == null)
            return;

        Rigidbody guideRigidbody = movement.theGuide.GetComponentInChildren<Rigidbody>();
        CapsuleCollider guideCollider = movement.theGuide.GetComponentInChildren<CapsuleCollider>();
        if (guideRigidbody == null || guideCollider == null)
        {
            Debug.LogWarning("[GuideModeController] Guide is missing Rigidbody or CapsuleCollider for shared movement.");
            return;
        }

        guideRigidbody.useGravity = false;
        guideRigidbody.isKinematic = true;
        guideCollider.isTrigger = true;
        guideCollider.radius = 50f;
        guideCollider.height = 0.5f;
        guideCollider.center = new Vector3(0f, 1f, 0f);
        movement.guideCollider = guideCollider;
    }

    private string BuildBaselinePrompt()
    {
        if (aiGuide == null)
        {
            Debug.LogError("[GuideModeController] AIGuide not found in scene.");
            return string.Empty;
        }

        return aiGuide.GetFormattedPrompt();
    }

    private string BuildImprovedPrompt()
    {
        string baseline = BuildBaselinePrompt();
        if (useImprovedPlaceholderPrompt)
            return improvedGuidePlaceholderPrompt;
        return baseline;
    }

    private void PushPromptToOpenAI(string prompt)
    {
        if (string.IsNullOrEmpty(prompt))
            return;

        if (openAIQueries != null)
            openAIQueries.text = prompt;

        if (realtimeClient != null)
            _ = realtimeClient.UpdateSystemInstructions(prompt);
    }

    private void CacheReferences()
    {
        if (sharedMovement == null)
            sharedMovement = FindObjectOfType<SharedMovement>();

        if (aiGuide == null)
            aiGuide = FindObjectOfType<AIGuide>();

        if (aiGuide != null)
        {
            aiGuideRoot = aiGuide.gameObject;
            if (realtimeClient == null)
                realtimeClient = aiGuide.GetComponent<RealtimeGuideClient>();
            if (openAIQueries == null)
                openAIQueries = aiGuide.GetComponent<OpenAIQueries>();

            if (aiGuideBehaviours == null)
            {
                aiGuideBehaviours = new Behaviour[]
                {
                    aiGuide,
                    realtimeClient,
                    openAIQueries,
                    aiGuide.GetComponent<AutomaticGuide>(),
                    aiGuide.GetComponent<AutomaticModification>(),
                    aiGuide.GetComponent<VRHandling>()
                };
            }
        }
    }
}

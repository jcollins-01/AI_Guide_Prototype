using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The guide's spoken introduction when a scene starts: what the project is, the buttons, what the vibration means,
// and to stand still while waiting for an answer. Fixed lines (not the model), so every participant hears the same thing.
public class GuideWelcome : MonoBehaviour
{
    // False while the welcome is still to come or playing, so other scripts (the soup task intro) wait for it
    public static bool Done { get; private set; } = true;

    public bool playWelcome = true;
    [Tooltip("Seconds after the guide connects before it starts talking, so the player has their bearings.")]
    public float delaySeconds = 2f;
    [Tooltip("Give up if the guide hasn't connected to Ollama after this many seconds.")]
    public float connectTimeout = 30f;

    [TextArea(2, 5)]
    public List<string> lines = new List<string>
    {
        "Welcome! This is a research project about helping blind and low-vision players find their way in virtual reality. I'm your guide.",
        "Here is how the controls work. Press the A button on your right controller, then ask me anything out loud. You'll hear a chime when I start listening, and another one when I've stopped.",
        "Press the X button on your left controller at any time to stop me talking.",
        "Use the left thumbstick to walk, and the right thumbstick to turn.",
        "When a hand gets close to something you can pick up, that controller starts to tick, faster and faster as you get closer, and then buzzes steadily when it's close enough to grab. Squeeze the grip button to pick it up. Touching furniture and other fixed things gives one strong pulse and then a light buzz.",
        "To find out what's next to a hand, press B on the right controller or Y on the left one, and I'll tell you what it is.",
        "You can always ask me what you're looking at, what is around you, or how to get somewhere. If you want me to lead you, take my arm and squeeze the grip button.",
        "One request: after you ask me something, please stay where you are until I have answered, because I describe things from the spot where you asked.",
        "Whenever you're ready, press A and ask me something."
    };

    [Tooltip("Said after the first line in scenes with the cooking task.")]
    [TextArea(2, 5)]
    public string chefLine = "And today I'm also your chef! We're cooking a carrot soup together. First we take the pot, fill it with " +
        "water and put it on the stove to heat. While the water heats up, we cut a carrot and put it in the pot, and we finish " +
        "with some spices. I'll help you find everything, step by step.";

    void Awake() => Done = !playWelcome;

    IEnumerator Start()
    {
        if (!playWelcome) yield break;

        RealtimeGuideClient guide = null;
        float waited = 0f;
        while (guide == null || !guide._isConnected || PlayerHead.Camera == null)
        {
            // Not "??": a missing component is a fake null in the Editor, which "??" doesn't treat as null
            if (guide == null) guide = GetComponent<RealtimeGuideClient>();
            if (guide == null) guide = FindObjectOfType<RealtimeGuideClient>();
            waited += Time.deltaTime;
            if (waited > connectTimeout)
            {
                Debug.LogWarning(guide == null ? "[Welcome] No RealtimeGuideClient in the scene; skipping the spoken welcome"
                    : !guide._isConnected ? "[Welcome] Guide never connected to Ollama; skipping the spoken welcome"
                    : "[Welcome] No player head camera found; skipping the spoken welcome");
                Done = true;
                yield break;
            }
            yield return null;
        }

        yield return new WaitForSeconds(delaySeconds);

        // One call: the client splits it into sentences and plays them back to back, and the text goes into the chat
        // history, so the model knows it has already introduced itself
        Debug.Log("[Welcome] Speaking the welcome and controls");
        var spoken = new List<string>(lines);
        if (FindObjectOfType<SoupTask>() != null && !string.IsNullOrWhiteSpace(chefLine))
            spoken.Insert(Mathf.Min(1, spoken.Count), chefLine);
        _ = guide.SpeakCustomText(string.Join(" ", spoken));
        yield return null;

        // Pressing A or X during the welcome cuts it short; either way it is done once the guide stops speaking
        while (guide._isAiSpeaking) yield return null;
        Done = true;
    }
}

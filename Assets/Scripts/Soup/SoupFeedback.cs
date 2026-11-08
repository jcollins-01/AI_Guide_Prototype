using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

// Sounds and vibration for the soup task. A blind player can't see that the pot snapped onto the burner or that the
// knob moved, so every state change gets a distinct sound at the object (3D, so it also tells them where it happened)
// and a pulse in the hand that did it. The sounds are generated in code so no audio assets are needed for the prototype.
public static class SoupFeedback
{
    private const int Rate = 44100;
    private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    public static AudioClip Click => Get("click", 0.03f, (t, n) => n * Mathf.Exp(-t * 250f));
    public static AudioClip Chop => Get("chop", 0.09f, (t, n) => (0.6f * n + 0.4f * Mathf.Sin(2 * Mathf.PI * 180f * t)) * Mathf.Exp(-t * 45f));
    public static AudioClip Clunk => Get("clunk", 0.18f, (t, n) => (0.8f * Mathf.Sin(2 * Mathf.PI * 110f * t) + 0.2f * n) * Mathf.Exp(-t * 22f));
    public static AudioClip Splash => Get("splash", 0.35f, (t, n) => n * Mathf.Exp(-t * 9f) * Mathf.Min(1f, t * 60f));
    public static AudioClip Shake => Get("shake", 0.14f, (t, n) => n * (Mathf.Sin(2 * Mathf.PI * 40f * t) > 0 ? 1f : 0.2f) * Mathf.Exp(-t * 14f) * 0.7f);
    public static AudioClip Pop => Get("pop", 0.06f, (t, n) => Mathf.Sin(2 * Mathf.PI * (600f + 2000f * t) * t) * Mathf.Exp(-t * 60f));
    public static AudioClip Done => Get("done", 0.9f, (t, n) =>
        (Mathf.Sin(2 * Mathf.PI * 523f * t) + (t > 0.25f ? Mathf.Sin(2 * Mathf.PI * 659f * t) : 0) + (t > 0.5f ? Mathf.Sin(2 * Mathf.PI * 784f * t) : 0)) * 0.3f * Mathf.Exp(-t * 2.5f));

    // Loops: running tap, gas flame, bubbling pot
    // A real recording (Resources/Audio/watersink) when there is one; the generated noise is only a fallback
    public static AudioClip Water
    {
        get
        {
            if (waterRecording == null) waterRecording = Resources.Load<AudioClip>("Audio/watersink");
            return waterRecording != null ? waterRecording : Get("water", 2f, LowPassed(0.25f), loop: true);
        }
    }
    private static AudioClip waterRecording;
    // Noise filtered twice down to a low rumble (plain noise sounds like radio static), with a slow 3 Hz flicker
    public static AudioClip Flame => Get("flame", 2f, Rumble(), loop: true);
    public static AudioClip Bubbling => Get("bubbling", 2f, (t, n) =>
    {
        float phase = (t * 9f) % 1f; // ~9 bubbles a second
        return Mathf.Sin(2 * Mathf.PI * (200f + 500f * phase) * t) * Mathf.Exp(-phase * 12f) * 0.5f + n * 0.05f;
    }, loop: true);

    private static System.Func<float, float, float> LowPassed(float amount)
    {
        float last = 0f;
        return (t, n) => last = Mathf.Lerp(last, n, amount);
    }

    private static System.Func<float, float, float> Rumble()
    {
        float a = 0f, b = 0f;
        return (t, n) =>
        {
            a = Mathf.Lerp(a, n, 0.04f);
            b = Mathf.Lerp(b, a, 0.04f);
            return b * 5f * (1f + 0.2f * Mathf.Sin(2 * Mathf.PI * 3f * t));
        };
    }

    private static AudioClip Get(string name, float seconds, System.Func<float, float, float> sample, bool loop = false)
    {
        if (clips.TryGetValue(name, out AudioClip clip) && clip != null) return clip;

        var random = new System.Random(name.GetHashCode());
        int count = Mathf.CeilToInt(seconds * Rate);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
            data[i] = Mathf.Clamp(sample((float)i / Rate, (float)(random.NextDouble() * 2 - 1)), -1f, 1f);

        // Fade the loop seam so looping sounds don't click
        if (loop)
            for (int i = 0; i < 400; i++) { float f = i / 400f; data[i] *= f; data[count - 1 - i] *= f; }

        clip = AudioClip.Create("Soup " + name, count, 1, Rate, false);
        clip.SetData(data, 0);
        clips[name] = clip;
        return clip;
    }

    // A 3D source on the object, so sounds come from where the thing is
    public static AudioSource AddSource(GameObject obj, bool loop = false)
    {
        AudioSource source = obj.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 0.5f;
        source.maxDistance = 12f;
        source.loop = loop;
        return source;
    }

    public static void Play(AudioSource source, AudioClip clip, float volume = 1f)
    {
        if (source != null) source.PlayOneShot(clip, volume);
    }

    // Vibrates the hand that did the interaction, or both hands if we don't know which
    public static void Pulse(IXRInteractor interactor, float amplitude = 0.5f, float duration = 0.1f)
    {
        if (interactor is XRBaseControllerInteractor controller && controller.SendHapticImpulse(amplitude, duration))
            return;
        InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).SendHapticImpulse(0, amplitude, duration);
        InputDevices.GetDeviceAtXRNode(XRNode.RightHand).SendHapticImpulse(0, amplitude, duration);
    }
}

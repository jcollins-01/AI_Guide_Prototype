using System;
using UnityEngine;

// The burner on top of the stove: a pot let go near it snaps onto it (SnapSpot), and it glows and hisses with the heat
public class StoveBurner : MonoBehaviour
{
    public StoveKnob knob;
    public SoupPot pot;
    public Renderer glow;

    public bool PotOnBurner => spot != null && spot.Occupied;
    public bool IsOn => knob != null && knob.level > 0;
    public event Action<bool> PotPlacedChanged;

    private SnapSpot spot;
    private AudioSource hissSource;
    private Material glowMaterial;
    private static readonly Color Cold = new Color(0.15f, 0.15f, 0.15f);

    void Awake()
    {
        spot = GetComponent<SnapSpot>();
        if (spot == null) spot = gameObject.AddComponent<SnapSpot>();
        spot.OccupiedChanged += on =>
        {
            Debug.Log($"[Soup] Pot {(on ? "placed on" : "taken off")} the burner");
            PotPlacedChanged?.Invoke(on);
        };
        hissSource = SoupFeedback.AddSource(gameObject, loop: true);
        hissSource.clip = SoupFeedback.Flame;
        if (glow != null) glowMaterial = glow.material;
    }

    void Start()
    {
        if (spot.item == null && pot != null) spot.item = pot.Item;
    }

    void Update()
    {
        int level = knob != null ? knob.level : 0;
        if (glowMaterial != null)
            glowMaterial.color = Color.Lerp(Cold, new Color(1f, 0.25f, 0.05f), level / 3f);
        if (level > 0 && !hissSource.isPlaying) hissSource.Play();
        if (level == 0 && hissSource.isPlaying) hissSource.Stop();
        hissSource.volume = 0.12f + 0.06f * level; // a soft rumble under everything else, a bit louder when higher
    }
}

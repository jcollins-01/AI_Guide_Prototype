using System;
using UnityEngine;

// The soup pot: how full of water it is, how hot, whether the carrots and spices are in, and the bubbling sound
[RequireComponent(typeof(SoupItem))]
public class SoupPot : MonoBehaviour
{
    [Range(0, 1)] public float water = 0f;
    public bool carrotsIn = false;
    public bool spicesIn = false;
    [Range(0, 1), Tooltip("0 = cold, 1 = boiling.")]
    public float temperature = 0f;
    [Tooltip("Seconds on medium heat until the water boils (low takes twice as long, high a third less).")]
    public float heatSeconds = 20f;
    [Tooltip("Seconds of running tap to fill the pot.")]
    public float fillSeconds = 4f;

    [Tooltip("Blue disc inside the pot, raised as it fills (for sighted observers).")]
    public Transform waterVisual;
    public GameObject carrotsVisual;
    public GameObject spicesVisual;
    [Tooltip("Local position of the water surface when empty and when full.")]
    public Vector3 waterEmptyLocal, waterFullLocal;

    public event Action Filled;
    public event Action CarrotsAdded;
    public event Action SpicesAdded;
    public event Action Boiling;

    public SoupItem Item { get; private set; }
    public bool IsFull => water >= 0.999f;
    public bool HasWater => water >= 0.5f;
    public bool IsBoiling => temperature >= 0.999f;

    private AudioSource source;
    private AudioSource bubbleSource;

    void Awake()
    {
        Item = GetComponent<SoupItem>();
        source = SoupFeedback.AddSource(gameObject);
        bubbleSource = SoupFeedback.AddSource(gameObject, loop: true);
        bubbleSource.clip = SoupFeedback.Bubbling;
        UpdateVisuals();
    }

    void Update()
    {
        // Chopped carrots that touch the top of the pot go in
        Bounds b = Item.Bounds();
        Vector3 top = new Vector3(b.center.x, b.max.y, b.center.z);
        foreach (Collider c in Physics.OverlapSphere(top, Mathf.Max(b.extents.x, b.extents.z) + 0.05f, ~0, QueryTriggerInteraction.Collide))
        {
            SoupItem other = c.attachedRigidbody != null ? c.attachedRigidbody.GetComponent<SoupItem>() : null;
            if (other != null && other.kind == SoupItem.Kind.ChoppedCarrots)
                AddCarrots(other);
        }
    }

    // Called by the sink tap every frame the pot is under running water
    public void Fill(float deltaTime)
    {
        if (IsFull) return;
        water = Mathf.Min(1f, water + deltaTime / fillSeconds);
        UpdateVisuals();
        if (IsFull)
        {
            SoupFeedback.Play(source, SoupFeedback.Pop);
            SoupFeedback.Pulse(Item.IsHeld ? Item.Grab.interactorsSelecting[0] : null, 0.6f, 0.2f);
            Filled?.Invoke();
        }
    }

    public void AddCarrots(SoupItem chopped)
    {
        if (chopped != null) Destroy(chopped.gameObject);
        if (carrotsIn) return;
        carrotsIn = true;
        SoupFeedback.Play(source, SoupFeedback.Splash);
        UpdateVisuals();
        CarrotsAdded?.Invoke();
    }

    // Called by the soup task every frame the pot is on a lit burner
    public void HeatUp(float deltaTime, int heatLevel)
    {
        if (IsBoiling || !HasWater || heatLevel <= 0) return;
        float rate = heatLevel == 1 ? 0.5f : heatLevel == 2 ? 1f : 1.5f;
        temperature = Mathf.Min(1f, temperature + deltaTime * rate / heatSeconds);
        if (IsBoiling)
        {
            SoupFeedback.Play(source, SoupFeedback.Pop);
            Boiling?.Invoke();
        }
    }

    public void AddSpices()
    {
        if (spicesIn) return;
        spicesIn = true;
        SoupFeedback.Play(source, SoupFeedback.Pop);
        UpdateVisuals();
        SpicesAdded?.Invoke();
    }

    // 0 = silent, 1 = gentle simmer, 2 = rolling boil
    public void SetBubbling(float intensity)
    {
        if (intensity <= 0.01f) { if (bubbleSource.isPlaying) bubbleSource.Stop(); return; }
        if (!bubbleSource.isPlaying) bubbleSource.Play();
        bubbleSource.volume = Mathf.Clamp(0.05f + 0.07f * intensity, 0f, 0.25f);
        bubbleSource.pitch = 0.8f + 0.3f * intensity;
    }

    private void UpdateVisuals()
    {
        if (waterVisual != null)
        {
            waterVisual.gameObject.SetActive(water > 0.01f);
            waterVisual.localPosition = Vector3.Lerp(waterEmptyLocal, waterFullLocal, water);
        }
        if (carrotsVisual != null) carrotsVisual.SetActive(carrotsIn);
        if (spicesVisual != null) spicesVisual.SetActive(spicesIn);
    }
}

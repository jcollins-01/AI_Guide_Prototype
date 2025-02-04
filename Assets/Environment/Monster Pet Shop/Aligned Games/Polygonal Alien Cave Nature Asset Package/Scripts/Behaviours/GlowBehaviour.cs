using UnityEngine;

namespace AlignedGames

{
    public class GlowBehaviour : MonoBehaviour
    {
        public float fadeSpeed = 1f; // Speed of the fade
        public bool useSecondMaterial = false; // Toggle to choose the second material

        public float minGlow = 0.3f;
        public float maxGlow = 0.8f;

        private Material material;

        private float targetSmoothness; // The target value for smoothness
        private float currentSmoothness; // The current value of smoothness

        void Start()
        {
            // Get the Renderer component
            Renderer renderer = GetComponent<Renderer>();

            if (renderer != null && renderer.materials.Length > 0)
            {
                // Select the appropriate material based on the toggle
                material = renderer.materials[useSecondMaterial ? 1 : 0];
            }
            else
            {
                Debug.LogError("No Renderer found on this GameObject or no materials assigned.");
            }

            // Initialize the smoothness values
            currentSmoothness = material.GetFloat("_Smoothness");
            targetSmoothness = currentSmoothness; // Start at the current smoothness
        }

        void Update()
        {
            // Check if material is assigned
            if (material != null)
            {
                // Update the smoothness value
                currentSmoothness = Mathf.MoveTowards(currentSmoothness, targetSmoothness, fadeSpeed * Time.deltaTime);
                material.SetFloat("_Smoothness", currentSmoothness);

                // Fade in and out logic
                if (Mathf.Approximately(currentSmoothness, targetSmoothness))
                {
                    // Switch target smoothness between minGlow and maxGlow
                    targetSmoothness = targetSmoothness == maxGlow ? minGlow : maxGlow;
                }
            }
        }
    }
}
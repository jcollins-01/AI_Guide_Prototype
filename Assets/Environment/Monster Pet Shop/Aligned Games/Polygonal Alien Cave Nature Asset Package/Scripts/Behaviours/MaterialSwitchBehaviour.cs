using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AlignedGames

{

    public class MaterialSwitchBehaviour : MonoBehaviour
    {
        // Array to hold the materials to cycle through
        public Material[] materials;

        // Index to keep track of the current material
        public int currentMaterialIndex = 0;

        // To hold the currently applied material
        private Material newMaterial;

        void Start()
        {
            // Ensure there's at least one material in the array
            if (materials.Length > 0)
            {
                // Assign the first material to all objects at the start
                ApplyMaterial(materials[0]);

            }

        }

        void Update()
        {
            // Check if the 'T' key is pressed
            if (Input.GetKeyDown(KeyCode.T))
            {
                CycleMaterials();
            }

        }

        // Function to cycle through materials
        void CycleMaterials()
        {
            // Increment the material index, looping back if it exceeds the number of materials
            currentMaterialIndex = (currentMaterialIndex + 1) % materials.Length;

            // Get the next material in the array
            newMaterial = materials[currentMaterialIndex];

            // Apply the new material to all renderers with only one material in the scene
            ApplyMaterial(newMaterial);

            Debug.Log("Switched to material: " + newMaterial.name);
        }

        // Function to apply a material to all renderers that have exactly one material
        void ApplyMaterial(Material material)
        {
            // Find all renderers in the scene
            Renderer[] renderers = FindObjectsOfType<Renderer>();

            // Iterate through each renderer and change its material if it has exactly one material
            foreach (Renderer renderer in renderers)
            {
                // Check if the renderer has exactly one material
                if (renderer.materials.Length == 1)
                {
                    // Assign the new material to the renderer
                    Material[] rendererMaterials = renderer.materials;
                    rendererMaterials[0] = material; // Since it has only one material, we replace the first one
                    renderer.materials = rendererMaterials;
                }
            }
        }
    }

}
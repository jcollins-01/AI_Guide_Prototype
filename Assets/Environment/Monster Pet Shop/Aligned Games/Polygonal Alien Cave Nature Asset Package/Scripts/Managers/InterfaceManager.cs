using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace AlignedGames

{

    public class InterfaceManager : MonoBehaviour
    {
        public GameObject Interface;       
        public GameObject MaterialText;

        void Start()
        {
            // Ensure the interface is visible at the start
            if (Interface != null)
            {
                Interface.SetActive(true);
            }
            else
            {
                Debug.LogWarning("Interface is not assigned in the Inspector.");
            }

            if (MaterialText == null)
            {
                Debug.LogWarning("MaterialText is not assigned in the Inspector.");
            }
        }

        void Update()
        {
            // Check if MaterialSwitchBehaviour exists on this GameObject
            MaterialSwitchBehaviour materialSwitch = GetComponent<MaterialSwitchBehaviour>();

            if (materialSwitch != null && MaterialText != null)
            {
                // Access the TextMeshProUGUI component
                TextMeshProUGUI materialTextComponent = MaterialText.GetComponent<TextMeshProUGUI>();
                if (materialTextComponent != null)
                {
                    // Update the material name in the UI text element
                    materialTextComponent.text = "Current Material: " + materialSwitch.materials[materialSwitch.currentMaterialIndex].name;

                    if ((materialTextComponent.text == "Current Material: atlass_1_1") || (materialTextComponent.text == "Current Material: atlass_1_2"))

                    {

                        materialTextComponent.text = "Current Material: AlignedGames/SpecialToon";

                    }

                }
                else
                {
                    Debug.LogWarning("MaterialText does not have a TextMeshProUGUI component.");
                }
            }

            // Toggle the Interface visibility when 'I' is pressed
            if (Input.GetKeyDown(KeyCode.I) && Interface != null)
            {
                Interface.SetActive(!Interface.activeSelf);
            }
        }
    }

}
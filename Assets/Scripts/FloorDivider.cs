using UnityEngine;

public class FloorDivider : MonoBehaviour
{
    public int rows = 2; // Number of rows to divide the area into
    public int columns = 2; // Number of columns to divide the area into
    [HideInInspector]
    public GameObject floorPrefab; // Assign a plane prefab here if desired
    public Material defaultMaterial; // Optional: assign a default material here, or it will use "Transparent"

    public void GenerateFloorSections()
    {
        // Clear any existing floor sections to prevent duplicates
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Floor Section"))
            {
                DestroyImmediate(child.gameObject);
            }
        }

        // Find the "Bounds" object in the scene
        GameObject boundsObject = GameObject.Find("Bounds");
        if (boundsObject == null)
        {
            Debug.LogError("Bounds object not found. Please create a GameObject named 'Bounds' in the scene.");
            return;
        }

        // Get the size and position of the Bounds object
        Renderer boundsRenderer = boundsObject.GetComponent<Renderer>();
        if (boundsRenderer == null)
        {
            Debug.LogError("Bounds object must have a Renderer component to determine its size.");
            return;
        }

        Vector3 boundsSize = boundsRenderer.bounds.size;
        Vector3 boundsCenter = boundsRenderer.bounds.center;

        // Calculate the size of each floor section
        float sectionWidth = boundsSize.x / columns;
        float sectionLength = boundsSize.z / rows;

        // Find or assign the default material
        if (defaultMaterial == null)
        {
            defaultMaterial = Resources.Load<Material>("Screenreader/Transparent");
            if (defaultMaterial == null)
            {
                Debug.LogError("Default material 'Transparent' not found in Resources. Please assign it manually.");
                return;
            }
        }

        // Loop through rows and columns to create floor sections
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                // Calculate the position for each floor section
                Vector3 sectionPosition = new Vector3(
                    boundsCenter.x - boundsSize.x / 2 + sectionWidth * (col + 0.5f),
                    boundsCenter.y,
                    boundsCenter.z - boundsSize.z / 2 + sectionLength * (row + 0.5f)
                );

                // Create a floor section (Plane) at the calculated position
                GameObject floorSection;
                if (floorPrefab != null)
                {
                    floorSection = Instantiate(floorPrefab, sectionPosition, Quaternion.identity);
                }
                else
                {
                    floorSection = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    floorSection.transform.position = sectionPosition;
                    floorSection.transform.localScale = new Vector3(sectionWidth / 10, 1, sectionLength / 10); // Adjust scale to match section size
                }

                // Set the name and parent of the floor section
                floorSection.name = $"Floor Section ({row},{col})";
                floorSection.transform.parent = boundsObject.transform;

                // Apply the default material
                Renderer renderer = floorSection.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = defaultMaterial;
                }
            }
        }
    }
}
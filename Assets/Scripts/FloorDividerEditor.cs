using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FloorDivider))]
public class FloorDividerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        FloorDivider floorDivider = (FloorDivider)target;
        if (GUILayout.Button("Generate Floor Sections"))
        {
            floorDivider.GenerateFloorSections();
        }
    }
}
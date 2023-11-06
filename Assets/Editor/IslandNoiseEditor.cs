using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(IslandNoise))]
public class IslandNoiseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector
        DrawDefaultInspector();

        // Get a reference to the IslandNoise script
        IslandNoise islandNoise = (IslandNoise)target;

        // Create sliders for the parameters
        islandNoise.octaves = EditorGUILayout.IntSlider("Octaves", islandNoise.octaves, 1, 8);
        islandNoise.persistence = EditorGUILayout.Slider("Persistence", islandNoise.persistence, 0, 1);
        islandNoise.lacunarity = EditorGUILayout.Slider("Lacunarity", islandNoise.lacunarity, 1, 4);

        // Add a button to regenerate noise
        if (GUILayout.Button("Generate Noise"))
        {
            islandNoise = (IslandNoise)target;
            islandNoise.GenerateNoise();

            // Find the IslandMesh component and regenerate the mesh
            IslandMesh islandMesh = islandNoise.GetComponent<IslandMesh>();
            if (islandMesh != null)
            {
                // Noise Version
                //islandMesh.InitializeMeshData();
                //islandMesh.GenerateMesh();
            }
        }
    }
}




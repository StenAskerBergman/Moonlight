using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GradientCombiner))]
public class GradientCombinerEditor : UniversalPreviewEditor
{

    /*
[CustomEditor(typeof(GradientCombiner))]
public class GradientCombinerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        GradientCombiner combiner = (GradientCombiner)target;

        if (GUILayout.Button("Generate Preview"))
        {
            combiner.GeneratePreview();
            Debug.Log("Generate Preview button clicked");

            Texture2D previewTexture = combiner.PreviewTexture;

            if (previewTexture != null)
            {
                Debug.Log($"Preview texture generated: {previewTexture.width}x{previewTexture.height}");

                // Save the texture to a file
                SaveTextureToFile(previewTexture);

                GUILayout.Label("Preview");
                Rect rect = new Rect(10, 10, 100, 100);  // Fixed dimensions
                EditorGUI.DrawPreviewTexture(rect, previewTexture, null, ScaleMode.ScaleToFit);

                Debug.Log($"Rect dimensions: {rect.width}x{rect.height}");
            }

            else
            {
                GUILayout.Label("Preview is Null");
                Debug.LogError("Preview is Null");
            }
        }
    }

    private void SaveTextureToFile(Texture2D texture)
    {
        byte[] bytes = texture.EncodeToPNG();
        string filePath = "Assets/Textures/PreviewTexture.png";
        System.IO.File.WriteAllBytes(filePath, bytes);
        AssetDatabase.Refresh();
        Debug.Log($"Texture saved to {filePath}");
    }    */
}

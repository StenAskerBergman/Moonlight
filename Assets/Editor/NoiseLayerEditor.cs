// NoiseLayerEditor.cs
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(NoiseLayer))]
public class NoiseLayerEditor : UniversalPreviewEditor
{
    /*
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        NoiseLayer noiseLayer = (NoiseLayer)target;

        if (noiseLayer.PreviewTexture != null)
        {
            GUILayout.Label("Preview");
            Rect rect = GUILayoutUtility.GetRect(100, 100);
            EditorGUI.DrawPreviewTexture(rect, noiseLayer.PreviewTexture, null, ScaleMode.ScaleToFit); 
            
            // Also works
            // EditorGUI.DrawTextureTransparent(rect, noiseLayer.previewTexture, ScaleMode.ScaleToFit);

        }
    }*/
}

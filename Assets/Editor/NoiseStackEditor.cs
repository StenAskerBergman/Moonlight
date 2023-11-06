using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NoiseStack))]
public class NoiseStackEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        NoiseStack noiseStack = (NoiseStack)target;

        if (GUILayout.Button("Regenerate Preview"))
        {
            noiseStack.GeneratePreview();
            Repaint();
        }

        if (noiseStack.PreviewTexture != null)
        {
            GUILayout.Label("Preview");
            Rect rect = GUILayoutUtility.GetRect(100, 100);
            EditorGUI.DrawPreviewTexture(rect, noiseStack.PreviewTexture, null, ScaleMode.ScaleToFit);
        }
    }
}

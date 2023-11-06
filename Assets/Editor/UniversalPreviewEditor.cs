using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ScriptableObject), true)]
public class UniversalPreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var previewableObject = target as IPreviewable;

        if (previewableObject != null)
        {
            previewableObject.GeneratePreview();

            if (previewableObject.PreviewTexture != null)
            {
                GUILayout.Label("Preview");
                Rect rect = GUILayoutUtility.GetRect(100, 100);
                EditorGUI.DrawPreviewTexture(rect, previewableObject.PreviewTexture, null, ScaleMode.ScaleToFit);
                EditorGUI.DrawTextureTransparent(rect, previewableObject.PreviewTexture, ScaleMode.ScaleToFit);
            }
        }
    }
}

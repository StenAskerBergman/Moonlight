using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(IslandMesh))]
public class IslandMeshEditor : UniversalPreviewEditor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        IslandMesh islandMesh = (IslandMesh)target;

        if (islandMesh.PreviewTexture != null)
        {
            if (GUILayout.Button("Flatten Mesh"))
            {
                islandMesh.FlattenMesh();
                RefrPreviewImage(true, islandMesh.PreviewTexture);

            }

            if (GUILayout.Button("Reshape Mesh"))
            {
                islandMesh.ReshapeMesh();
                RefrPreviewImage(true, islandMesh.PreviewTexture);

            }

            if (GUILayout.Button("Save Height Map"))
            {
                islandMesh.SaveHeightMap();
            }



            GUILayout.Label("Preview");
            RefrPreviewImage(true, islandMesh.PreviewTexture);

            // Also works
            // EditorGUI.DrawTextureTransparent(rect, noiseLayer.previewTexture, ScaleMode.ScaleToFit);

        }

        void RefrPreviewImage(bool isDirty, Texture texture)
        {
            if (isDirty)
            {
                Rect rect = GUILayoutUtility.GetRect(100, 100);
                EditorGUI.DrawPreviewTexture(rect, texture, null, ScaleMode.ScaleToFit);
                isDirty = false;
            }
            else
            {
                // Do nothing
            }
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ChildUIColor : MonoBehaviour
{
    [SerializeField]
    private Color color = Color.white;

    [SerializeField]
    private bool update;

    public void ApplyColor()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);

#if UNITY_EDITOR
        Undo.RecordObjects(graphics, "Apply Child UI Color");
#endif

        foreach (Graphic graphic in graphics)
        {
            if (graphic.gameObject == gameObject)
                continue;

            graphic.color = color;

#if UNITY_EDITOR
            EditorUtility.SetDirty(graphic);
#endif
        }
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (update && !Application.isPlaying)
            ApplyColor();
#endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ChildUIColor))]
public class ChildUIColorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ChildUIColor component = (ChildUIColor)target;

        EditorGUILayout.Space();

        if (GUILayout.Button("Apply Color To Children"))
            component.ApplyColor();
    }
}
#endif
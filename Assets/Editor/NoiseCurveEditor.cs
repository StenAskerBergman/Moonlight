using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NoiseCurve))]
public class NoiseCurveEditor : UniversalPreviewEditor
{
    /*
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        NoiseCurve noiseCurve = (NoiseCurve)target;

        // Add any additional inspector GUI code here

        // Example: Draw the curve in the inspector
        noiseCurve.curve = EditorGUILayout.CurveField("Curve", noiseCurve.curve);
    }*/
}

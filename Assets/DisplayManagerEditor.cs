using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor (typeof(DisplayManager))]
public class DisplayManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {

        DisplayManager displayManager = (DisplayManager)target;

            DrawDefaultInspector(); // base.OnInspectorGUI(); // Draws the default inspector + the custom inspector - Causes Replication Issues
            
        EditorGUILayout.LabelField("UI Management", EditorStyles.boldLabel);

            if (GUILayout.Button(new GUIContent("Adopt", "Add all UI children to the FullList.")))
            {
                displayManager.UpdateAdoption();
            }
            if (GUILayout.Button("Clear"))
            {
                displayManager.Clear();
            }

        EditorGUILayout.Space(10);

            if (GUILayout.Button("Show"))
            {
                displayManager.Show(displayManager.gameObject);
            }
            if (GUILayout.Button("Hide"))
            {
                displayManager.Hide(displayManager.gameObject);
            }


        EditorGUILayout.Space(10);

            if (GUILayout.Button("Show Over All"))
            {
                displayManager.ShowOverAll(displayManager.gameObject);
            }
            if (GUILayout.Button("Hide All"))
            {
                displayManager.HideAll();
            }
            if (GUILayout.Button("Clear All"))
            {
                displayManager.Clear(displayManager.FullList); 
                displayManager.Clear(displayManager.HideList); 
                displayManager.Clear(displayManager.ShowList);
            }
    }
}
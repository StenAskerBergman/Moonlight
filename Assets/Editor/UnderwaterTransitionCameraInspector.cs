using Moonlight.Rendering;
using UnityEditor;
using UnityEngine;

namespace Moonlight.EditorTools
{
    [InitializeOnLoad]
    internal static class UnderwaterTransitionCameraInspector
    {
        private static bool showControls = true;

        static UnderwaterTransitionCameraInspector()
        {
            Editor.finishedDefaultHeaderGUI += DrawCameraControls;
        }

        private static void DrawCameraControls(UnityEditor.Editor editor)
        {
            if (!(editor.target is Camera camera) || !camera.CompareTag("MainCamera"))
                return;

            UnderwaterTransitionController controller = FindController(camera);

            EditorGUILayout.Space();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                showControls = EditorGUILayout.Foldout(showControls, "Underwater Transition", true);
                if (!showControls)
                    return;

                if (controller == null)
                {
                    EditorGUILayout.HelpBox(
                        "No Underwater Transition Controller targets this camera. Add one to this camera or a parent object, then assign Target Camera here.",
                        MessageType.Info);
                    return;
                }

                var serializedController = new SerializedObject(controller);
                serializedController.UpdateIfRequiredOrScript();

                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("Controller", controller, typeof(UnderwaterTransitionController), true);

                DrawSection("Detection", serializedController,
                    "targetCamera", "waterSurface", "waterHeight", "surfaceHysteresis", "trackedDiveUnit");
                DrawSection("Transition", serializedController,
                    "diveDuration", "surfaceDuration", "transitionCurve");
                DrawToggleSection("Pre-Crossing Timing", serializedController, "enablePreCrossingTiming",
                    "preCrossingDistance");
                DrawSection("Surface Optics", serializedController,
                    "underwaterColor", "distortionStrength", "surfaceEdgeWidth");
                DrawToggleSection("HUD Concealment", serializedController, "enableHudConcealment",
                    "uiConcealmentLayer", "hudCanvasGroup", "underwaterHudObject", "submergedHudAlpha", "surfacedHudAlpha", "hideCanvasWhenFullySubmerged");
                DrawSection("Depth", serializedController,
                    "shallowWaterColor", "deepWaterColor", "abyssalColor", "absorptionCoefficients",
                    "fogDensity", "deepDepthThreshold", "abyssDepthThreshold", "sunScatteringIntensity",
                    "sunDepthExtinction");
                DrawToggleSection("Lower Apron Fade", serializedController, "enableLowerApronFade",
                    "lowerApronFadeStart", "lowerApronFadeEnd", "lowerApronFadeStrength");

                DrawToggleSection("Caustics", serializedController, "enableCaustics",
                    "causticsStrength", "causticsScale", "causticsSpeed", "causticsFadeDepth");
                DrawToggleSection("Marine Snow", serializedController, "enableMarineSnow",
                    "marineSnowIntensity", "marineSnowScale", "marineSnowSpeed");
                DrawSection("God Rays", serializedController, "godRayIntensity");
                DrawToggleSection("Drifting Debris", serializedController, "enableDebris",
                    "debrisDensity", "debrisBrightness", "debrisDriftSpeed");
                DrawToggleSection("Surface Droplets", serializedController, "enableSurfaceDroplets",
                    "dropletIntensity", "dropletFallSpeed");
                DrawToggleSection("Dive Splash", serializedController, "playDiveSound",
                    "diveSound", "diveVolume");
                DrawToggleSection("Surface Splash", serializedController, "playSurfaceSound",
                    "surfaceSound", "surfaceVolume");
                DrawToggleSection("Underwater Audio Muffling", serializedController, "enableUnderwaterMuffling",
                    "underwaterCutoffFrequency", "surfacedCutoffFrequency", "lowPassResonance");

                serializedController.ApplyModifiedProperties();
            }
        }

        private static UnderwaterTransitionController FindController(Camera camera)
        {
            UnderwaterTransitionController controller = camera.GetComponentInParent<UnderwaterTransitionController>();
            if (controller != null)
                return controller;

            UnderwaterTransitionController[] controllers = Object.FindObjectsOfType<UnderwaterTransitionController>(true);
            foreach (UnderwaterTransitionController candidate in controllers)
            {
                var serializedCandidate = new SerializedObject(candidate);
                SerializedProperty targetCamera = serializedCandidate.FindProperty("targetCamera");
                if (targetCamera != null && targetCamera.objectReferenceValue == camera)
                    return candidate;
            }

            return null;
        }

        private static void DrawSection(string title, SerializedObject serializedObject, params string[] propertyNames)
        {
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (string propertyName in propertyNames)
                DrawProperty(serializedObject, propertyName);
        }

        private static void DrawToggleSection(string title, SerializedObject serializedObject, string toggleName, params string[] propertyNames)
        {
            EditorGUILayout.Space(3f);
            SerializedProperty toggle = serializedObject.FindProperty(toggleName);
            if (toggle == null)
                return;

            EditorGUILayout.PropertyField(toggle, new GUIContent(title));
            using (new EditorGUI.DisabledScope(!toggle.boolValue))
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (string propertyName in propertyNames)
                    DrawProperty(serializedObject, propertyName);
            }
        }

        private static void DrawProperty(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, true);
        }
    }
}

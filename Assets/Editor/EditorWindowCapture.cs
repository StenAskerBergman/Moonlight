using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Captures visible Unity Editor windows for review outside Unity. Captures are
/// intentionally explicit: Unity change notifications do not reliably identify
/// which docked window visually represents a changed object.
/// </summary>
public static class EditorWindowCapture
{
    public static readonly string OutputDirectory = Path.GetFullPath(
        Path.Combine(Application.dataPath, "..", "Temp", "EditorWindowCaptures"));

    public static readonly string LatestPath = Path.Combine(OutputDirectory, "Latest.png");
    public static readonly string ComponentLatestPath = Path.Combine(OutputDirectory, "ComponentLatest.png");

    [MenuItem("Tools/Moonlight/Capture/Focused Editor Window")]
    public static void CaptureFocusedWindow()
    {
        Capture(EditorWindow.focusedWindow);
    }

    [MenuItem("Tools/Moonlight/Capture/Inspector")]
    public static void CaptureInspector()
    {
        EditorWindow focused = EditorWindow.focusedWindow;
        EditorWindow inspector = focused != null && focused.titleContent.text == "Inspector"
            ? focused
            : Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(window => window != null && window.titleContent.text == "Inspector");

        Capture(inspector);
    }

    [MenuItem("CONTEXT/Component/Capture Component for Chat")]
    private static void OpenComponentCapture(MenuCommand command)
    {
        Component component = command.context as Component;
        if (component == null)
        {
            Debug.LogWarning("Component capture skipped: the context target is not a Component.");
            return;
        }

        ComponentCaptureWindow.Open(component);
    }

    public static void Capture(EditorWindow window)
    {
        Capture(window, LatestPath);
    }

    public static void CaptureComponentWindow(EditorWindow window)
    {
        Capture(window, ComponentLatestPath);
    }

    private static void Capture(EditorWindow window, string outputPath)
    {
        if (window == null)
        {
            Debug.LogWarning("Editor window capture skipped: no matching visible window was found.");
            return;
        }

        Rect windowRect = window.position;
        string windowTitle = window.titleContent.text;
        window.Repaint();

        // Delay until the requested repaint has reached the screen buffer.
        EditorApplication.delayCall += () => CaptureScreenRect(windowRect, windowTitle, outputPath);
    }

    private static void CaptureScreenRect(Rect screenRect, string windowTitle, string outputPath)
    {
        int width = Mathf.Max(1, Mathf.RoundToInt(screenRect.width));
        int height = Mathf.Max(1, Mathf.RoundToInt(screenRect.height));
        Vector2 screenPosition = new Vector2(
            Mathf.Round(screenRect.x),
            Mathf.Round(screenRect.y));

        Texture2D screenshot = null;
        try
        {
            Color[] pixels = InternalEditorUtility.ReadScreenPixel(screenPosition, width, height);
            if (pixels == null || pixels.Length != width * height)
            {
                Debug.LogError($"Editor window capture failed for '{windowTitle}': no pixels were returned.");
                return;
            }

            screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenshot.SetPixels(pixels);
            screenshot.Apply(false, false);

            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllBytes(outputPath, screenshot.EncodeToPNG());
            Debug.Log($"<color=cyan>[Editor Window Capture]</color> Captured '{windowTitle}' to '{outputPath}'.");
        }
        finally
        {
            if (screenshot != null)
            {
                Object.DestroyImmediate(screenshot);
            }
        }
    }
}

/// <summary>Isolated, scrollable inspector for one selected component.</summary>
internal sealed class ComponentCaptureWindow : EditorWindow
{
    private Component targetComponent;
    private Editor componentEditor;
    private Vector2 scrollPosition;

    public static void Open(Component component)
    {
        ComponentCaptureWindow window = CreateInstance<ComponentCaptureWindow>();
        window.targetComponent = component;
        window.componentEditor = Editor.CreateEditor(component);
        window.titleContent = new GUIContent($"Capture: {component.GetType().Name}");
        window.minSize = new Vector2(420f, 360f);
        window.position = new Rect(window.position.position, new Vector2(520f, 700f));
        window.ShowUtility();
    }

    private void OnGUI()
    {
        if (targetComponent == null || componentEditor == null)
        {
            EditorGUILayout.HelpBox("The captured component is no longer available.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField(targetComponent.gameObject.name, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(targetComponent.GetType().Name, EditorStyles.miniLabel);
        EditorGUILayout.Space(3f);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        componentEditor.DrawHeader();
        componentEditor.OnInspectorGUI();
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(3f);
        if (GUILayout.Button("Capture This Component for Chat", GUILayout.Height(28f)))
        {
            EditorWindowCapture.CaptureComponentWindow(this);
        }
    }

    private void OnDisable()
    {
        if (componentEditor != null)
        {
            Object.DestroyImmediate(componentEditor);
        }
    }
}

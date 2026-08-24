#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class PreLobbyUIBuilder
{
    private static readonly Color Navy = new Color32(7, 15, 28, 255);
    private static readonly Color Card = new Color32(16, 29, 48, 245);
    private static readonly Color Field = new Color32(25, 43, 66, 255);
    private static readonly Color Accent = new Color32(89, 199, 217, 255);
    private static readonly Color Muted = new Color32(151, 169, 190, 255);

    [MenuItem("Moonlight/Build Pre-Lobby UI")]
    public static void Build()
    {
        GameObject old = GameObject.Find("PreLobby UI");
        if (old != null) Object.DestroyImmediate(old);

        GameObject debugLauncher = GameObject.Find("Debug Match Launcher");
        if (debugLauncher != null) Object.DestroyImmediate(debugLauncher);

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create pre-lobby EventSystem");
        }

        var root = new GameObject("PreLobby UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(root, "Create pre-lobby UI");
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        Image background = Image("Background", root.transform, Navy);
        Stretch(background.rectTransform);

        Image glow = Image("Moon Glow", background.transform, new Color32(24, 72, 94, 120));
        SetRect(glow.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(420, -180), new Vector2(820, 820));
        glow.raycastTarget = false;

        TMP_Text brand = Text("Brand", background.transform, "MOONLIGHT  /  NEW VOYAGE", 18, Accent, FontStyles.Bold);
        SetRect(brand.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(112, -82), new Vector2(560, 40));
        brand.characterSpacing = 4;

        TMP_Text title = Text("Title", background.transform, "Shape the world\nbefore dawn.", 72, Color.white, FontStyles.Bold);
        SetRect(title.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(112, 90), new Vector2(760, 210));
        title.lineSpacing = -12;

        TMP_Text intro = Text("Intro", background.transform,
            "Choose the rules for your expedition. You can refine your crew\nand invite players in the next lobby.", 22, Muted, FontStyles.Normal);
        SetRect(intro.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(116, -95), new Vector2(720, 100));

        TMP_Text step = Text("Step", background.transform, "01  CONFIGURE    02  ASSEMBLE    03  DEPART", 16, Muted, FontStyles.Normal);
        SetRect(step.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(112, 72), new Vector2(700, 32));
        step.characterSpacing = 2;

        Image card = Image("Configuration Card", background.transform, Card);
        SetRect(card.rectTransform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-120, 0), new Vector2(660, 860));

        TMP_Text kicker = Text("Kicker", card.transform, "EXPEDITION SETUP", 16, Accent, FontStyles.Bold);
        SetRect(kicker.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(54, -50), new Vector2(500, 30));
        kicker.characterSpacing = 3;
        TMP_Text heading = Text("Heading", card.transform, "Create a new session", 34, Color.white, FontStyles.Bold);
        SetRect(heading.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(54, -94), new Vector2(550, 50));

        TMP_InputField name = Input(card.transform, "Session Name", new Vector2(54, -205), "New Expedition");
        TMP_Dropdown pattern = Dropdown(card.transform, "Island Pattern", new Vector2(54, -335));
        TMP_Dropdown faction = Dropdown(card.transform, "Starting Faction", new Vector2(54, -465));

        Label(card.transform, "ISLAND COUNT", new Vector2(54, -555));
        Slider slider = SliderControl(card.transform, new Vector2(54, -605));
        TMP_Text count = Text("Island Count Value", card.transform, "4", 20, Color.white, FontStyles.Bold);
        SetRect(count.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-55, -555), new Vector2(50, 30));
        count.alignment = TextAlignmentOptions.Right;

        Button back = ButtonControl(card.transform, "Back", new Vector2(54, 50), new Vector2(180, 58), false);
        Button proceed = ButtonControl(card.transform, "Continue to Lobby", new Vector2(254, 50), new Vector2(352, 58), true);

        PreLobbyController controller = root.AddComponent<PreLobbyController>();
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("matchNameInput").objectReferenceValue = name;
        serialized.FindProperty("spawnPatternDropdown").objectReferenceValue = pattern;
        serialized.FindProperty("islandCountSlider").objectReferenceValue = slider;
        serialized.FindProperty("islandCountValue").objectReferenceValue = count;
        serialized.FindProperty("factionDropdown").objectReferenceValue = faction;
        serialized.FindProperty("continueButton").objectReferenceValue = proceed;
        serialized.FindProperty("backButton").objectReferenceValue = back;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = root;
        Debug.Log("Pre-lobby UI built and wired.");
    }

    private static TMP_InputField Input(Transform parent, string label, Vector2 pos, string placeholder)
    {
        Label(parent, label.ToUpperInvariant(), pos);
        GameObject go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = label;
        go.transform.SetParent(parent, false);
        SetRect((RectTransform)go.transform, new Vector2(0, 1), new Vector2(0, 1), pos + new Vector2(0, -48), new Vector2(552, 58));
        go.GetComponent<Image>().color = Field;
        TMP_InputField field = go.GetComponent<TMP_InputField>();
        field.text = placeholder;
        field.textComponent.color = Color.white;
        field.textComponent.fontSize = 19;
        field.placeholder.GetComponent<TMP_Text>().color = Muted;
        return field;
    }

    private static TMP_Dropdown Dropdown(Transform parent, string label, Vector2 pos)
    {
        Label(parent, label.ToUpperInvariant(), pos);
        GameObject go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        go.name = label;
        go.transform.SetParent(parent, false);
        SetRect((RectTransform)go.transform, new Vector2(0, 1), new Vector2(0, 1), pos + new Vector2(0, -48), new Vector2(552, 58));
        go.GetComponent<Image>().color = Field;
        TMP_Dropdown dropdown = go.GetComponent<TMP_Dropdown>();
        dropdown.captionText.color = Color.white;
        dropdown.captionText.fontSize = 19;
        return dropdown;
    }

    private static Slider SliderControl(Transform parent, Vector2 pos)
    {
        GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
        go.name = "Island Count";
        go.transform.SetParent(parent, false);
        SetRect((RectTransform)go.transform, new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(552, 34));
        Slider slider = go.GetComponent<Slider>();
        slider.minValue = 1;
        slider.maxValue = 12;
        slider.wholeNumbers = true;
        slider.value = 4;
        slider.fillRect.GetComponent<Image>().color = Accent;
        slider.targetGraphic.color = Color.white;
        return slider;
    }

    private static Button ButtonControl(Transform parent, string text, Vector2 pos, Vector2 size, bool primary)
    {
        GameObject go = DefaultControls.CreateButton(new DefaultControls.Resources());
        go.name = text;
        go.transform.SetParent(parent, false);
        SetRect((RectTransform)go.transform, new Vector2(0, 0), new Vector2(0, 0), pos, size);
        Image image = go.GetComponent<Image>();
        image.color = primary ? Accent : Field;
        Object.DestroyImmediate(go.GetComponentInChildren<Text>());
        TMP_Text label = Text("Label", go.transform, text, 18, primary ? Navy : Color.white, FontStyles.Bold);
        Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.Center;
        return go.GetComponent<Button>();
    }

    private static void Label(Transform parent, string text, Vector2 pos)
    {
        TMP_Text label = Text(text, parent, text, 14, Muted, FontStyles.Bold);
        SetRect(label.rectTransform, new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(500, 26));
        label.characterSpacing = 2;
    }

    private static Image Image(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text Text(string name, Transform parent, string value, float size, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = size;
        // This project's default TMP material has a black face. Clone it per text
        // before correcting the face so one label cannot recolor all other labels.
        text.fontMaterial = new Material(text.fontSharedMaterial);
        text.faceColor = Color.white;
        text.color = color;
        text.fontStyle = style;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
#endif

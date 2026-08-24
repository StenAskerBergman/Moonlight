using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Enums;

/// <summary>Collects the pre-lobby choices and hands them to the match scene.</summary>
public class PreLobbyController : MonoBehaviour
{
    [Header("Fields")]
    [SerializeField] private TMP_InputField matchNameInput;
    [SerializeField] private TMP_Dropdown spawnPatternDropdown;
    [SerializeField] private Slider islandCountSlider;
    [SerializeField] private TMP_Text islandCountValue;
    [SerializeField] private TMP_Dropdown factionDropdown;

    [Header("Actions")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button backButton;

    private void Awake()
    {
        PopulateOptions();
        islandCountSlider.onValueChanged.AddListener(OnIslandCountChanged);
        continueButton.onClick.AddListener(Continue);
        backButton.onClick.AddListener(Back);
        OnIslandCountChanged(islandCountSlider.value);
    }

    private void OnDestroy()
    {
        islandCountSlider?.onValueChanged.RemoveListener(OnIslandCountChanged);
        continueButton?.onClick.RemoveListener(Continue);
        backButton?.onClick.RemoveListener(Back);
    }

    private void PopulateOptions()
    {
        spawnPatternDropdown.ClearOptions();
        spawnPatternDropdown.AddOptions(new List<string>(System.Enum.GetNames(typeof(MapManager.SpawnPattern))));

        factionDropdown.ClearOptions();
        var labels = new List<string>();
        foreach (Faction faction in System.Enum.GetValues(typeof(Faction)))
        {
            if (faction != Faction.None) labels.Add(faction.ToString());
        }
        factionDropdown.AddOptions(labels);
    }

    private void OnIslandCountChanged(float value)
    {
        islandCountValue.text = Mathf.RoundToInt(value).ToString();
    }

    public void Continue()
    {
        string sessionName = string.IsNullOrWhiteSpace(matchNameInput.text)
            ? "New Expedition"
            : matchNameInput.text.Trim();

        var factions = new List<Faction>();
        foreach (Faction faction in System.Enum.GetValues(typeof(Faction)))
        {
            if (faction != Faction.None) factions.Add(faction);
        }

        var config = new MatchConfig
        {
            matchName = sessionName,
            spawnPattern = (MapManager.SpawnPattern)spawnPatternDropdown.value,
            numberOfIslands = Mathf.RoundToInt(islandCountSlider.value),
            startingFactions = new List<Faction> { factions[factionDropdown.value] }
        };

        GameSession.SetPending(config);
        SceneRouter.ToMatch();
    }

    public void Back()
    {
        GameSession.Clear();
        SceneRouter.ToMainMenu();
    }
}

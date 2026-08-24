using System.Collections.Generic;
using UnityEngine;
using static Enums;

public class PlayerFactionController : MonoBehaviour
{
    private Dictionary<Faction, FactionData> factionDict = new Dictionary<Faction, FactionData>();

    [SerializeField] private List<Faction> startingFactions; // Store all the factions player starts with
    
    public GameObject TYC, ECO, SCI;
    private Faction currentDisplayedFaction = Faction.None;

    private void Awake()
    {
        InitializeFactions();
    }

    private void Start()
    {
        ActivateStartingFactions();
    }

    private void InitializeFactions()
    {
        factionDict[Faction.Tyc] = new FactionData { gameObject = TYC, isActive = false };
        factionDict[Faction.Eco] = new FactionData { gameObject = ECO, isActive = false };
        factionDict[Faction.Sci] = new FactionData { gameObject = SCI, isActive = false };
    }

    private void ActivateStartingFactions()
    {
        foreach (var faction in startingFactions)
        {
            JoinFaction(faction);
        }
    }

    /// <summary>
    /// Overrides the factions the player starts with. Must be called before
    /// Start(), which is where ActivateStartingFactions() reads the list -
    /// MatchBootstrapper does this from Awake().
    ///
    /// Faction.None is filtered out: it has no entry in factionDict, so joining
    /// it would throw. A lobby dropdown can easily produce it.
    /// </summary>
    public void SetStartingFactions(List<Faction> factions)
    {
        if (factions == null)
        {
            return;
        }

        var filtered = factions.FindAll(f => f != Faction.None);
        if (filtered.Count == 0)
        {
            Debug.LogWarning("PlayerFactionController: SetStartingFactions got no " +
                             "usable factions - keeping the Inspector's list.");
            return;
        }

        startingFactions = filtered;
    }

    public void JoinFaction(Faction faction)
    {
        if (!factionDict.TryGetValue(faction, out var data))
        {
            Debug.LogError($"PlayerFactionController: no entry for faction '{faction}' - ignoring.");
            return;
        }

        data.isActive = true;
        UpdateFactionDisplay();
    }

    private void UpdateFactionDisplay()
    {
        foreach (var pair in factionDict)
        {
            if (pair.Value.gameObject == null)
            {
                Debug.LogError($"PlayerFactionController: faction '{pair.Key}' has no " +
                               "GameObject assigned in the Inspector - skipping.");
                continue;
            }

            pair.Value.gameObject.SetActive(pair.Value.isActive);
            if (pair.Value.isActive && currentDisplayedFaction == Faction.None) // Only set if there's no current displayed faction
            {
                currentDisplayedFaction = pair.Key;
            }
        }
    }

    public Faction GetCurrentFactionDisplay()
    {
        return currentDisplayedFaction;
    }

    public bool IsFactionActive(Faction faction)
    {
        return factionDict[faction].isActive;
    }

    private class FactionData
    {
        public GameObject gameObject;
        public bool isActive;
    }
}

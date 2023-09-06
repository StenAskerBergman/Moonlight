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

    public void JoinFaction(Faction faction)
    {
        factionDict[faction].isActive = true;
        UpdateFactionDisplay();
    }

    private void UpdateFactionDisplay()
    {
        foreach (var pair in factionDict)
        {
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

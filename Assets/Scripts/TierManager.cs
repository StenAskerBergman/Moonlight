using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TierManager : MonoBehaviour
{
    [System.Serializable]
    public class TierObject
    {
        public GameObject gameObject;
        public string Description;
        public List<ProgressionRequirement> progressionRequirements;
    }

    [System.Serializable]
    public class ProgressionRequirement
    {
        public ProgressionType progressionType;
        public enum CurrentTier 
        { 
            Tier0,

            Tier1Tycoon,
            Tier2Tycoon,
            Tier3Tycoon,

            Tier1Ecology,
            Tier2Ecology,
            Tier3Ecology,
        };
        public int requiredProgression;
    }
 

    public enum ProgressionType
    {
        Faction1,
        Faction2,
        Faction3
        // Add more progression types as needed
    }

    public List<TierObject> tierObjects; // List of objects with required progression values
    
    private Dictionary<ProgressionType, int> playerProgression = new Dictionary<ProgressionType, int>();

    void Start()
    {
        // Initialize player's progression for all types
        foreach (ProgressionType type in System.Enum.GetValues(typeof(ProgressionType)))
        {
            playerProgression[type] = -1;
        }
        UpdateTiers();
    }

    public void IncreaseProgression(ProgressionType type, int amount)
    {
        Debug.Log("Faction: " + playerProgression[type] + " - Amount: " + amount);
        playerProgression[type] += amount;
        UpdateTiers();
    }

    // Devs: Button Functions for Game Testing
    public void TycoonFactionProgression(int amount)
    {
        IncreaseProgression(ProgressionType.Faction1, amount);
    }
    public void EcologyFactionProgression(int amount)
    {
        IncreaseProgression(ProgressionType.Faction2, amount);
    }
    public void ScienceFactionProgression(int amount)
    {
        IncreaseProgression(ProgressionType.Faction3, amount);
    }

    void UpdateTiers()
    {
        foreach (TierObject tierObject in tierObjects)
        {
            bool unlocked = false;
            foreach (ProgressionRequirement requirement in tierObject.progressionRequirements)
            {
                if (playerProgression[requirement.progressionType] >= requirement.requiredProgression)
                {
                    unlocked = true;
                    break;
                }
            }

            tierObject.gameObject.SetActive(unlocked);

            // Enable or disable interaction based on whether the tier is unlocked
            Collider _ObjectCollider = tierObject.gameObject.GetComponent<Collider>();
            if (_ObjectCollider != null)
            {
                _ObjectCollider.enabled = unlocked;
            }

            Graphic _UIElement = tierObject.gameObject.GetComponent<Graphic>();
            if (_UIElement != null)
            {
                _UIElement.raycastTarget = unlocked;
            }
        }
    }
}

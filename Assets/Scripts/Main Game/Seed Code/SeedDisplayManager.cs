// Start - SeedDisplayManager.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SeedDisplayManager : MonoBehaviour
{
    // In SeedDisplayManager.cs
    public SeedManager seedManager;

    [Header("Current Seeds")]
    public Island currentIsland;
    public List<SeedSlot> seedSlots;
    public Sprite emptySeed;

    private void Awake()
    {
        seedManager = FindObjectOfType<SeedManager>();
        if (seedManager == null)
        {
            Debug.LogError("No SeedManager found in scene.");
            return;
        }
    }

    private void Start()
    {
        // Subscribe to events related to island switching
        IslandManager.instance.OnPlayerHoverIsland += OnCurrentIslandChanged;
        IslandManager.instance.OnPlayerEnterIsland += OnCurrentIslandChanged;
    }

    private void OnDestroy()
    {
        // Unsubscribe from events on script destruction
        IslandManager.instance.OnPlayerHoverIsland -= OnCurrentIslandChanged;
        IslandManager.instance.OnPlayerEnterIsland -= OnCurrentIslandChanged;
    }

    private void OnCurrentIslandChanged(Island island)
    {
        if (island != null)
        {
            currentIsland = island;
            UpdateSeedDisplay();
        }
        else
        {
            Debug.Log("Island = Null");
        }
    }

    public void UpdateSeedDisplay()
    {
        if (currentIsland == null)
        {
            Debug.LogError("Failed to update seed UI. Current island is null.");
            return;
        }

        if (seedManager == null || seedSlots == null)
        {
            Debug.LogError("SeedManager or SeedSlots is null.");
            return;
        }

        List<ItemData> seedsOnIsland = seedManager.GetSeedsOnIsland(currentIsland);

        if (seedsOnIsland == null)
        {
            Debug.LogError("No seeds data returned for the current island.");
            return;
        }
        else
        {
            // Will Log 0 Because you don't look at a island when you start...
            // Debug.Log("Seeds count: " + seedsOnIsland.Count);  // Log the count of seeds
        }

        for (int i = 0; i < seedSlots.Count; i++)
        {
            if (i < seedsOnIsland.Count) // <- Null Ref Here
            {
                seedSlots[i].slotIndex = i;  // Set the slot index
                SetSeedDataOnSlot(seedSlots[i].gameObject, seedsOnIsland[i]);  // Set the seed data on each slot
            }
            else
            {
                seedSlots[i].ClearSeedData();  // Clear the seed data if no seed is associated
            }
        }
    }
    public bool ShouldShowDefaultImage(int slotIndex, int seedCount)
    {
        return slotIndex == seedCount;  // Show default image only in the first empty slot
    }

    public void SetSeedDataOnSlot(GameObject slot, ItemData seedData)
    {
        SeedSlot seedSlot = slot.GetComponent<SeedSlot>();
        if (seedSlot != null)
        {
            seedSlot.SetSeedData(seedData);
        }
        else
        {
            Debug.LogError("No SeedSlot component found on seed slot.");
        }
    }

}

// End - SeedDisplayManager.cs

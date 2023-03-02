using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IslandListener : MonoBehaviour
{
    private Island currentIsland;

    private void Start()
    {
        // Subscribe to the event in the IslandManager that detects when the player is looking at an island
        IslandManager.instance.OnPlayerEnterIsland += OnPlayerEnterIsland;
    }

    private void OnDestroy()
    {
        // Unsubscribe from the event to prevent memory leaks
        IslandManager.instance.OnPlayerEnterIsland -= OnPlayerEnterIsland;
    }

    private void OnPlayerEnterIsland(Island island)
    {
        // Store a reference to the current island
        currentIsland = island;
            

        // Update any relevant UI or gameplay elements based on the current island
        Debug.Log("Player entered island: " + currentIsland.name);
        Debug.Log("Player entered island: " + island.name);
    }
}

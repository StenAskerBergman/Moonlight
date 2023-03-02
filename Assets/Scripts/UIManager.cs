using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public Text resource1CountText;
    public Text resource2CountText;
    public Text resource3CountText;
    private PlayerResourceManager playerResourceManager;
    private IslandResourceManager islandResourceManager;

    private void Start()
    {
        // Subscribe to event for the current island.
    }

    private void OnDestroy()
    {
        // Unsubscribes on Destruction
    }

    private void OnCurrentIslandChanged(Island island)
    {
        // Unsubscribe from the OnResourceCountChanged event for the previous island.
        if (islandResourceManager != null)
        {
            islandResourceManager.OnResourceCountChanged -= OnResourceCountChanged;
        }

        // Get the IslandResourceManager for the current island
        islandResourceManager = GameManager.instance.GetIslandResourceManager(island.id);
        if (islandResourceManager == null)
        {
            Debug.LogError("UIManager: Could not get IslandResourceManager for current island.");
            return;
        }

        // Subscribe to the OnResourceCountChanged event for the current island.
        islandResourceManager.OnResourceCountChanged += OnResourceCountChanged;

        UpdateResourceText(Enums.Resource.Resource1, resource1CountText, playerResourceManager, islandResourceManager);
        UpdateResourceText(Enums.Resource.Resource2, resource2CountText, playerResourceManager, islandResourceManager);
        UpdateResourceText(Enums.Resource.Resource3, resource3CountText, playerResourceManager, islandResourceManager);
    }

    private void OnResourceCountChanged(Enums.Resource resource, int count)
    {
        if (resource == Enums.Resource.Resource1)
        {
            resource1CountText.text = count.ToString();
        }
        else if (resource == Enums.Resource.Resource2)
        {
            resource2CountText.text = count.ToString();
        }
        else if (resource == Enums.Resource.Resource3)
        {
            resource3CountText.text = count.ToString();
        }
    }

    private void UpdateResourceText(Enums.Resource resource, Text textObj, PlayerResourceManager playerResourceManager, IslandResourceManager islandResourceManager)
    {
        int count = playerResourceManager.GetResourceCount(islandResourceManager.island.id, resource, 0);
        textObj.text = $"{resource.ToString()}: {count}";
    }
}

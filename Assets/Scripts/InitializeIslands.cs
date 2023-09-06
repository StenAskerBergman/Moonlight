using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Initialize Resources on the Storage Component
    // When Building on a New Island 
    // When Starting a New game on a island
    
public class InitializeIslands : MonoBehaviour
{
    public int resource1 = 50;
    public int resource2 = 50;
    public int resource3 = 50;

    private IslandStorage islandStorage;

    private void Start()
    {
        islandStorage = GetComponent<IslandStorage>();

        if (islandStorage != null)
        {
            islandStorage.AddResource(ItemEnums.ResourceType.Resource1, resource1);
            islandStorage.AddResource(ItemEnums.ResourceType.Resource2, resource2);
            islandStorage.AddResource(ItemEnums.ResourceType.Resource3, resource3);
        }
        else
        {
            Debug.LogError("IslandInitializer: Could not find IslandStorage component");
        }
    }
}

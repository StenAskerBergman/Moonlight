using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IslandStorageManager : StorageManager
{
    public IslandStorage islandStorage { get; private set; } // Making it publicly readable but only settable within the class.

    public IslandStorageManager(Storage storage) : base(storage)
    {
        this.islandStorage = islandStorage;
    }

    // Unity Constructor
    private void Awake()
    {
        islandStorage = GetComponent<IslandStorage>();
        if (!islandStorage)
        {
            islandStorage = gameObject.AddComponent<IslandStorage>();
        }
        if (!storage)
        {
            storage = islandStorage;
        }
    }
    // Override or add new methods to add island-specific behavior.
}


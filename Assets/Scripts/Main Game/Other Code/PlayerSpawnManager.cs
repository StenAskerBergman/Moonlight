using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Handles spawning the player's starter flagship when beginning on an unsettled map.
/// Subscribes to MapManager.OnMapGenerated to safely position the ship in navigable water
/// near the first island and provision it with starting colonization cargo.
/// </summary>
public class PlayerSpawnManager : MonoBehaviour
{
    public static PlayerSpawnManager Instance { get; private set; }

    [Header("Starter Ship Settings")]
    [Tooltip("The starter ship prefab to instantiate. If unassigned, defaults to Commandship.")]
    [SerializeField] private GameObject starterShipPrefab;

    [Tooltip("Distance in world units offshore from the first island's beach to spawn the ship.")]
    [SerializeField] private float offshoreDistance = 25f;

    [Tooltip("Height level for water surface.")]
    [SerializeField] private float waterLevel = 0f;

    [Header("Starting Cargo")]
    [Tooltip("If disabled/false, the starter ship will spawn with an empty inventory (no start resources).")]
    [SerializeField] private bool grantStartingResources = true;
    public bool GrantStartingResources { get => grantStartingResources; set => grantStartingResources = value; }

    [SerializeField] private ItemData buildingModulesItem;
    [SerializeField] private int buildingModulesAmount = 40;

    [SerializeField] private ItemData toolsItem;
    [SerializeField] private int toolsAmount = 40;

    [SerializeField] private ItemData fishItem;
    [SerializeField] private int fishAmount = 40;

    [Header("Behavior")]
    [Tooltip("Auto-select the starter ship upon spawning so player is immediately in control.")]
    [SerializeField] private bool autoSelectOnSpawn = true;

    [Tooltip("Focus the main CameraRig onto the spawned ship.")]
    [SerializeField] private bool focusCameraOnSpawn = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;

        LoadDefaultAssetsIfNull();
    }

    private void OnEnable()
    {
        RuntimeNavMeshBaker.OnNavMeshBaked += HandleNavMeshReady;
    }

    private void OnDisable()
    {
        RuntimeNavMeshBaker.OnNavMeshBaked -= HandleNavMeshReady;
    }

    private void LoadDefaultAssetsIfNull()
    {
#if UNITY_EDITOR
        if (starterShipPrefab == null)
        {
            starterShipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Units Prefabs/Characters/Ships/Commandship.prefab"
            );
        }

        if (buildingModulesItem == null)
        {
            buildingModulesItem = AssetDatabase.LoadAssetAtPath<ItemData>(
                "Assets/Prefabs/Item Prefabs/Materials/Refined/Building Modules.asset"
            );
        }

        if (toolsItem == null)
        {
            toolsItem = AssetDatabase.LoadAssetAtPath<ItemData>(
                "Assets/Prefabs/Item Prefabs/Materials/Refined/Tools.asset"
            );
        }

        if (fishItem == null)
        {
            fishItem = AssetDatabase.LoadAssetAtPath<ItemData>(
                "Assets/Prefabs/Item Prefabs/Goods/Food/Fish.asset"
            );
        }
#endif
    }

    private void HandleNavMeshReady()
    {
        if (GameSession.Active != null)
        {
            grantStartingResources = GameSession.Active.startWithResources;
        }

        // Check if there is already an established player base / warehouse on any island
        InfluenceManager[] influenceManagers = FindObjectsOfType<InfluenceManager>();
        bool hasBase = influenceManagers != null && influenceManagers.Any(m => m != null && m.HasWarehouse);

        // Check if a player ship already exists in the scene
        bool hasExistingShip = UnitSelections.Instance != null &&
            ((UnitSelections.Instance.unitList != null && UnitSelections.Instance.unitList.Any(u => u != null && InfluenceManager.IsBoatUnit(u))) ||
             (UnitSelections.Instance.unitsSelected != null && UnitSelections.Instance.unitsSelected.Any(u => u != null && InfluenceManager.IsBoatUnit(u))));

        if (!hasBase && !hasExistingShip)
        {
            SpawnStarterShip();
        }
    }

    public GameObject SpawnStarterShip()
    {
        LoadDefaultAssetsIfNull();

        Vector3 spawnPosition = CalculateSafeWaterSpawnPosition();
        Quaternion spawnRotation = CalculateSpawnRotation(spawnPosition);

        GameObject shipObj = null;

        if (starterShipPrefab != null)
        {
            shipObj = Instantiate(starterShipPrefab, spawnPosition, spawnRotation);
        }
        else if (UnitService.Instance != null)
        {
            Unit createdUnit = UnitService.Instance.RequestUnit(MoveType.Watercraft, spawnPosition);
            if (createdUnit != null)
            {
                shipObj = createdUnit.gameObject;
                shipObj.transform.rotation = spawnRotation;
            }
        }

        if (shipObj == null)
        {
            Debug.LogError("PlayerSpawnManager: Failed to spawn starter ship. No valid prefab or UnitService available.");
            return null;
        }

        shipObj.name = "Starter Flagship";

        // Provision starting cargo
        ProvisionStartingCargo(shipObj);

        // Register and select
        Unit unitComponent = shipObj.GetComponent<Unit>();
        if (unitComponent != null)
        {
            unitComponent.moveType = MoveType.Watercraft;
            unitComponent.SetDisplayName("Flagship");

            if (UnitSelections.Instance != null)
            {
                if (!UnitSelections.Instance.unitList.Contains(unitComponent))
                {
                    UnitSelections.Instance.unitList.Add(unitComponent);
                }

                if (autoSelectOnSpawn)
                {
                    UnitSelections.Instance.ClickSelect(unitComponent);
                }
            }
        }

        // Camera focus
        if (focusCameraOnSpawn)
        {
            FocusCamera(spawnPosition);
        }

        Debug.Log($"<color=cyan>PlayerSpawnManager: Successfully spawned Starter Flagship named {shipObj.name} at {spawnPosition} with 40 Modules, 40 Tools, and 40 Fish.</color>");
        return shipObj;
    }

    private Vector3 CalculateSafeWaterSpawnPosition()
    {
        Vector3 rawPos = GetRawSpawnPosition();
        
        // Snap to nearest NavMesh position so the Agent doesn't fail to initialize
        if (UnityEngine.AI.NavMesh.SamplePosition(rawPos, out UnityEngine.AI.NavMeshHit hit, 150f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return hit.position;
        }
        
        return rawPos;
    }

    private Vector3 GetRawSpawnPosition()
    {
        Island targetIsland = null;

        if (MapManager.instance != null && MapManager.instance.islands != null && MapManager.instance.islands.Count > 0)
        {
            targetIsland = MapManager.instance.islands.FirstOrDefault(i => i != null);
        }

        if (targetIsland == null)
        {
            targetIsland = FindObjectOfType<Island>();
        }

        if (targetIsland != null)
        {
            Vector3 islandCenter = targetIsland.transform.position;
            MapGrid mapGrid = targetIsland.GetComponent<MapGrid>();

            if (mapGrid != null && mapGrid.Grid != null)
            {
                Cell[,] grid = mapGrid.Grid;
                int size = mapGrid.Size;

                // Find a beach cell to offset from
                for (int x = 0; x < size; x++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        Cell cell = grid[x, z];
                        if (cell != null && cell.currentTerrainType == Cell.TerrainType.Beach)
                        {
                            Vector3 beachWorldPos = targetIsland.transform.position + new Vector3(x, 0f, z);
                            Vector3 outwardDir = (beachWorldPos - islandCenter);
                            outwardDir.y = 0f;

                            if (outwardDir.sqrMagnitude > 0.01f)
                            {
                                outwardDir.Normalize();
                                Vector3 spawnPos = beachWorldPos + outwardDir * offshoreDistance;
                                spawnPos.y = waterLevel;
                                return spawnPos;
                            }
                        }
                    }
                }
            }

            // Fallback: simple radial offset from island center
            Vector3 fallback = islandCenter + new Vector3(offshoreDistance + 20f, 0f, 0f);
            fallback.y = waterLevel;
            return fallback;
        }

        // Default global water coordinate
        return new Vector3(0f, waterLevel, -30f);
    }

    private Quaternion CalculateSpawnRotation(Vector3 spawnPosition)
    {
        Island targetIsland = null;
        if (MapManager.instance != null && MapManager.instance.islands != null && MapManager.instance.islands.Count > 0)
        {
            targetIsland = MapManager.instance.islands.FirstOrDefault(i => i != null);
        }
        if (targetIsland == null) targetIsland = FindObjectOfType<Island>();

        if (targetIsland != null)
        {
            Vector3 toIsland = (targetIsland.transform.position - spawnPosition);
            toIsland.y = 0f;
            if (toIsland.sqrMagnitude > 0.01f)
            {
                return Quaternion.LookRotation(toIsland.normalized, Vector3.up);
            }
        }

        return Quaternion.identity;
    }

    private void ProvisionStartingCargo(GameObject shipObj)
    {
        if (!grantStartingResources)
        {
            Debug.Log("<color=yellow>PlayerSpawnManager: grantStartingResources is false. Spawning starter ship with no initial resources.</color>");
            return;
        }

        UnitInventory unitInventory = shipObj.GetComponent<UnitInventory>();
        if (unitInventory == null)
        {
            unitInventory = shipObj.AddComponent<UnitInventory>();
        }

        if (buildingModulesItem != null && buildingModulesAmount > 0)
        {
            unitInventory.AddItem(buildingModulesItem, buildingModulesAmount, "PlayerSpawnManager");
        }

        if (toolsItem != null && toolsAmount > 0)
        {
            unitInventory.AddItem(toolsItem, toolsAmount, "PlayerSpawnManager");
        }

        if (fishItem != null && fishAmount > 0)
        {
            unitInventory.AddItem(fishItem, fishAmount, "PlayerSpawnManager");
        }
    }

    private void FocusCamera(Vector3 targetPosition)
    {
        CameraRig cameraRig = FindObjectOfType<CameraRig>();
        if (cameraRig != null)
        {
            cameraRig.newPosition = new Vector3(targetPosition.x, cameraRig.newPosition.y, targetPosition.z - 15f);
        }
    }
}

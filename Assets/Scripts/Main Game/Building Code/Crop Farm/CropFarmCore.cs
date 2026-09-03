using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoritative production building for a modular crop farm.
/// Owns the production timer, output inventory, workforce requirement, fertility requirement,
/// required & current field counts, productivity calculation, field ownership, and optional agricultural upgrades.
/// Drives centralized production simulation for all attached field capacity modules.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Building))]
public class CropFarmCore : MonoBehaviour
{
    [Header("Farm Configuration")]
    [SerializeField] private CropFarmData farmData;

    [Header("Runtime Simulation State")]
    [SerializeField, Range(0f, 1f)] private float currentCycleProgress = 0f;
    [SerializeField] private float effectiveProductivity = 0f;
    [SerializeField] private float fieldProductivity = 0f;
    [SerializeField] private int currentWorkforce = 10;

    [Header("Grid & Footprint")]
    [Tooltip("Footprint size of the Farm Core building in grid cells (e.g. (3, 3) for a 3x3 building).")]
    [SerializeField] private Vector2Int coreFootprintSize = new Vector2Int(3, 3);

    // Dependencies
    private Building _building;
    private BuildingSimulation _simulation;
    private BuildingOutput _buildingOutput;
    private BuildingProperties _properties;
    private GridSystem _gridSystem;

    // Field registry and topology
    private readonly HashSet<CropFieldModule> _allOwnedFields = new HashSet<CropFieldModule>();
    private readonly HashSet<CropFieldModule> _connectedFields = new HashSet<CropFieldModule>();
    private readonly Dictionary<Vector2Int, CropFieldModule> _coordToFieldMap = new Dictionary<Vector2Int, CropFieldModule>();
    private readonly HashSet<Vector2Int> _coreFootprintCoords = new HashSet<Vector2Int>();

    // Agricultural Upgrades
    private readonly List<AgriculturalModule> _agriculturalModules = new List<AgriculturalModule>();

    // Upgrade aggregate modifiers
    private float _upgradeProductivityMultiplier = 1.0f;
    private float _upgradeProductivityFlat = 0.0f;
    private float _upgradeCycleTimeMultiplier = 1.0f;
    private int _upgradeExtraOutput = 0;
    private int _upgradeExtraStorage = 0;
    private int _upgradeWorkforceReduction = 0;
    private int _upgradeVirtualFieldBonus = 0;

    // Public properties
    public CropFarmData FarmData => farmData;
    public float CurrentCycleProgress => currentCycleProgress;
    public float EffectiveProductivity => effectiveProductivity;
    public float FieldProductivity => fieldProductivity;

    public int RequiredFieldCount => farmData != null ? farmData.requiredFieldCount : 1;
    public int CurrentConnectedFieldCount => _connectedFields.Count;
    public int TotalOwnedFieldCount => _allOwnedFields.Count;
    public int EffectiveWorkforceRequired => farmData != null ? Mathf.Max(0, farmData.workforceRequired - _upgradeWorkforceReduction) : 0;
    public int CurrentWorkforce
    {
        get => currentWorkforce;
        set
        {
            currentWorkforce = Mathf.Max(0, value);
            RecalculateProductivity();
        }
    }

    public CropFertilityType RequiredFertility => farmData != null ? farmData.requiredFertility : CropFertilityType.None;
    public bool HasRequiredFertility => IslandFertility.CheckFertility(transform, RequiredFertility);
    public IReadOnlyCollection<CropFieldModule> AllOwnedFields => _allOwnedFields;
    public IReadOnlyCollection<CropFieldModule> ConnectedFields => _connectedFields;
    public IReadOnlyCollection<Vector2Int> CoreFootprintCoords => _coreFootprintCoords;

    // Events
    public static event Action<CropFarmCore, ItemEnums.ResourceType, int> OnAnyHarvestCompleted;
    public event Action<CropFarmCore, float> OnProductivityChanged;
    public event Action<CropFarmCore, float> OnCycleProgressChanged;
    public event Action<CropFarmCore, ItemEnums.ResourceType, int> OnHarvestCompleted;
    public event Action<CropFarmCore, int, int> OnFieldCountChanged; // (connected, total)

    private void Awake()
    {
        _building = GetComponent<Building>();
        _simulation = GetComponent<BuildingSimulation>();
        _buildingOutput = GetComponent<BuildingOutput>();
        if (_buildingOutput == null)
        {
            _buildingOutput = gameObject.AddComponent<BuildingOutput>();
        }
        if (farmData != null)
        {
            _buildingOutput.RegisterItemDefinition(farmData.producedResource, farmData.producedItemData);
        }
        _properties = GetComponent<BuildingProperties>();

        if (farmData != null)
        {
            currentWorkforce = farmData.workforceRequired;
        }
    }

    private void Start()
    {
        ResolveGridAndFootprint();
        RecomputeAgriculturalModifiers();
        RecomputeFieldConnectivity();
    }

    private void OnEnable()
    {
        if (_simulation != null)
        {
            _simulation.OnStateChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        if (_simulation != null)
        {
            _simulation.OnStateChanged -= HandleStateChanged;
        }
    }

    private void HandleStateChanged(BuildingEnums.BuildingState state)
    {
        RecalculateProductivity();
    }

    /// <summary>
    /// Computes the grid coordinates occupied by the Farm Core building itself.
    /// </summary>
    public void ResolveGridAndFootprint()
    {
        if (_gridSystem == null)
        {
            _gridSystem = GetComponentInParent<GridSystem>();
            if (_gridSystem == null && IslandManager.instance != null)
            {
                _gridSystem = IslandManager.instance.GetCurrentGridSystem();
            }
        }

        _coreFootprintCoords.Clear();

        Vector2Int baseSize = coreFootprintSize;
        if (_properties != null && _properties.buildingSize.x > 0 && _properties.buildingSize.z > 0)
        {
            baseSize = new Vector2Int(Mathf.RoundToInt(_properties.buildingSize.x), Mathf.RoundToInt(_properties.buildingSize.z));
        }

        if (_gridSystem != null)
        {
            Vector3Int origin = _gridSystem.WorldToCell(transform.position);
            for (int x = 0; x < baseSize.x; x++)
            {
                for (int z = 0; z < baseSize.y; z++)
                {
                    _coreFootprintCoords.Add(new Vector2Int(origin.x + x, origin.z + z));
                }
            }
        }
        else
        {
            // Fallback grid-local footprint around 0,0
            int originX = Mathf.RoundToInt(transform.position.x);
            int originZ = Mathf.RoundToInt(transform.position.z);
            for (int x = 0; x < baseSize.x; x++)
            {
                for (int z = 0; z < baseSize.y; z++)
                {
                    _coreFootprintCoords.Add(new Vector2Int(originX + x, originZ + z));
                }
            }
        }
    }

    private void Update()
    {
        bool isOperational = IsOperationalBuilding();

        if (!isOperational)
        {
            return;
        }

        // 1. Check Fertility
        if (!HasRequiredFertility)
        {
            if (_simulation != null)
            {
                _simulation.SetShutdownReason(BuildingEnums.BuildingShutdownReason.MissingInput);
            }
            return;
        }

        // 2. Check Workforce
        if (EffectiveWorkforceRequired > 0 && currentWorkforce <= 0)
        {
            if (_simulation != null)
            {
                _simulation.SetShutdownReason(BuildingEnums.BuildingShutdownReason.MissingWorkers);
            }
            return;
        }

        // 3. Check Storage
        if (_buildingOutput != null && _buildingOutput.IsFull)
        {
            if (_simulation != null)
            {
                _simulation.SetShutdownReason(BuildingEnums.BuildingShutdownReason.StorageFull);
            }
            return;
        }

        // Clear shutdown reason if operational
        if (_simulation != null && _simulation.CurrentShutdownReason != BuildingEnums.BuildingShutdownReason.None)
        {
            _simulation.SetShutdownReason(BuildingEnums.BuildingShutdownReason.None);
        }

        // 4. Advance Production Cycle Timer
        if (farmData != null && farmData.baseCycleSeconds > 0f && effectiveProductivity > 0f)
        {
            float cycleTime = farmData.baseCycleSeconds * _upgradeCycleTimeMultiplier;
            cycleTime = Mathf.Max(0.1f, cycleTime);

            float progressDelta = (Time.deltaTime * effectiveProductivity) / cycleTime;
            currentCycleProgress += progressDelta;
            OnCycleProgressChanged?.Invoke(this, currentCycleProgress);

            if (currentCycleProgress >= 1f)
            {
                ExecuteHarvest();
                currentCycleProgress = Mathf.Repeat(currentCycleProgress, 1f);
                OnCycleProgressChanged?.Invoke(this, currentCycleProgress);
            }
        }
    }

    private bool IsOperationalBuilding()
    {
        if (_simulation != null)
        {
            return _simulation.CurrentState == BuildingEnums.BuildingState.Active;
        }
        if (_building != null)
        {
            return _building.CurrentState == BuildingEnums.BuildingState.Active;
        }
        return true;
    }

    private void ExecuteHarvest()
    {
        if (farmData == null) return;

        int outputAmount = Mathf.Max(1, farmData.baseOutputAmount + _upgradeExtraOutput);
        ItemEnums.ResourceType resource = farmData.producedResource;

        if (_buildingOutput != null)
        {
            _buildingOutput.AddOutput(resource, outputAmount);
        }

        OnHarvestCompleted?.Invoke(this, resource, outputAmount);
        OnAnyHarvestCompleted?.Invoke(this, resource, outputAmount);
    }

    #region Field Management & Graph Topology

    /// <summary>
    /// Registers a field module under this Farm Core's ownership.
    /// </summary>
    public void RegisterField(CropFieldModule field, Vector2Int coords)
    {
        if (field == null) return;

        _allOwnedFields.Add(field);
        _coordToFieldMap[coords] = field;

        RecomputeFieldConnectivity();
    }

    /// <summary>
    /// Unregisters and removes a field module from this Farm Core's ownership.
    /// </summary>
    public void UnregisterField(CropFieldModule field, Vector2Int coords)
    {
        if (field == null) return;

        _allOwnedFields.Remove(field);
        _coordToFieldMap.Remove(coords);

        RecomputeFieldConnectivity();
    }

    /// <summary>
    /// Runs a BFS graph traversal starting from the Farm Core footprint to determine
    /// every field module that is connected back to the farm core.
    /// Disconnected fields no longer contribute to production until reconnected.
    /// </summary>
    public void RecomputeFieldConnectivity()
    {
        _connectedFields.Clear();

        if (_coreFootprintCoords.Count == 0)
        {
            ResolveGridAndFootprint();
        }

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        // Seed BFS with all fields adjacent to the Farm Core footprint
        Vector2Int[] orthogonalDirections = new Vector2Int[]
        {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0)
        };

        foreach (var coreCoord in _coreFootprintCoords)
        {
            foreach (var dir in orthogonalDirections)
            {
                Vector2Int neighborCoord = coreCoord + dir;
                if (_coordToFieldMap.TryGetValue(neighborCoord, out CropFieldModule adjacentField))
                {
                    if (visited.Add(neighborCoord))
                    {
                        queue.Enqueue(neighborCoord);
                    }
                }
            }
        }

        // BFS flood-fill through connected field neighbors
        while (queue.Count > 0)
        {
            Vector2Int currentCoord = queue.Dequeue();

            if (_coordToFieldMap.TryGetValue(currentCoord, out CropFieldModule field))
            {
                _connectedFields.Add(field);

                foreach (var dir in orthogonalDirections)
                {
                    Vector2Int nextCoord = currentCoord + dir;
                    if (_coordToFieldMap.ContainsKey(nextCoord) && visited.Add(nextCoord))
                    {
                        queue.Enqueue(nextCoord);
                    }
                }
            }
        }

        // Update connected state for all owned fields
        foreach (var field in _allOwnedFields)
        {
            if (field != null)
            {
                bool isConnected = _connectedFields.Contains(field);
                field.SetConnectedState(isConnected);
            }
        }

        RecalculateProductivity();
        OnFieldCountChanged?.Invoke(this, _connectedFields.Count, _allOwnedFields.Count);
    }

    /// <summary>
    /// Tests if a candidate grid coordinate would be connected to the Farm Core or an existing connected field.
    /// </summary>
    public bool IsCoordinateAdjacentToConnectedFarm(Vector2Int targetCoord)
    {
        Vector2Int[] orthogonalDirections = new Vector2Int[]
        {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0)
        };

        foreach (var dir in orthogonalDirections)
        {
            Vector2Int neighbor = targetCoord + dir;
            // Directly touches core footprint
            if (_coreFootprintCoords.Contains(neighbor))
            {
                return true;
            }
            // Touches an already connected field
            if (_coordToFieldMap.TryGetValue(neighbor, out CropFieldModule field) && field != null && field.IsConnectedToCore)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Productivity & Modifiers

    /// <summary>
    /// Recalculates field productivity and total effective productivity.
    /// </summary>
    public void RecalculateProductivity()
    {
        int requiredFields = RequiredFieldCount;
        int activeFields = _connectedFields.Count + _upgradeVirtualFieldBonus;

        // Proportional field productivity: 100% at required field count, proportionally less with fewer fields
        fieldProductivity = Mathf.Clamp01((float)activeFields / Mathf.Max(1, requiredFields));

        // Workforce ratio
        float workforceRatio = 1.0f;
        if (EffectiveWorkforceRequired > 0)
        {
            workforceRatio = Mathf.Clamp01((float)currentWorkforce / EffectiveWorkforceRequired);
        }

        // Combined effective productivity
        float baseProd = fieldProductivity * workforceRatio;
        effectiveProductivity = (baseProd * _upgradeProductivityMultiplier) + _upgradeProductivityFlat;

        if (!IsOperationalBuilding() || !HasRequiredFertility)
        {
            effectiveProductivity = 0f;
        }

        effectiveProductivity = Mathf.Max(0f, effectiveProductivity);

        if (_simulation != null)
        {
            _simulation.SetEfficiency(effectiveProductivity);
        }

        OnProductivityChanged?.Invoke(this, effectiveProductivity);
    }

    #endregion

    #region Agricultural Modules / Upgrades

    public void AddAgriculturalModule(AgriculturalModule module)
    {
        if (module == null || _agriculturalModules.Contains(module)) return;

        _agriculturalModules.Add(module);
        RecomputeAgriculturalModifiers();
    }

    public void RemoveAgriculturalModule(AgriculturalModule module)
    {
        if (module == null || !_agriculturalModules.Contains(module)) return;

        _agriculturalModules.Remove(module);
        RecomputeAgriculturalModifiers();
    }

    private void RecomputeAgriculturalModifiers()
    {
        _upgradeProductivityMultiplier = 1.0f;
        _upgradeProductivityFlat = 0.0f;
        _upgradeCycleTimeMultiplier = 1.0f;
        _upgradeExtraOutput = 0;
        _upgradeExtraStorage = 0;
        _upgradeWorkforceReduction = 0;
        _upgradeVirtualFieldBonus = 0;

        foreach (var mod in _agriculturalModules)
        {
            if (mod == null) continue;
            _upgradeProductivityMultiplier *= mod.ProductivityMultiplier;
            _upgradeProductivityFlat += mod.ProductivityFlatBonus;
            _upgradeCycleTimeMultiplier *= mod.CycleTimeMultiplier;
            _upgradeExtraOutput += mod.ExtraOutputAmount;
            _upgradeExtraStorage += mod.ExtraStorageCapacity;
            _upgradeWorkforceReduction += mod.WorkforceReduction;
            _upgradeVirtualFieldBonus += mod.VirtualFieldBonus;
        }

        // Apply extra storage to BuildingOutput
        if (_buildingOutput != null && farmData != null)
        {
            // BuildingOutput has internal outputCapacity
        }

        RecalculateProductivity();
    }

    #endregion

    public void SetFarmData(CropFarmData data)
    {
        this.farmData = data;
        if (data != null)
        {
            currentWorkforce = data.workforceRequired;
        }
        RecomputeFieldConnectivity();
    }
}

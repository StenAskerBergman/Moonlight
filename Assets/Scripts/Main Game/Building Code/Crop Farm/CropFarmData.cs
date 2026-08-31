using System;
using UnityEngine;

/// <summary>
/// ScriptableObject definition for a modular crop farm (e.g. Grain Farm, Plantain Plantation).
/// Defines base cycle timing, required field count, base output, workforce, and required fertility.
/// </summary>
[CreateAssetMenu(fileName = "New Crop Farm Data", menuName = "Data/Building/Crop Farm Data")]
public class CropFarmData : ScriptableObject, IIdentifiable
{
    [Header("Identity")]
    [Tooltip("Namespaced identifier (e.g. 'core:grain_farm', 'core:plantain_plantation').")]
    [SerializeField] private string identifier = "core:crop_farm";

    public Identifier Id => !string.IsNullOrEmpty(identifier)
        ? new Identifier(identifier)
        : new Identifier($"core:{name.ToLowerInvariant().Replace(' ', '_')}");

    [Header("Crop Info")]
    public string cropName = "Grain";
    [TextArea(2, 4)] public string cropDescription = "Standard agricultural crop.";
    public Sprite cropIcon;

    [Header("Fertility Requirement")]
    [Tooltip("Fertility required on the island for this farm to operate and cultivate fields.")]
    public CropFertilityType requiredFertility = CropFertilityType.Grain;

    [Header("Field Requirements & Scaling")]
    [Tooltip("Field count needed for 100% field productivity (e.g. 144 for Grain, 128 for Plantain).")]
    [Min(1)] public int requiredFieldCount = 144;

    [Tooltip("Optional maximum field capacity limit (-1 for uncapped).")]
    public int maxFieldCount = -1;

    [Header("Production Parameters")]
    [Tooltip("Base cycle time in seconds for one harvest at 100% productivity (e.g. 60s for Grain, 30s for Plantain).")]
    [Min(0.1f)] public float baseCycleSeconds = 60f;

    [Tooltip("Base amount produced per completed cycle at 100% productivity (e.g. 1 Grain, 1 Plantain).")]
    [Min(1)] public int baseOutputAmount = 1;

    [Tooltip("Resource produced and deposited into BuildingOutput.")]
    public ItemEnums.ResourceType producedResource = ItemEnums.ResourceType.Grain;

    [Tooltip("Optional GoodType equivalent.")]
    public ItemEnums.GoodType producedGood = ItemEnums.GoodType.Grain;

    [Tooltip("Optional ItemData reference for inventory/trading.")]
    public ItemData producedItemData;

    [Header("Workforce & Capacity")]
    [Tooltip("Number of workers required to run at full speed.")]
    [Min(0)] public int workforceRequired = 10;

    [Tooltip("Output storage capacity in the Farm Core before production stalls.")]
    [Min(1)] public int outputCapacity = 30;

    [Header("Visuals & Field Prefabs")]
    [Tooltip("Optional field module prefab to instantiate for each 1x1 tile.")]
    public GameObject fieldPrefab;

    [Tooltip("Material tint or color for field preview and gizmos.")]
    public Color fieldColor = new Color(0.85f, 0.75f, 0.2f, 1f);
}

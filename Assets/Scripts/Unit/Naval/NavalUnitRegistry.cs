using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Registry service and editor asset factory for Moonlight's naval unit roster.
/// Discovers and registers all 16 naval vessel definitions into GameRegistries.Units.
/// </summary>
public static class NavalUnitRegistry
{
    public const string FreightShipId = "moonlight:freight_ship";
    public const string CargoLinerId = "moonlight:cargo_liner";
    public const string ContainerShipId = "moonlight:container_ship";
    public const string OilTankerId = "moonlight:oil_tanker";

    public const string CommandoShipId = "moonlight:commando_ship";
    public const string ViperId = "moonlight:viper";
    public const string HovercraftId = "moonlight:hovercraft";
    public const string ColossusId = "moonlight:colossus";
    public const string SharkId = "moonlight:shark";
    public const string RaiderId = "moonlight:raider";
    public const string AtlasId = "moonlight:atlas";

    public const string T38OceanGliderId = "moonlight:t38_ocean_glider";
    public const string SisyphusId = "moonlight:sisyphus";
    public const string DeepSeaHunterId = "moonlight:deep_sea_hunter";
    public const string OrcaId = "moonlight:orca";
    public const string ErebosId = "moonlight:erebos";

    public static readonly string[] AllNavalIds = new string[]
    {
        FreightShipId,
        CargoLinerId,
        ContainerShipId,
        OilTankerId,
        CommandoShipId,
        ViperId,
        HovercraftId,
        ColossusId,
        SharkId,
        RaiderId,
        AtlasId,
        T38OceanGliderId,
        SisyphusId,
        DeepSeaHunterId,
        OrcaId,
        ErebosId
    };

    private static readonly Dictionary<string, NavalUnitDefinition> definitionsById = new Dictionary<string, NavalUnitDefinition>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void InitializeRegistry()
    {
        definitionsById.Clear();

        // Load all definitions in Resources or Project
        NavalUnitDefinition[] loaded = Resources.LoadAll<NavalUnitDefinition>("");
        foreach (var def in loaded)
        {
            RegisterDefinition(def);
        }

#if UNITY_EDITOR
        if (definitionsById.Count < AllNavalIds.Length)
        {
            string[] guids = AssetDatabase.FindAssets("t:NavalUnitDefinition");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                NavalUnitDefinition def = AssetDatabase.LoadAssetAtPath<NavalUnitDefinition>(path);
                if (def != null)
                {
                    RegisterDefinition(def);
                }
            }
        }
#endif

        Debug.Log($"<color=cyan>[NavalUnitRegistry] Initialized with {definitionsById.Count} naval definitions registered.</color>");
    }

    public static void RegisterDefinition(NavalUnitDefinition def)
    {
        if (def == null) return;
        string idStr = def.Id.FullId;

        definitionsById[idStr] = def;
        GameRegistries.Units.Register(def.Id, def, Registry<UnitDefinition>.DuplicatePolicy.WarnAndOverwrite);
    }

    public static NavalUnitDefinition GetDefinition(string id)
    {
        if (definitionsById.TryGetValue(id, out var def)) return def;
        return null;
    }

    public static IEnumerable<NavalUnitDefinition> AllDefinitions => definitionsById.Values;
}

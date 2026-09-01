#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Editor utility to generate and validate all 16 Anno 2070-inspired naval vessel definitions,
/// abilities, movement profiles, and prefabs in Moonlight.
/// Accessible via menu item Tools/Moonlight/Naval Roster/Generate All Naval Assets.
/// </summary>
public static class NavalRosterAssetGenerator
{
    private const string DataRoot = "Assets/Data/Units/Naval";
    private const string PrefabRoot = "Assets/Prefabs/Units Prefabs/Naval";

    [MenuItem("Tools/Moonlight/Naval Roster/Generate All Naval Assets")]
    public static void GenerateAllAssets()
    {
        Debug.Log("<color=cyan><b>=== Generating Naval Roster Assets ===</b></color>");

        EnsureDirectories();

        // 1. Generate Abilities
        var diveAbility = GetOrCreateAsset<DiveAbilityDefinition>($"{DataRoot}/Abilities/Ability_Dive.asset", def =>
        {
            SetIdentifier(def, "moonlight:ability_dive");
            def.displayName = "Dive";
            def.description = "Submerge to deep water or surface.";
            def.abilityType = AbilityType.Toggle;
            def.cooldown = 8f;
        });

        var empAbility = GetOrCreateAsset<EMPModuleAbilityDefinition>($"{DataRoot}/Abilities/Ability_EMPModule.asset", def =>
        {
            SetIdentifier(def, "moonlight:ability_emp_module");
            def.displayName = "EMP Module";
            def.description = "Discharges an electromagnetic pulse disabling nearby vehicles.";
            def.abilityType = AbilityType.Active;
            def.range = 10f;
            def.duration = 60f;
            def.cooldown = 300f;
            def.targetRestrictions = CombatTargetCapabilities.All;
        });

        var droneAbility = GetOrCreateAsset<AttackDroneAbilityDefinition>($"{DataRoot}/Abilities/Ability_AttackDrone.asset", def =>
        {
            SetIdentifier(def, "moonlight:ability_attack_drone");
            def.displayName = "Attack Drone";
            def.description = "Deploys a temporary autonomous combat drone.";
            def.abilityType = AbilityType.Active;
            def.cooldown = 90f;
            def.duration = 45f;
            def.range = 25f;
            def.targetRestrictions = CombatTargetCapabilities.All;
        });

        var depthChargesAbility = GetOrCreateAsset<DepthChargesAbilityDefinition>($"{DataRoot}/Abilities/Ability_DepthCharges.asset", def =>
        {
            SetIdentifier(def, "moonlight:ability_depth_charges");
            def.displayName = "Depth Charges";
            def.description = "Launches explosive depth charges targeting submerged submarines in the area.";
            def.abilityType = AbilityType.Targeted;
            def.cooldown = 30f;
            def.range = 20f;
            def.damage = 120;
            def.areaOfEffect = 8f;
            def.targetRestrictions = CombatTargetCapabilities.Submarine;
        });

        var fleetRepairAbility = GetOrCreateAsset<FleetRepairAbilityDefinition>($"{DataRoot}/Abilities/Ability_FleetRepair.asset", def =>
        {
            SetIdentifier(def, "moonlight:ability_fleet_repair");
            def.displayName = "Fleet Repair";
            def.description = "Provides continuous maintenance and field repairs to nearby friendly vessels.";
            def.abilityType = AbilityType.Passive;
            def.range = 18f;
            def.repairAmountPerPulse = 10;
        });

        var hangarAbility = GetOrCreateAsset<AircraftHangarAbilityDefinition>($"{DataRoot}/Abilities/Ability_AircraftHangar.asset", def =>
        {
            SetIdentifier(def, "moonlight:ability_aircraft_hangar");
            def.displayName = "Aircraft Hangar";
            def.description = "Flight deck facilities for support aircraft (capacity: 2 light or 1 heavy).";
            def.abilityType = AbilityType.Passive;
            def.lightAircraftCapacity = 2;
            def.heavyAircraftCapacity = 1;
        });

        var silentRunningAbility = GetOrCreateAsset<SilentRunningAbilityDefinition>($"{DataRoot}/Abilities/Ability_SilentRunning.asset", def =>
        {
            SetIdentifier(def, "moonlight:ability_silent_running");
            def.displayName = "Silent Running";
            def.description = "Baffles propulsion noise, significantly reducing enemy detection visibility while submerged.";
            def.abilityType = AbilityType.Passive;
            def.detectionModifierWhenSubmerged = 0.1f;
        });

        var missileAbility = GetOrCreateAsset<MediumRangeMissileAbilityDefinition>($"{DataRoot}/Abilities/Ability_MediumRangeMissile.asset", def =>
        {
            SetIdentifier(def, "moonlight:ability_medium_range_missile");
            def.displayName = "Medium-Range Missile";
            def.description = "Launches a long-range tactical cruise missile against surface targets.";
            def.abilityType = AbilityType.Targeted;
            def.range = 50f;
            def.cooldown = 120f;
            def.missileDamage = 250;
            def.areaOfEffect = 12f;
            def.targetRestrictions = CombatTargetCapabilities.Surface;
        });

        // 2. Generate Movement Profiles
        var shipMovement = GetOrCreateAsset<NavMeshMovementProfile>($"{DataRoot}/Movement/ShipMovementProfile.asset", p =>
        {
            SetField(p, "domain", MoveType.Watercraft);
            SetField(p, "agentTypeID", -1372625422); // Ship
            SetField(p, "areaMask", -1); // All areas
            SetField(p, "travelMedium", LayerMask.GetMask("Water"));
            SetField(p, "speed", 8f);
            SetField(p, "acceleration", 8f);
            SetField(p, "angularSpeed", 120f);
        });

        var subMovement = GetOrCreateAsset<NavMeshMovementProfile>($"{DataRoot}/Movement/SubmarineMovementProfile.asset", p =>
        {
            SetField(p, "domain", MoveType.Submersible);
            SetField(p, "agentTypeID", -334000983); // Submarine
            SetField(p, "areaMask", -1);
            SetField(p, "travelMedium", LayerMask.GetMask("Water"));
            SetField(p, "speed", 8f);
            SetField(p, "acceleration", 8f);
            SetField(p, "angularSpeed", 120f);
        });

        var hoverMovement = GetOrCreateAsset<NavMeshMovementProfile>($"{DataRoot}/Movement/HovercraftMovementProfile.asset", p =>
        {
            SetField(p, "domain", MoveType.Hovercraft);
            SetField(p, "agentTypeID", -1372625422); // Ship
            SetField(p, "areaMask", -1);
            SetField(p, "travelMedium", LayerMask.GetMask("Water"));
            SetField(p, "speed", 12f);
            SetField(p, "acceleration", 15f);
            SetField(p, "angularSpeed", 180f);
        });

        // 3. Generate Prefabs and Definitions for all 16 vessels

        // --- Surface Trade Ships ---
        CreateNavalEntry(
            "Freight Ship", "Trade/Freight Ship", NavalClass.TradeShip, typeof(TradeShip),
            speed: 8f, cargoSlots: 3, maxStack: 40, itemSlots: 1, maxHealth: 350,
            targets: CombatTargetCapabilities.None, submerge: false,
            abilities: new AbilityDefinition[0], moveProfile: shipMovement,
            scale: new Vector3(1f, 1f, 1f), subModelIndex: 0
        );

        CreateNavalEntry(
            "Cargo Liner", "Trade/Cargo Liner", NavalClass.TradeShip, typeof(TradeShip),
            speed: 9f, cargoSlots: 4, maxStack: 50, itemSlots: 1, maxHealth: 450,
            targets: CombatTargetCapabilities.None, submerge: false,
            abilities: new AbilityDefinition[0], moveProfile: shipMovement,
            scale: new Vector3(1.15f, 1.15f, 1.3f), subModelIndex: 0
        );

        CreateNavalEntry(
            "Container Ship", "Trade/Container Ship", NavalClass.TradeShip, typeof(TradeShip),
            speed: 6.5f, cargoSlots: 6, maxStack: 60, itemSlots: 2, maxHealth: 650,
            targets: CombatTargetCapabilities.None, submerge: false,
            abilities: new AbilityDefinition[0], moveProfile: shipMovement,
            scale: new Vector3(1.5f, 1.3f, 1.7f), subModelIndex: 0
        );

        CreateNavalEntry(
            "Oil Tanker", "Trade/Oil Tanker", NavalClass.TradeShip, typeof(TradeShip),
            speed: 6f, cargoSlots: 8, maxStack: 60, itemSlots: 0, maxHealth: 700,
            targets: CombatTargetCapabilities.None, submerge: false,
            abilities: new AbilityDefinition[0], moveProfile: shipMovement,
            scale: new Vector3(1.6f, 1.25f, 1.8f), subModelIndex: 0
        );

        // --- Surface Combat Ships ---
        CreateNavalEntry(
            "Commando Ship", "Combat/Commando Ship", NavalClass.Warship, typeof(Warship),
            speed: 8f, cargoSlots: 3, maxStack: 40, itemSlots: 1, maxHealth: 500,
            targets: CombatTargetCapabilities.Surface | CombatTargetCapabilities.Air, submerge: false,
            abilities: new AbilityDefinition[0], moveProfile: shipMovement,
            scale: new Vector3(1.2f, 1.2f, 1.2f), subModelIndex: 0
        );

        CreateNavalEntry(
            "Viper", "Combat/Viper", NavalClass.Warship, typeof(Warship),
            speed: 10f, cargoSlots: 2, maxStack: 40, itemSlots: 1, maxHealth: 400,
            targets: CombatTargetCapabilities.Surface | CombatTargetCapabilities.Submarine, submerge: false,
            abilities: new AbilityDefinition[] { droneAbility }, moveProfile: shipMovement,
            scale: new Vector3(0.9f, 0.9f, 1.1f), subModelIndex: 0
        );

        CreateNavalEntry(
            "Hovercraft", "Combat/Hovercraft", NavalClass.Warship, typeof(Warship),
            speed: 12f, cargoSlots: 1, maxStack: 20, itemSlots: 2, maxHealth: 300,
            targets: CombatTargetCapabilities.Surface | CombatTargetCapabilities.Air, submerge: false,
            abilities: new AbilityDefinition[0], moveProfile: hoverMovement,
            scale: new Vector3(0.85f, 0.75f, 0.9f), subModelIndex: 0
        );

        CreateNavalEntry(
            "Colossus", "Combat/Colossus", NavalClass.Warship, typeof(Warship),
            speed: 5f, cargoSlots: 1, maxStack: 40, itemSlots: 2, maxHealth: 1200,
            targets: CombatTargetCapabilities.Surface, submerge: false,
            abilities: new AbilityDefinition[] { depthChargesAbility }, moveProfile: shipMovement,
            scale: new Vector3(2f, 1.6f, 2.2f), subModelIndex: 0
        );

        CreateNavalEntry(
            "Shark", "Combat/Shark", NavalClass.Warship, typeof(Warship),
            speed: 9f, cargoSlots: 2, maxStack: 40, itemSlots: 3, maxHealth: 600,
            targets: CombatTargetCapabilities.All, submerge: false,
            abilities: new AbilityDefinition[0], moveProfile: shipMovement,
            scale: new Vector3(1.3f, 1.1f, 1.4f), subModelIndex: 0
        );

        CreateNavalEntry(
            "Raider", "Combat/Raider", NavalClass.Warship, typeof(Warship),
            speed: 10f, cargoSlots: 2, maxStack: 40, itemSlots: 2, maxHealth: 480,
            targets: CombatTargetCapabilities.All, submerge: false,
            abilities: new AbilityDefinition[0], moveProfile: shipMovement,
            scale: new Vector3(1.05f, 1.0f, 1.15f), subModelIndex: 0
        );

        // --- Support ---
        CreateNavalEntry(
            "Atlas", "Support/Atlas", NavalClass.SupportShip, typeof(SupportShip),
            speed: 6f, cargoSlots: 1, maxStack: 80, itemSlots: 0, maxHealth: 1500,
            targets: CombatTargetCapabilities.None, submerge: false,
            abilities: new AbilityDefinition[] { fleetRepairAbility, hangarAbility }, moveProfile: shipMovement,
            scale: new Vector3(2.2f, 1.5f, 2.5f), subModelIndex: 0,
            aircraftCapacity: 2
        );

        // --- Submarines ---
        CreateNavalEntry(
            "T38 Ocean Glider", "Submarines/T38 Ocean Glider", NavalClass.Submarine, typeof(Submarine),
            speed: 11f, cargoSlots: 4, maxStack: 40, itemSlots: 2, maxHealth: 400,
            targets: CombatTargetCapabilities.None, submerge: true,
            abilities: new AbilityDefinition[] { diveAbility, empAbility }, moveProfile: subMovement,
            scale: new Vector3(1f, 1f, 1f), subModelIndex: 1
        );

        CreateNavalEntry(
            "Sisyphus", "Submarines/Sisyphus", NavalClass.Submarine, typeof(Submarine),
            speed: 7.5f, cargoSlots: 6, maxStack: 60, itemSlots: 0, maxHealth: 700,
            targets: CombatTargetCapabilities.None, submerge: true,
            abilities: new AbilityDefinition[] { diveAbility, silentRunningAbility }, moveProfile: subMovement,
            scale: new Vector3(1.5f, 1.2f, 1.6f), subModelIndex: 2
        );

        CreateNavalEntry(
            "Deep Sea Hunter", "Submarines/Deep Sea Hunter", NavalClass.Submarine, typeof(Submarine),
            speed: 8f, cargoSlots: 1, maxStack: 40, itemSlots: 2, maxHealth: 550,
            targets: CombatTargetCapabilities.Surface | CombatTargetCapabilities.Submarine, submerge: true,
            abilities: new AbilityDefinition[] { diveAbility }, moveProfile: subMovement,
            scale: new Vector3(1.1f, 1.1f, 1.2f), subModelIndex: 3
        );

        CreateNavalEntry(
            "Orca", "Submarines/Orca", NavalClass.Submarine, typeof(Submarine),
            speed: 6f, cargoSlots: 1, maxStack: 40, itemSlots: 1, maxHealth: 650,
            targets: CombatTargetCapabilities.Surface | CombatTargetCapabilities.Submarine, submerge: true,
            abilities: new AbilityDefinition[] { diveAbility, missileAbility }, moveProfile: subMovement,
            scale: new Vector3(1.4f, 1.3f, 1.5f), subModelIndex: 4
        );

        CreateNavalEntry(
            "Erebos", "Submarines/Erebos", NavalClass.Submarine, typeof(Submarine),
            speed: 6f, cargoSlots: 1, maxStack: 40, itemSlots: 1, maxHealth: 750,
            targets: CombatTargetCapabilities.Surface | CombatTargetCapabilities.Submarine, submerge: true,
            abilities: new AbilityDefinition[] { diveAbility, missileAbility }, moveProfile: subMovement,
            scale: new Vector3(1.45f, 1.35f, 1.55f), subModelIndex: 5
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Refresh Registry
        NavalUnitRegistry.InitializeRegistry();

        Debug.Log("<color=green><b>=== Naval Roster Asset Generation Complete! ===</b></color>");
    }

    private static void CreateNavalEntry(
        string vesselName,
        string relativePath,
        NavalClass navalClass,
        System.Type navalComponentType,
        float speed,
        int cargoSlots,
        int maxStack,
        int itemSlots,
        int maxHealth,
        CombatTargetCapabilities targets,
        bool submerge,
        AbilityDefinition[] abilities,
        MovementProfile moveProfile,
        Vector3 scale,
        int subModelIndex,
        int aircraftCapacity = 0
    )
    {
        string id = $"moonlight:{vesselName.ToLowerInvariant().Replace(' ', '_').Replace('-', '_')}";
        string defPath = $"{DataRoot}/{relativePath}.asset";
        string prefabPath = $"{PrefabRoot}/{relativePath}.prefab";

        // Create or update definition asset first
        var defAsset = GetOrCreateAsset<NavalUnitDefinition>(defPath, def =>
        {
            SetIdentifier(def, id);
            def.name = vesselName;
            def.displayName = vesselName;
            def.displayCategory = GetCategoryName(vesselName, navalClass);
            def.navalClass = navalClass;
            def.movementSpeed = speed;
            def.cargoSlotCount = cargoSlots;
            def.cargoCapacityPerSlot = maxStack;
            def.equipmentSlotCount = itemSlots;
            def.maxHealth = maxHealth;
            def.attackCapabilities = targets;
            def.canSubmerge = submerge;
            def.canCarryAircraft = aircraftCapacity > 0;
            def.aircraftCapacity = aircraftCapacity;
            def.unitType = UnitType.Character;
            def.movementProfile = moveProfile;
            def.nameType = NameType.Ship;

            def.abilities.Clear();
            if (abilities != null)
            {
                def.abilities.AddRange(abilities);
            }
        });

        // Create or update prefab with reference to definition
        GameObject prefabObj = CreateOrUpdatePrefab(prefabPath, vesselName, navalComponentType, speed, cargoSlots, maxStack, itemSlots, maxHealth, moveProfile, scale, submerge, subModelIndex, defAsset);

        // Assign prefab to defAsset
        defAsset.prefab = prefabObj;
        EditorUtility.SetDirty(defAsset);
        AssetDatabase.SaveAssets();
    }

    private static GameObject CreateOrUpdatePrefab(
        string prefabPath,
        string vesselName,
        System.Type navalComponentType,
        float speed,
        int cargoSlots,
        int maxStack,
        int itemSlots,
        int maxHealth,
        MovementProfile moveProfile,
        Vector3 scale,
        bool submerge,
        int subModelIndex,
        NavalUnitDefinition defAsset
    )
    {
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Units Prefabs/Characters/Ships/Commandship.prefab");
        GameObject instance = sourcePrefab != null ? Object.Instantiate(sourcePrefab) : new GameObject(vesselName);
        instance.name = vesselName;

        // Remove old template ItemSlot child GameObjects
        var oldSlots = instance.GetComponentsInChildren<ItemSlot>(true);
        for (int i = 0; i < oldSlots.Length; i++)
        {
            if (oldSlots[i] != null) Object.DestroyImmediate(oldSlots[i].gameObject);
        }

        // Ensure Root Layer is 9 (Clickable)
        instance.layer = 9;

        // Setup Unit
        Unit unit = instance.GetComponent<Unit>();
        if (unit == null) unit = instance.AddComponent<Unit>();
        unit.definition = defAsset;
        unit.unitType = UnitType.Character;
        unit.moveType = submerge ? MoveType.Submersible : (vesselName == "Hovercraft" ? MoveType.Hovercraft : MoveType.Watercraft);
        unit.SetDisplayName(vesselName);
        unit.Selectable = true;
        unit.Targetable = true;

        // Setup NavMeshAgent
        NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
        if (agent == null) agent = instance.AddComponent<NavMeshAgent>();
        agent.agentTypeID = submerge ? -334000983 : -1372625422;
        agent.speed = speed;
        agent.acceleration = speed >= 10f ? 12f : 8f;
        agent.angularSpeed = speed >= 10f ? 150f : 120f;
        agent.radius = 1f;
        agent.height = 1.5f;

        // Setup UnitMovement
        UnitMovement movement = instance.GetComponent<UnitMovement>();
        if (movement == null) movement = instance.AddComponent<UnitMovement>();
        movement.agent = agent;
        movement.TravelMedium = LayerMask.GetMask("Water");

        // Setup Damageable
        Damageable dmg = instance.GetComponent<Damageable>();
        if (dmg == null) dmg = instance.AddComponent<Damageable>();
        var totalHpField = typeof(Damageable).GetField("totalHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (totalHpField != null) totalHpField.SetValue(dmg, maxHealth);
        dmg.currentHealth = maxHealth;

        // Setup UnitStorageManager
        UnitStorageManager sm = instance.GetComponent<UnitStorageManager>();
        if (sm == null) sm = instance.AddComponent<UnitStorageManager>();
        sm.NormalSlots = cargoSlots;
        sm.MaxStackSize = maxStack;

        // Setup UnitStorage
        UnitStorage storage = instance.GetComponent<UnitStorage>();
        if (storage == null) storage = instance.AddComponent<UnitStorage>();

        // Setup UnitInventory
        UnitInventory inv = instance.GetComponent<UnitInventory>();
        if (inv == null) inv = instance.AddComponent<UnitInventory>();
        inv.itemSlots = null;
        inv.configuredSlotCount = cargoSlots;
        inv.configuredMaxStack = maxStack;
        inv.ConfigureSlots(cargoSlots, maxStack);

        // Setup UnitEquipment
        UnitEquipment equip = instance.GetComponent<UnitEquipment>();
        if (equip == null) equip = instance.AddComponent<UnitEquipment>();
        equip.ConfigureSlots(itemSlots);

        // Setup UnitAbilities
        UnitAbilities abilities = instance.GetComponent<UnitAbilities>();
        if (abilities == null) abilities = instance.AddComponent<UnitAbilities>();
        if (defAsset != null && defAsset.abilities != null)
        {
            abilities.SetAbilities(defAsset.abilities);
        }

        // Setup DiveInteraction for submarines
        if (submerge)
        {
            DiveInteraction dive = instance.GetComponent<DiveInteraction>();
            if (dive == null) dive = instance.AddComponent<DiveInteraction>();
        }
        else
        {
            DiveInteraction dive = instance.GetComponent<DiveInteraction>();
            if (dive != null) Object.DestroyImmediate(dive);
        }

        // Setup NavalUnit specialization component
        NavalUnit existingNaval = instance.GetComponent<NavalUnit>();
        if (existingNaval != null && existingNaval.GetType() != navalComponentType)
        {
            Object.DestroyImmediate(existingNaval);
            existingNaval = null;
        }
        if (existingNaval == null)
        {
            existingNaval = (NavalUnit)instance.AddComponent(navalComponentType);
        }
        SetField(existingNaval, "definition", defAsset);
        if (defAsset != null)
        {
            existingNaval.ApplyDefinition(defAsset);
        }

        // Adjust Visual Model / Mesh if available
        Transform graphicsTrans = instance.transform.Find("Graphics");
        if (graphicsTrans != null)
        {
            graphicsTrans.localScale = scale;
            if (submerge)
            {
                Mesh subMesh = GetSubmarineMesh(subModelIndex);
                if (subMesh != null)
                {
                    MeshFilter[] filters = graphicsTrans.GetComponentsInChildren<MeshFilter>(true);
                    if (filters.Length > 0)
                    {
                        filters[0].sharedMesh = subMesh;
                    }
                }
            }
        }

        // Ensure child 0 is selection marker
        EnsureSelectionChild(instance);

        // Save Prefab
        string dir = Path.GetDirectoryName(prefabPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);

        return savedPrefab;
    }

    private static Mesh GetSubmarineMesh(int index)
    {
        string[] modelPaths = new string[]
        {
            "",
            "Assets/Prefabs/Units Prefabs/Characters/Submarines/Models/Submarine.fbx",
            "Assets/Prefabs/Units Prefabs/Characters/Submarines/Models/Submarine A2.fbx",
            "Assets/Prefabs/Units Prefabs/Characters/Submarines/Models/Sub_Fhark.fbx",
            "Assets/Prefabs/Units Prefabs/Characters/Submarines/Models/Submarine A3.fbx",
            "Assets/Prefabs/Units Prefabs/Characters/Submarines/Models/Submarine A4.fbx"
        };

        if (index > 0 && index < modelPaths.Length)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPaths[index]);
            if (model != null)
            {
                MeshFilter mf = model.GetComponentInChildren<MeshFilter>();
                if (mf != null) return mf.sharedMesh;
            }
        }
        return null;
    }

    private static void EnsureSelectionChild(GameObject root)
    {
        if (root.transform.childCount == 0 || root.transform.GetChild(0).name != "Selected")
        {
            Transform existing = root.transform.Find("Selected");
            if (existing != null)
            {
                existing.SetSiblingIndex(0);
            }
            else
            {
                GameObject sel = GameObject.CreatePrimitive(PrimitiveType.Quad);
                sel.name = "Selected";
                sel.transform.SetParent(root.transform, false);
                sel.transform.SetSiblingIndex(0);
                sel.transform.localPosition = new Vector3(0, -0.5f, 0);
                sel.transform.localRotation = Quaternion.Euler(90, 0, 0);
                sel.transform.localScale = new Vector3(3, 3, 1);
                sel.SetActive(false);
                Object.DestroyImmediate(sel.GetComponent<Collider>());
            }
        }
    }

    private static string GetCategoryName(string vesselName, NavalClass navalClass)
    {
        return vesselName switch
        {
            "Freight Ship" => "Basic Trade Ship",
            "Cargo Liner" => "Fast Trade Ship",
            "Container Ship" => "Heavy Trade Ship",
            "Oil Tanker" => "Bulk Transport Ship",
            "Commando Ship" => "General Purpose Warship",
            "Viper" => "Fast Anti-Submarine Warship",
            "Hovercraft" => "Fast Anti-Air Warship",
            "Colossus" => "Heavy Battleship",
            "Shark" => "Elite Multi-Role Warship",
            "Raider" => "Fast Multi-Role Raider",
            "Atlas" => "Fleet Support / Aircraft Carrier",
            "T38 Ocean Glider" => "Utility / Cargo Submarine",
            "Sisyphus" => "Heavy Cargo Submarine",
            "Deep Sea Hunter" => "Attack Submarine",
            "Orca" => "Heavy Missile Submarine",
            "Erebos" => "Advanced Missile Submarine",
            _ => navalClass.ToString()
        };
    }

    private static T GetOrCreateAsset<T>(string path, System.Action<T> initializer) where T : ScriptableObject
    {
        string dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            initializer(asset);
            AssetDatabase.CreateAsset(asset, path);
        }
        else
        {
            initializer(asset);
            EditorUtility.SetDirty(asset);
        }
        return asset;
    }

    private static void SetIdentifier(ScriptableObject target, string id)
    {
        var field = target.GetType().GetField("identifier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null)
        {
            field = target.GetType().BaseType?.GetField("identifier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        }
        if (field != null) field.SetValue(target, id);
    }

    private static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            if (field.FieldType == typeof(LayerMask) && value is int intVal)
            {
                LayerMask lm = default;
                lm.value = intVal;
                field.SetValue(target, lm);
                return;
            }
            field.SetValue(target, value);
        }
    }

    private static void EnsureDirectories()
    {
        string[] dirs = new string[]
        {
            $"{DataRoot}/Trade",
            $"{DataRoot}/Combat",
            $"{DataRoot}/Support",
            $"{DataRoot}/Submarines",
            $"{DataRoot}/Abilities",
            $"{DataRoot}/Movement",
            $"{PrefabRoot}/Trade",
            $"{PrefabRoot}/Combat",
            $"{PrefabRoot}/Support",
            $"{PrefabRoot}/Submarines"
        };

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }
    }
}
#endif

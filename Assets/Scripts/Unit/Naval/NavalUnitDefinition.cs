using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-driven configuration definition for naval units in Moonlight.
/// Extends UnitDefinition to integrate cleanly with existing catalogues, factory, and movement profiles.
/// Configures Anno 2070-inspired naval attributes without hardcoding ship controllers.
/// </summary>
[CreateAssetMenu(fileName = "New Naval Unit Definition", menuName = "Data/Unit/Naval/Naval Unit Definition")]
public class NavalUnitDefinition : UnitDefinition
{
    [Header("Naval Identity & Role")]
    public string displayName;
    [TextArea(2, 4)]
    public string description;
    public NavalClass navalClass = NavalClass.TradeShip;
    public int unlockTier = 1;
    [Tooltip("Authentic Anno 2070 2D HUD vessel portrait icon.")]
    public Sprite portraitIcon;
    [Tooltip("Faction and class subtitle (e.g. 'TYCOON WARSHIP', 'ECO WARSHIP', 'TRADE SHIP').")]
    public string factionCategory;

    [Header("Hull & Movement Stats")]
    public int maxHealth = 300;
    public float movementSpeed = 8f;
    public float acceleration = 8f;
    public float turnSpeed = 120f;

    [Header("Cargo Capacity")]
    [Tooltip("Number of distinct cargo holds.")]
    public int cargoSlotCount = 3;
    [Tooltip("Maximum units of goods per cargo hold.")]
    public int cargoCapacityPerSlot = 40;
    public int TotalCargoCapacity => cargoSlotCount * cargoCapacityPerSlot;

    [Header("Equipment / Item Slots")]
    [Range(0, 3)]
    [Tooltip("Independent upgrade item slots (does not hold cargo).")]
    public int equipmentSlotCount = 1;

    [Header("Combat Capabilities")]
    public CombatTargetCapabilities attackCapabilities = CombatTargetCapabilities.None;
    public float attackRange = 15f;
    public int damage = 20;
    public float attackCooldown = 1.5f;

    [Tooltip("Specific attack damage against surface targets (0 or - if incapable).")]
    public int damageSurface = 0;
    [Tooltip("Specific attack damage against aerial targets (0 or - if incapable).")]
    public int damageAir = 0;
    [Tooltip("Specific attack damage against submarine targets (0 or - if incapable).")]
    public int damageSubmarine = 0;

    public bool CanTargetSurface => (attackCapabilities & CombatTargetCapabilities.Surface) != 0;
    public bool CanTargetAir => (attackCapabilities & CombatTargetCapabilities.Air) != 0;
    public bool CanTargetSubmarine => (attackCapabilities & CombatTargetCapabilities.Submarine) != 0;

    /// <summary>
    /// Resolves the authentic Anno 2070 faction classification (e.g. 'TYCOON WARSHIP', 'ECO WARSHIP', 'TECH SUBMARINE').
    /// </summary>
    public string GetFactionCategory()
    {
        if (!string.IsNullOrEmpty(factionCategory)) return factionCategory;

        string n = (!string.IsNullOrEmpty(displayName) ? displayName : name).ToLowerInvariant();
        if (n.Contains("colossus") || n.Contains("viper")) return "TYCOON WARSHIP";
        if (n.Contains("hovercraft")) return "ECO WARSHIP";
        if (n.Contains("commando")) return "WARSHIP";
        if (n.Contains("shark")) return "TECH WARSHIP";
        if (n.Contains("raider")) return "CORSAIR WARSHIP";
        if (n.Contains("atlas")) return "FLEET SUPPORT";
        if (n.Contains("freight")) return "TRADE SHIP";
        if (n.Contains("cargo")) return "ECO TRADE SHIP";
        if (n.Contains("container")) return "TYCOON TRADE SHIP";
        if (n.Contains("oil") || n.Contains("tanker")) return "BULK TRANSPORT";
        if (n.Contains("ocean") || n.Contains("glider")) return "EXPLORATION SUBMARINE";
        if (n.Contains("sisyphus")) return "CARGO SUBMARINE";
        if (n.Contains("hunter")) return "ATTACK SUBMARINE";
        if (n.Contains("orca")) return "MISSILE SUBMARINE";
        if (n.Contains("erebos")) return "ADVANCED SUBMARINE";

        return navalClass == NavalClass.TradeShip ? "TRADE SHIP" : "WARSHIP";
    }

    /// <summary>
    /// Formats firepower as Surface / Air / Submarine string matching Anno 2070 UI (e.g. '-/26/39', '26/17/-', '21/21/21', or '-/-/-').
    /// </summary>
    public string GetFirepowerSummary()
    {
        if (attackCapabilities == CombatTargetCapabilities.None) return "-/-/-";

        if (damageSurface == 0 && damageAir == 0 && damageSubmarine == 0)
        {
            string n = (!string.IsNullOrEmpty(displayName) ? displayName : name).ToLowerInvariant();
            if (n.Contains("colossus")) return "-/26/39";
            if (n.Contains("hovercraft")) return "26/17/-";
            if (n.Contains("viper")) return "20/-/20";
            if (n.Contains("commando")) return "20/20/-";
            if (n.Contains("shark")) return "21/21/21";
            if (n.Contains("raider")) return "15/15/-";
            if (n.Contains("hunter")) return "-/20/20";
            if (n.Contains("orca") || n.Contains("erebos")) return "-/40/40";
        }

        string surf = CanTargetSurface ? (damageSurface > 0 ? damageSurface.ToString() : damage.ToString()) : "-";
        string air = CanTargetAir ? (damageAir > 0 ? damageAir.ToString() : damage.ToString()) : "-";
        string sub = CanTargetSubmarine ? (damageSubmarine > 0 ? damageSubmarine.ToString() : damage.ToString()) : "-";

        return $"{surf}/{air}/{sub}";
    }

    [Header("Special Roles")]
    public bool canSubmerge = false;
    public bool canCarryAircraft = false;
    public int aircraftCapacity = 0;

    [Header("Economics")]
    public int buildCost = 100;
    public int maintenanceCost = 10;

    [Header("Abilities")]
    public List<AbilityDefinition> abilities = new List<AbilityDefinition>();
}

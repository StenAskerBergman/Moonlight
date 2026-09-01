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

    public bool CanTargetSurface => (attackCapabilities & CombatTargetCapabilities.Surface) != 0;
    public bool CanTargetAir => (attackCapabilities & CombatTargetCapabilities.Air) != 0;
    public bool CanTargetSubmarine => (attackCapabilities & CombatTargetCapabilities.Submarine) != 0;

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

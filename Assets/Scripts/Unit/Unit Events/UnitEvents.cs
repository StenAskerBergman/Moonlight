using UnityEngine;

/// <summary>
/// Slice 1 unit event structs. Named On{Noun}{PastTense} per project convention
/// (MapManager.OnMapGenerated, RuntimeNavMeshBaker.OnNavMeshBaked).
///
/// All events are readonly structs — zero allocation, pass by 'in' reference.
/// </summary>

/// <summary>Fired when gameplay requests a unit before the factory runs.</summary>
public readonly struct OnUnitRequested
{
    public readonly UnitDefinition Definition;
    public readonly Vector3 Position;

    public OnUnitRequested(UnitDefinition definition, Vector3 position)
    {
        Definition = definition;
        Position = position;
    }
}

/// <summary>Fired after the factory has created and activated a unit.</summary>
public readonly struct OnUnitCreated
{
    public readonly Unit Unit;
    public readonly UnitDefinition Definition;

    public OnUnitCreated(Unit unit, UnitDefinition definition)
    {
        Unit = unit;
        Definition = definition;
    }
}

/// <summary>Fired when a unit has been delivered (e.g. production queue complete).</summary>
public readonly struct OnUnitDelivered
{
    public readonly Unit Unit;

    public OnUnitDelivered(Unit unit)
    {
        Unit = unit;
    }
}

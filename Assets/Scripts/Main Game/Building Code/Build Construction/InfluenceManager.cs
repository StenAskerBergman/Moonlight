using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class InfluenceManager : MonoBehaviour
{
    [SerializeField] private List<InfluenceZone> activeZones = new List<InfluenceZone>();

    public bool HasWarehouse => activeZones.Any(z => z != null && (z.ZoneType == RequirementEnums.RequirementSubTypeZone.DepotZone || z.ZoneType == RequirementEnums.RequirementSubTypeZone.TradeZone));

    public void RegisterZone(InfluenceZone zone)
    {
        if (zone != null && !activeZones.Contains(zone))
        {
            activeZones.Add(zone);
        }
    }

    public void UnregisterZone(InfluenceZone zone)
    {
        if (zone != null)
        {
            activeZones.Remove(zone);
        }
    }

    public bool IsWithinBuildableArea(Vector3 worldPosition)
    {
        // Clean up any null/destroyed references
        activeZones.RemoveAll(z => z == null);

        if (activeZones.Count == 0) return false;
        return activeZones.Any(z => z.ContainsPoint(worldPosition));
    }

    public const float BoatFoundingRange = 30f;

    public static Unit GetNearestPlayerBoat(Vector3 targetPosition, float maxRange = BoatFoundingRange)
    {
        if (UnitSelections.Instance == null) return null;

        Vector3 targetFlat = targetPosition;
        targetFlat.y = 0f;

        // First priority: check if a currently selected unit is a valid boat in range
        if (UnitSelections.Instance.unitsSelected != null)
        {
            foreach (Unit unit in UnitSelections.Instance.unitsSelected)
            {
                if (unit != null && IsBoatUnit(unit))
                {
                    Vector3 unitFlat = unit.transform.position;
                    unitFlat.y = 0f;
                    if (Vector3.Distance(targetFlat, unitFlat) <= maxRange)
                    {
                        return unit;
                    }
                }
            }
        }

        // Second priority: search all active units for the closest player boat
        Unit nearestBoat = null;
        float nearestDist = maxRange;

        if (UnitSelections.Instance.unitList != null)
        {
            foreach (Unit unit in UnitSelections.Instance.unitList)
            {
                if (unit != null && IsBoatUnit(unit))
                {
                    Vector3 unitFlat = unit.transform.position;
                    unitFlat.y = 0f;
                    float dist = Vector3.Distance(targetFlat, unitFlat);
                    if (dist <= nearestDist)
                    {
                        nearestDist = dist;
                        nearestBoat = unit;
                    }
                }
            }
        }

        return nearestBoat;
    }

    public static bool IsBoatUnit(Unit unit)
    {
        if (unit == null) return false;
        if (unit.moveType == MoveType.Watercraft || unit.moveType == MoveType.Hovercraft || unit.moveType == MoveType.Submersible)
        {
            return true;
        }
        string name = (unit.displayName ?? unit.name ?? "").ToLower();
        return name.Contains("ship") || name.Contains("boat") || name.Contains("vessel") || name.Contains("flagship") || name.Contains("submarine");
    }

    public bool CanPlaceWarehouse(Vector3 worldPosition, GridSystem gridSystem, out Unit foundingBoat)
    {
        // Clean up any null/destroyed references
        activeZones.RemoveAll(z => z == null);
        foundingBoat = null;

        // First warehouse on an unsettled island: any valid Beach cell on the island within boat range
        if (!HasWarehouse)
        {
            if (gridSystem == null) return false;
            Cell cell = gridSystem.GetCellAtWorldPosition(worldPosition);
            if (cell == null || cell.currentTerrainType != Cell.TerrainType.Beach) return false;

            foundingBoat = GetNearestPlayerBoat(worldPosition);
            return foundingBoat != null;
        }

        // Subsequent warehouses or harbor expansions must be within existing island influence
        return IsWithinBuildableArea(worldPosition);
    }

    public bool CanPlaceWarehouse(Vector3 worldPosition, GridSystem gridSystem)
    {
        return CanPlaceWarehouse(worldPosition, gridSystem, out _);
    }
}

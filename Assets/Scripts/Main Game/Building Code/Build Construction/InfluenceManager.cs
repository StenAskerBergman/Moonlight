using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class InfluenceManager : MonoBehaviour
{
    [SerializeField] private List<InfluenceZone> activeZones = new List<InfluenceZone>();

    public bool HasWarehouse => activeZones.Any(z => z != null && (z.ZoneType == RequirementEnums.RequirementSubTypeZone.DepotZone || z.ZoneType == RequirementEnums.RequirementSubTypeZone.TradeZone));

    /// <summary>
    /// The zones defining where building is currently allowed. Read by the placement
    /// overlay so it can shade exactly the area IsWithinBuildableArea would accept,
    /// rather than testing every cell on the island.
    /// </summary>
    public IReadOnlyList<InfluenceZone> Zones => activeZones;

    public int ActiveZoneCount => activeZones.Count(z => z != null);

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

    /// <summary>
    /// How far a vessel may reach to found a harbor. A Settlement component is the
    /// per-vessel authority, so the white circle drawn around the boat and the reach the
    /// placement check enforces are always the same number.
    /// </summary>
    public static float FoundingRangeOf(Unit unit, float fallback = BoatFoundingRange)
    {
        if (unit == null) return fallback;

        Settlement settlement = unit.GetComponent<Settlement>();
        return settlement != null ? settlement.SettleRange : fallback;
    }

    /// <summary>
    /// The player boat currently selected, regardless of distance. Used to anchor the
    /// influence circle for the whole placement session rather than to the hovered cell.
    /// </summary>
    public static Unit GetSelectedPlayerBoat()
    {
        if (UnitSelections.Instance == null || UnitSelections.Instance.unitsSelected == null) return null;

        foreach (Unit unit in UnitSelections.Instance.unitsSelected)
        {
            if (unit != null && IsBoatUnit(unit)) return unit;
        }
        return null;
    }

    /// <summary>
    /// Whether a prefab is a harbor/warehouse - the building that founds an island.
    ///
    /// Deliberately tolerant: not every prefab carries a BuildingData asset (the Depot
    /// does not), and a null BuildingData used to make every check fall through to
    /// "ordinary building", which required existing island influence. Nothing could ever
    /// satisfy that on an unsettled island, so the founding loop could not start.
    /// </summary>
    public static bool IsHarborBuilding(GameObject prefab)
    {
        if (prefab == null) return false;

        return IsHarborBuilding(prefab.GetComponent<BuildingProperties>()) || NameLooksLikeHarbor(prefab.name);
    }

    public static bool IsHarborBuilding(BuildingProperties properties)
    {
        if (properties == null) return false;

        if (IsHarborBuilding(properties.buildingData)) return true;

        if (NameLooksLikeHarbor(properties.buildingName)) return true;

        InfluenceZone zone = properties.GetComponent<InfluenceZone>();
        return zone != null && (zone.ZoneType == RequirementEnums.RequirementSubTypeZone.DepotZone
                             || zone.ZoneType == RequirementEnums.RequirementSubTypeZone.TradeZone);
    }

    public static bool IsHarborBuilding(BuildingData data)
    {
        if (data == null) return false;

        if (data.buildingType == BuildingEnums.BuildingType.OnShore.ToString()) return true;

        if (data.buildingTags != null && System.Array.Exists(data.buildingTags, tag =>
                tag.Equals("Warehouse", System.StringComparison.OrdinalIgnoreCase) ||
                tag.Equals("Harbor", System.StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return NameLooksLikeHarbor(data.buildingName);
    }

    private static bool NameLooksLikeHarbor(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        return name.IndexOf("Warehouse", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Harbor", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Depot", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsSubmersibleUnit(Unit unit)
    {
        return unit != null && unit.moveType == MoveType.Submersible;
    }

    public static Unit GetNearestPlayerBoat(Vector3 targetPosition, float maxRange = BoatFoundingRange, bool? requireSubmersible = null)
    {
        if (UnitSelections.Instance == null) return null;

        Vector3 targetFlat = targetPosition;
        targetFlat.y = 0f;

        // First priority: check if a currently selected unit is a valid boat in range
        if (UnitSelections.Instance.unitsSelected != null)
        {
            foreach (Unit unit in UnitSelections.Instance.unitsSelected)
            {
                if (unit != null && IsBoatUnit(unit) && (!requireSubmersible.HasValue || IsSubmersibleUnit(unit) == requireSubmersible.Value))
                {
                    Vector3 unitFlat = unit.transform.position;
                    unitFlat.y = 0f;
                    if (Vector3.Distance(targetFlat, unitFlat) <= FoundingRangeOf(unit, maxRange))
                    {
                        return unit;
                    }
                }
            }
        }

        // Second priority: search all active units for the closest player boat
        Unit nearestBoat = null;
        float nearestDist = float.MaxValue;

        if (UnitSelections.Instance.unitList != null)
        {
            foreach (Unit unit in UnitSelections.Instance.unitList)
            {
                if (unit != null && IsBoatUnit(unit) && (!requireSubmersible.HasValue || IsSubmersibleUnit(unit) == requireSubmersible.Value))
                {
                    Vector3 unitFlat = unit.transform.position;
                    unitFlat.y = 0f;
                    float dist = Vector3.Distance(targetFlat, unitFlat);
                    if (dist <= FoundingRangeOf(unit, maxRange) && dist < nearestDist)
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
        // Name matching is only a backstop for a unit whose moveType was never set; set
        // moveType on the prefab rather than relying on it. It is weak by design: at
        // runtime Unit.Start auto-names an unnamed unit from NameGenerator, so displayName
        // is usually something like "Turtle" that matches none of these keywords. The
        // ?? here also only falls through on null, and the Inspector default is an empty
        // string, so the object-name fallback needs the explicit whitespace check.
        string name = (string.IsNullOrWhiteSpace(unit.displayName) ? unit.name : unit.displayName).ToLower();
        return name.Contains("ship") || name.Contains("boat") || name.Contains("vessel") || name.Contains("flagship") || name.Contains("submarine");
    }

    public bool CanPlaceWarehouse(Vector3 worldPosition, GridSystem gridSystem, out Unit foundingBoat, Unit requestingBoat = null)
    {
        // Clean up any null/destroyed references
        activeZones.RemoveAll(z => z == null);
        foundingBoat = null;

        // First warehouse on an unsettled island or underwater plateau:
        // - Surface vessels settle legal coastal/quay cells on islands.
        // - Submarines (MoveType.Submersible) settle buildable seabed plateau cells (Anno 2070 Deep Ocean).
        if (!HasWarehouse)
        {
            if (gridSystem == null) return false;
            Cell cell = gridSystem.GetCellAtWorldPosition(worldPosition);
            if (cell == null || cell.isBlocked || cell.isOccupied) return false;

            bool isPlateauCell = cell.currentTerrainType == Cell.TerrainType.Plateau;
            if (isPlateauCell)
            {
                if (!cell.IsSlopeSuitableForBuilding) return false;
            }
            else if (!QuaySystem.IsLegalQuayTerrain(cell.currentTerrainType))
            {
                return false;
            }

            if (requestingBoat == null)
            {
                foundingBoat = GetNearestPlayerBoat(worldPosition, BoatFoundingRange, requireSubmersible: isPlateauCell);
            }
            else if (IsBoatUnit(requestingBoat) && IsSubmersibleUnit(requestingBoat) == isPlateauCell)
            {
                Vector3 offset = requestingBoat.transform.position - worldPosition;
                offset.y = 0f;
                float range = FoundingRangeOf(requestingBoat, BoatFoundingRange);
                if (offset.sqrMagnitude <= range * range) foundingBoat = requestingBoat;
            }
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

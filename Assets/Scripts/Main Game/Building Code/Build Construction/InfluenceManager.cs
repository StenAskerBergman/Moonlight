using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class InfluenceManager : MonoBehaviour
{
    private List<InfluenceZone> activeZones = new List<InfluenceZone>();

    public bool HasWarehouse => activeZones.Any(z => z.ZoneType == RequirementEnums.RequirementSubTypeZone.DepotZone || z.ZoneType == RequirementEnums.RequirementSubTypeZone.TradeZone);

    public void RegisterZone(InfluenceZone zone) { activeZones.Add(zone); }
    public void UnregisterZone(InfluenceZone zone) { activeZones.Remove(zone); }

    public bool IsWithinBuildableArea(Vector3 worldPosition)
    {
        if (activeZones.Count == 0) return false;
        return activeZones.Any(z => z.ContainsPoint(worldPosition));
    }

    public bool CanPlaceWarehouse(Vector3 worldPosition, GridSystem gridSystem)
    {
        // First warehouse: any Beach cell on the island
        // Subsequent: must be within existing influence
        if (!HasWarehouse)
        {
            Cell cell = gridSystem.GetCellAtWorldPosition(worldPosition);
            return cell != null && cell.currentTerrainType == Cell.TerrainType.Beach;
        }
        return IsWithinBuildableArea(worldPosition);
    }
}

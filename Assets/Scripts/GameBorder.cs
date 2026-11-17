using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameBorder : MonoBehaviour
{
    public bool ShowRange = true;
    [Tooltip("Automatically derive camera pan bounds and world center from the generated map chunks.")]
    public bool autoFitToGeneratedMap = true;
    [Tooltip("Extra pan margin beyond the generated map edge so outer islands and plateaus can be centered cleanly.")]
    [Min(0f)] public float margin = 20f;
    public Vector2 _range = new Vector2(200, 200); // Map Border for the Camera
    public Vector3 worldCenter = new Vector3(0, 0, 0);
    public float xOff, yOff, zOff;

    private void Awake()
    {
        if (autoFitToGeneratedMap && FitToGeneratedMap())
            return;

        MapManager mapManager = GetComponent<MapManager>();
        if (mapManager != null)
        {
            if (xOff == 0) xOff = mapManager.xOffset;
            if (yOff == 0) yOff = 0;
            if (zOff == 0) zOff = mapManager.zOffset;
            worldCenter = new Vector3(transform.position.x - xOff / 2, worldCenter.y, transform.position.z + zOff / 2);
        }
    }

    public bool FitToGeneratedMap()
    {
        MapGrid[] grids = GetComponentsInChildren<MapGrid>(true);
        if (grids == null || grids.Length == 0)
            grids = FindObjectsOfType<MapGrid>();

        Bounds mapBounds = default;
        bool hasBounds = false;
        for (int i = 0; i < grids.Length; i++)
        {
            if (grids[i] == null) continue;
            MeshRenderer mr = grids[i].GetComponent<MeshRenderer>();
            if (mr == null) continue;
            if (!hasBounds)
            {
                mapBounds = mr.bounds;
                hasBounds = true;
            }
            else
            {
                mapBounds.Encapsulate(mr.bounds);
            }
        }

        if (!hasBounds) return false;

        worldCenter = new Vector3(mapBounds.center.x, 0f, mapBounds.center.z);
        _range = new Vector2(mapBounds.extents.x + margin, mapBounds.extents.z + margin);
        return true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        if (ShowRange) Gizmos.DrawWireCube(worldCenter, new Vector3(_range.x * 2, 0, _range.y * 2));
    }

    internal bool IsInBounds(Vector3 position)
    {
        // Adjust position by world center for checking
        Vector3 adjustedPosition = position - worldCenter;
        return adjustedPosition.x > -_range.x &&
               adjustedPosition.x < _range.x &&
               adjustedPosition.z > -_range.y &&
               adjustedPosition.z < _range.y;
    }

    internal Vector3 GetNearestPointOnBounds(Vector3 position)
    {
        // Adjust position by world center for clamping
        Vector3 adjustedPosition = position - worldCenter;
        adjustedPosition.x = Mathf.Clamp(adjustedPosition.x, -_range.x, _range.x);
        adjustedPosition.z = Mathf.Clamp(adjustedPosition.z, -_range.y, _range.y);

        // Convert back to world space
        return adjustedPosition + worldCenter;
    }
}

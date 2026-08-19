using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameBorder : MonoBehaviour
{
    public bool ShowRange = true;
    public Vector2 _range = new Vector2(100, 100); // Map Border for the Camera
    public Vector3 worldCenter = new Vector3(0, 0, 0);
    public float xOff, yOff, zOff;

    private void Awake()
    {
        MapManager mapManager = GetComponent<MapManager>();

        // Apply the offset if they haven't been set explicitly
        if (xOff == 0) xOff = mapManager.xOffset;
        if (yOff == 0) yOff = 0; // Assume yOff is not used as there's no yOffset in your example
        if (zOff == 0) zOff = mapManager.zOffset;

        // Adjust the world center based on the transform and offsets
        worldCenter = new Vector3(transform.position.x - xOff / 2, worldCenter.y, transform.position.z + zOff / 2);
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

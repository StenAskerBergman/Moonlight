using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the visible, elevated water surfaces for reserved rivers and alpine lakes.
/// Terrain generation owns the bed; this builder owns only the thin water skin.
/// </summary>
public static class InlandWaterMeshBuilder
{
    private const float SurfaceLift = 0.035f;

    public static Mesh Build(FeatureReservationMap reservations)
    {
        if (reservations == null || (reservations.Rivers.Count == 0 && reservations.Lakes.Count == 0))
            return null;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        for (int i = 0; i < reservations.Rivers.Count; i++)
            AppendRiver(reservations.Rivers[i], vertices, uvs, triangles);
        for (int i = 0; i < reservations.Lakes.Count; i++)
            AppendLake(reservations.Lakes[i], vertices, uvs, triangles);

        if (vertices.Count == 0) return null;
        Mesh mesh = new Mesh { name = "Generated Inland Water" };
        if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AppendRiver(
        FeatureReservationMap.RiverCorridor river,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        if (river == null || river.Waypoints.Count < 2) return;
        int start = vertices.Count;

        for (int i = 0; i < river.Waypoints.Count; i++)
        {
            Vector2 previous = river.Waypoints[Mathf.Max(0, i - 1)].Position;
            Vector2 next = river.Waypoints[Mathf.Min(river.Waypoints.Count - 1, i + 1)].Position;
            Vector2 direction = (next - previous).normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float t = i / (float)(river.Waypoints.Count - 1);
            float halfWidth = river.Waypoints[i].ChannelRadius * 0.88f;
            float y = river.GetSurfaceHeight(t) + SurfaceLift;
            Vector2 center = river.Waypoints[i].Position;

            vertices.Add(new Vector3(center.x - perpendicular.x * halfWidth, y, center.y - perpendicular.y * halfWidth));
            vertices.Add(new Vector3(center.x + perpendicular.x * halfWidth, y, center.y + perpendicular.y * halfWidth));
            uvs.Add(new Vector2(0f, t * 8f));
            uvs.Add(new Vector2(1f, t * 8f));

            if (i == 0) continue;
            int row = start + i * 2;
            triangles.Add(row - 2); triangles.Add(row); triangles.Add(row - 1);
            triangles.Add(row - 1); triangles.Add(row); triangles.Add(row + 1);
        }
    }

    private static void AppendLake(
        FeatureReservationMap.LakeBasin lake,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        if (lake == null) return;
        const int segments = 32;
        int centerIndex = vertices.Count;
        vertices.Add(new Vector3(lake.Center.x, lake.SurfaceHeight + SurfaceLift, lake.Center.y));
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int i = 0; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);
            vertices.Add(new Vector3(
                lake.Center.x + x * lake.Radius * 0.94f,
                lake.SurfaceHeight + SurfaceLift,
                lake.Center.y + z * lake.Radius * 0.94f));
            uvs.Add(new Vector2(x * 0.5f + 0.5f, z * 0.5f + 0.5f));
            if (i == 0) continue;
            triangles.Add(centerIndex);
            triangles.Add(centerIndex + i);
            triangles.Add(centerIndex + i + 1);
        }
    }
}

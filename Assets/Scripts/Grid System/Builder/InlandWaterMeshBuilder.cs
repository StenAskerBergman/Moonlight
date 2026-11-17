// tldr: Builds the water surface mesh for inland water - rivers and lakes - as distinct from the global ocean.

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Uploads the river network's wet triangles - every course, confluence and source lake from the
/// one shared field, so overlapping courses never draw two water sheets. No path, radius or
/// height reconstruction.
/// </summary>
public static class InlandWaterMeshBuilder
{
    public static Mesh Build(FeatureReservationMap reservations)
    {
        if (reservations == null || reservations.RiverNetwork == null || reservations.RiverPlans.Count == 0) return null;
        var vertices = new List<Vector3>(); var uv = new List<Vector2>();
        var colors = new List<Color>(); var indices = new List<int>();
        reservations.RiverNetwork.AppendWaterMesh(vertices, uv, colors, indices);
        var mesh = new Mesh { name = "Connected River Landscape Water" };
        if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(indices, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }
}

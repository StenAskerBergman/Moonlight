// tldr: Builds the water surface mesh for inland water - rivers and lakes - as distinct from the global ocean.

using System.Collections.Generic;
using UnityEngine;

/// <summary>Uploads RiverPlan's wet triangles. No path, radius or height reconstruction.</summary>
public static class InlandWaterMeshBuilder
{
    public static Mesh Build(FeatureReservationMap reservations)
    {
        if (reservations == null || reservations.RiverPlans.Count == 0) return null;
        var vertices = new List<Vector3>(); var uv = new List<Vector2>();
        var colors = new List<Color>(); var indices = new List<int>();
        foreach (RiverPlan plan in reservations.RiverPlans) plan.AppendWaterMesh(vertices, uv, colors, indices);
        var mesh = new Mesh { name = "Connected River Landscape Water" };
        if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(indices, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }
}

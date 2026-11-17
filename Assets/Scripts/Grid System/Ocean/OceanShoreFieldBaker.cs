// tldr: Bakes the world-space "where is the coast" texture the ocean and terrain shaders share:
// signed distance to the shoreline, water depth, and the seaward direction, rasterised from the
// generated chunk meshes - i.e. from the ground the player actually sees.

using System.Collections.Generic;
using UnityEngine;

public static class OceanShoreFieldBaker
{
    public struct Result
    {
        public Texture2D Texture;
        /// <summary>World-space XZ rectangle the texture covers.</summary>
        public Rect WorldRect;
        public int LandTexels;
    }

    private const float NoData = float.NegativeInfinity;
    private const float FarDistance = 1000f;

    /// <param name="chunkMeshes">Renderable terrain meshes and their local-to-world matrices.</param>
    /// <param name="seaLevel">Rendered ocean datum.</param>
    /// <param name="noDataDepth">Depth reported where no chunk covers the field (open sea).</param>
    /// <param name="texelSize">World units per texel; enlarged automatically to cap the size.</param>
    /// <param name="margin">Open-sea margin around the chunks, at least the shore-wave reach.</param>
    public static Result Bake(
        IReadOnlyList<(Mesh mesh, Matrix4x4 localToWorld)> chunkMeshes,
        float seaLevel,
        float noDataDepth,
        float texelSize,
        float margin,
        int maxResolution = 1024)
    {
        Result result = default;
        if (chunkMeshes == null || chunkMeshes.Count == 0) return result;

        // World footprint of everything we rasterise.
        bool hasBounds = false;
        Bounds bounds = default;
        foreach (var (mesh, matrix) in chunkMeshes)
        {
            if (mesh == null) continue;
            Bounds world = TransformBounds(mesh.bounds, matrix);
            if (!hasBounds) { bounds = world; hasBounds = true; }
            else bounds.Encapsulate(world);
        }
        if (!hasBounds) return result;

        float minX = bounds.min.x - margin;
        float minZ = bounds.min.z - margin;
        float sizeX = bounds.size.x + margin * 2f;
        float sizeZ = bounds.size.z + margin * 2f;
        texelSize = Mathf.Max(texelSize, Mathf.Max(sizeX, sizeZ) / Mathf.Max(64, maxResolution));
        int width = Mathf.Clamp(Mathf.CeilToInt(sizeX / texelSize), 2, maxResolution);
        int height = Mathf.Clamp(Mathf.CeilToInt(sizeZ / texelSize), 2, maxResolution);
        // Snap the covered size to whole texels so texel centres land on a regular world grid.
        sizeX = width * texelSize;
        sizeZ = height * texelSize;

        float[] ground = new float[width * height];
        for (int i = 0; i < ground.Length; i++) ground[i] = NoData;

        foreach (var (mesh, matrix) in chunkMeshes)
        {
            if (mesh == null || !mesh.isReadable) continue;
            RasteriseMesh(mesh, matrix, ground, width, height, minX, minZ, texelSize);
        }

        // Land / sea classification at texel centres.
        int count = width * height;
        bool[] land = new bool[count];
        int landTexels = 0;
        for (int i = 0; i < count; i++)
        {
            land[i] = !float.IsNegativeInfinity(ground[i]) && ground[i] > seaLevel;
            if (land[i]) landTexels++;
        }

        // Exact Euclidean distance from every texel to the nearest texel of the other class.
        float[] toLand = new float[count];
        float[] toSea = new float[count];
        for (int i = 0; i < count; i++)
        {
            toLand[i] = land[i] ? 0f : float.MaxValue;
            toSea[i] = land[i] ? float.MaxValue : 0f;
        }
        DistanceTransform2D(toLand, width, height);
        DistanceTransform2D(toSea, width, height);

        // Signed distance (metres, + seaward). The boundary sits between texel centres, hence -0.5.
        float[] signedDistance = new float[count];
        for (int i = 0; i < count; i++)
        {
            float d = land[i]
                ? -(Mathf.Sqrt(toSea[i]) - 0.5f)
                : Mathf.Sqrt(toLand[i]) - 0.5f;
            if (float.IsNaN(d) || float.IsInfinity(d) || d > FarDistance) d = FarDistance;
            if (d < -FarDistance) d = -FarDistance;
            signedDistance[i] = d * texelSize;
        }

        // Texel-centre distances step in half-texel stairs along diagonal coasts; a small blur
        // turns them into smooth contours so breaker lines follow the coast instead of the grid.
        BoxBlur(signedDistance, width, height, 1);
        BoxBlur(signedDistance, width, height, 1);

        // Seaward direction = normalised gradient of the distance field.
        ushort[] pixels = new ushort[count * 4];
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = z * width + x;
                float gx = signedDistance[z * width + Mathf.Min(x + 1, width - 1)]
                         - signedDistance[z * width + Mathf.Max(x - 1, 0)];
                float gz = signedDistance[Mathf.Min(z + 1, height - 1) * width + x]
                         - signedDistance[Mathf.Max(z - 1, 0) * width + x];
                float len = Mathf.Sqrt(gx * gx + gz * gz);
                if (len > 1e-5f) { gx /= len; gz /= len; }
                else { gx = 0f; gz = 0f; }

                float depth = float.IsNegativeInfinity(ground[i]) ? noDataDepth : seaLevel - ground[i];

                int p = i * 4;
                pixels[p + 0] = Mathf.FloatToHalf(signedDistance[i]);
                pixels[p + 1] = Mathf.FloatToHalf(Mathf.Clamp(depth, -1000f, 1000f));
                pixels[p + 2] = Mathf.FloatToHalf(gx);
                pixels[p + 3] = Mathf.FloatToHalf(gz);
            }
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true)
        {
            name = "Ocean Shore Field",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
        };
        texture.SetPixelData(pixels, 0);
        texture.Apply(false, true);

        result.Texture = texture;
        result.WorldRect = new Rect(minX, minZ, sizeX, sizeZ);
        result.LandTexels = landTexels;
        return result;
    }

    private static void RasteriseMesh(
        Mesh mesh, Matrix4x4 matrix, float[] ground, int width, int height,
        float minX, float minZ, float texelSize)
    {
        Vector3[] vertices = mesh.vertices;
        for (int v = 0; v < vertices.Length; v++)
            vertices[v] = matrix.MultiplyPoint3x4(vertices[v]);

        float inv = 1f / texelSize;
        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            if (mesh.GetTopology(sub) != MeshTopology.Triangles) continue;
            int[] triangles = mesh.GetTriangles(sub);
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]];
                Vector3 b = vertices[triangles[t + 1]];
                Vector3 c = vertices[triangles[t + 2]];

                // Texel-space coordinates; texel (x, z) has its centre at (x + 0.5, z + 0.5).
                float ax = (a.x - minX) * inv - 0.5f, az = (a.z - minZ) * inv - 0.5f;
                float bx = (b.x - minX) * inv - 0.5f, bz = (b.z - minZ) * inv - 0.5f;
                float cx = (c.x - minX) * inv - 0.5f, cz = (c.z - minZ) * inv - 0.5f;

                int x0 = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(ax, Mathf.Min(bx, cx))));
                int x1 = Mathf.Min(width - 1, Mathf.FloorToInt(Mathf.Max(ax, Mathf.Max(bx, cx))));
                int z0 = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(az, Mathf.Min(bz, cz))));
                int z1 = Mathf.Min(height - 1, Mathf.FloorToInt(Mathf.Max(az, Mathf.Max(bz, cz))));
                if (x0 > x1 || z0 > z1) continue;

                float area = (bx - ax) * (cz - az) - (cx - ax) * (bz - az);
                if (Mathf.Abs(area) < 1e-8f) continue; // vertical or degenerate in plan view
                float invArea = 1f / area;

                for (int z = z0; z <= z1; z++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float w0 = ((bx - x) * (cz - z) - (cx - x) * (bz - z)) * invArea;
                        float w1 = ((cx - x) * (az - z) - (ax - x) * (cz - z)) * invArea;
                        float w2 = 1f - w0 - w1;
                        const float tolerance = -1e-4f;
                        if (w0 < tolerance || w1 < tolerance || w2 < tolerance) continue;

                        float h = w0 * a.y + w1 * b.y + w2 * c.y;
                        int index = z * width + x;
                        // Max keeps the walkable top where skirts or overlapping edges stack.
                        if (h > ground[index]) ground[index] = h;
                    }
                }
            }
        }
    }

    /// <summary>
    /// In-place squared Euclidean distance transform (Felzenszwalb &amp; Huttenlocher).
    /// Input: 0 at features, float.MaxValue elsewhere. Output: squared distance in texels.
    /// </summary>
    private static void DistanceTransform2D(float[] grid, int width, int height)
    {
        int n = Mathf.Max(width, height);
        float[] f = new float[n];
        float[] d = new float[n];
        int[] v = new int[n];
        float[] zBuf = new float[n + 1];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++) f[z] = grid[z * width + x];
            DistanceTransform1D(f, height, d, v, zBuf);
            for (int z = 0; z < height; z++) grid[z * width + x] = d[z];
        }
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++) f[x] = grid[z * width + x];
            DistanceTransform1D(f, width, d, v, zBuf);
            for (int x = 0; x < width; x++) grid[z * width + x] = d[x];
        }
    }

    private static void DistanceTransform1D(float[] f, int n, float[] d, int[] v, float[] z)
    {
        const float Inf = 1e20f;
        int k = 0;
        int first = -1;
        for (int q = 0; q < n; q++)
        {
            if (f[q] < Inf) { first = q; break; }
        }
        if (first < 0)
        {
            for (int q = 0; q < n; q++) d[q] = float.MaxValue;
            return;
        }

        v[0] = first;
        z[0] = float.NegativeInfinity;
        z[1] = float.PositiveInfinity;
        for (int q = first + 1; q < n; q++)
        {
            if (f[q] >= Inf) continue;
            float s;
            while (true)
            {
                int p = v[k];
                s = ((f[q] + q * (float)q) - (f[p] + p * (float)p)) / (2f * q - 2f * p);
                if (s <= z[k] && k > 0) { k--; continue; }
                if (s <= z[k]) { k = -1; }
                break;
            }
            k++;
            v[k] = q;
            z[k] = s;
            z[k + 1] = float.PositiveInfinity;
        }

        k = 0;
        for (int q = 0; q < n; q++)
        {
            while (z[k + 1] < q) k++;
            float dq = q - v[k];
            d[q] = dq * dq + f[v[k]];
        }
    }

    private static void BoxBlur(float[] data, int width, int height, int radius)
    {
        float[] temp = new float[data.Length];
        float norm = 1f / (radius * 2 + 1);
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                float sum = 0f;
                for (int o = -radius; o <= radius; o++)
                    sum += data[z * width + Mathf.Clamp(x + o, 0, width - 1)];
                temp[z * width + x] = sum * norm;
            }
        }
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                float sum = 0f;
                for (int o = -radius; o <= radius; o++)
                    sum += temp[Mathf.Clamp(z + o, 0, height - 1) * width + x];
                data[z * width + x] = sum * norm;
            }
        }
    }

    private static Bounds TransformBounds(Bounds local, Matrix4x4 matrix)
    {
        Vector3 c = local.center;
        Vector3 e = local.extents;
        Bounds world = new Bounds(matrix.MultiplyPoint3x4(c), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);
            world.Encapsulate(matrix.MultiplyPoint3x4(corner));
        }
        return world;
    }
}

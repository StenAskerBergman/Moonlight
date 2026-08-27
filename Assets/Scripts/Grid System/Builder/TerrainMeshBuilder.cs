using UnityEngine;
using System.Collections.Generic;
using static Cell;
using UnityEngine.Rendering;

public class TerrainMeshBuilder
{
    public int size = 100;
    public float edgeStrength = 0.5f;
    public int edgeLength = 1;

    private readonly Cell[,] grid;
    private readonly IslandTerrainProvider terrainSource;
    private readonly int visualSamplesPerCell;

    public TerrainMeshBuilder(Cell[,] grid)
    {
        this.grid = grid;
        this.size = grid.GetLength(0);
        visualSamplesPerCell = 1;
    }

    public TerrainMeshBuilder(
        Cell[,] grid,
        IslandTerrainProvider terrainSource,
        int visualSamplesPerCell)
    {
        this.grid = grid;
        this.terrainSource = terrainSource;
        this.size = grid.GetLength(0);
        this.visualSamplesPerCell = Mathf.Max(1, visualSamplesPerCell);
    }

    public Mesh Build()
    {
        if (terrainSource != null && visualSamplesPerCell >= 1)
        {
            return BuildFractionalMesh();
        }

        Mesh mesh = new Mesh { name = "Generated Terrain" };
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Cell cell = grid[x, y];
                float cellHeight = cell.height;

                // Cell x occupies local [x, x+1) - same convention as the fractional
                // path above, so the legacy/debug mesh cannot disagree about extent.
                Vector3 a = new Vector3(x, cellHeight, y + 1f);
                Vector3 b = new Vector3(x + 1f, cellHeight, y + 1f);
                Vector3 c = new Vector3(x, cellHeight, y);
                Vector3 d = new Vector3(x + 1f, cellHeight, y);


                Vector2 uvA = new Vector2(x / (float)size, y / (float)size);
                Vector2 uvB = new Vector2((x + 1) / (float)size, y / (float)size);
                Vector2 uvC = new Vector2(x / (float)size, (y + 1) / (float)size);
                Vector2 uvD = new Vector2((x + 1) / (float)size, (y + 1) / (float)size);
                Vector3[] V = new Vector3[] { a, b, c, b, d, c };
                Vector2[] uv = new Vector2[] { uvA, uvB, uvC, uvB, uvD, uvC };
                for (int k = 0; k < 6; k++)
                {
                    vertices.Add(V[k]);
                    triangles.Add(triangles.Count);
                    uvs.Add(uv[k]);
                }

            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();

        return mesh;
    }

    private static int cachedQuadsPerAxis = -1;
    private static int[] cachedTriangles;
    private static Vector2[] cachedUvs;

    private static (int[] triangles, Vector2[] uvs) GetOrCreateTopology(int quadsPerAxis)
    {
        if (cachedQuadsPerAxis == quadsPerAxis && cachedTriangles != null && cachedUvs != null)
        {
            return (cachedTriangles, cachedUvs);
        }

        int verticesPerAxis = quadsPerAxis + 1;
        Vector2[] uvs = new Vector2[verticesPerAxis * verticesPerAxis];
        int[] triangles = new int[quadsPerAxis * quadsPerAxis * 6];

        for (int z = 0; z < verticesPerAxis; z++)
        {
            for (int x = 0; x < verticesPerAxis; x++)
            {
                int index = z * verticesPerAxis + x;
                uvs[index] = new Vector2(x / (float)quadsPerAxis, z / (float)quadsPerAxis);
            }
        }

        int triangleIndex = 0;
        for (int z = 0; z < quadsPerAxis; z++)
        {
            for (int x = 0; x < quadsPerAxis; x++)
            {
                int bottomLeft = z * verticesPerAxis + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + verticesPerAxis;
                int topRight = topLeft + 1;

                // Alternate the split diagonal in a checkerboard instead of using the same one for
                // every quad.
                //
                // With a uniform diagonal, every vertex averages an ASYMMETRIC fan of six
                // triangles - three on the diagonal's side, three off it - so RecalculateNormals
                // biases each normal along that one direction. On flat or gently curved ground it
                // is invisible, but where the surface bends sharply across the diagonal (a
                // mountain flank arriving at the coastal slope) neighbouring vertices pick up
                // systematically different normals and the bend renders as regular triangular
                // teeth. The heightfield itself is smooth there - every height contour traced
                // across this boundary has zero direction reversals - so the serration was purely
                // an artefact of how the smooth field was triangulated, which is why smoothing the
                // field, the texture and the water all left it untouched.
                //
                // Flipping the diagonal on alternate quads makes the fans mirror each other, so
                // the bias cancels between neighbours. Winding is preserved in both cases.
                if (((x + z) & 1) == 0)
                {
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = topRight;
                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = topRight;
                    triangles[triangleIndex++] = bottomRight;
                    triangles[triangleIndex++] = bottomLeft;
                }
                else
                {
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = topRight;
                    triangles[triangleIndex++] = bottomRight;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = bottomRight;
                    triangles[triangleIndex++] = bottomLeft;
                }
            }
        }

        cachedQuadsPerAxis = quadsPerAxis;
        cachedTriangles = triangles;
        cachedUvs = uvs;
        return (triangles, uvs);
    }

    private Mesh BuildFractionalMesh()
    {
        TerrainSampleCache cache = terrainSource.GetOrCreateSampleCache(visualSamplesPerCell);
        int quadsPerAxis = size * visualSamplesPerCell;
        int verticesPerAxis = quadsPerAxis + 1;
        float step = cache.Step;

        Vector3[] vertices = new Vector3[verticesPerAxis * verticesPerAxis];
        float[] heights = cache.Heights;

        System.Threading.Tasks.Parallel.For(0, verticesPerAxis, z =>
        {
            float localZ = z * step;
            int rowOffset = z * verticesPerAxis;
            for (int x = 0; x < verticesPerAxis; x++)
            {
                int index = rowOffset + x;
                vertices[index] = new Vector3(x * step, heights[index], localZ);
            }
        });

        var (triangles, uvs) = GetOrCreateTopology(quadsPerAxis);

        Mesh mesh = new Mesh { name = $"Fractional Terrain x{visualSamplesPerCell}" };
        if (vertices.Length > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}


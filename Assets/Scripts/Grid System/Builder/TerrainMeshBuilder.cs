using UnityEngine;
using System.Collections.Generic;
using static Cell;

public class TerrainMeshBuilder
{
    public int size = 100;
    public float edgeStrength = 0.5f;
    public int edgeLength = 1;

    private Cell[,] grid;

    public TerrainMeshBuilder(Cell[,] grid)
    {
        this.grid = grid;
        this.size = grid.GetLength(0);
    }

    public Mesh Build()
    {
        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Cell cell = grid[x, y];
                // Get the height from the cell's position, which should be set correctly in the Grid
                float cellHeight = cell.cellPosition.y;

                // If the cell is water, we might want to set it to a specific height
                if (cell.currentTerrainType == TerrainType.Water)
                {
                    cellHeight = -1f; // Set water level; adjust as needed
                }

                if (cell.currentTerrainType == TerrainType.Shallow)
                {
                    cellHeight = -2f; // Set Shallow level; adjust as needed
                }

                if (cell.currentTerrainType == TerrainType.Deep)
                {
                    cellHeight = -3f; // Set Deep level; adjust as needed
                }

                if (cell.currentTerrainType == TerrainType.Plateau)
                {
                    cellHeight = -3f; // Set Plateau level; adjust as needed
                }

                if (cell.currentTerrainType == TerrainType.Abyssal)
                {
                    cellHeight = -5f; // Set Abyssal level; adjust as needed
                }

                if (cell.currentTerrainType == TerrainType.Mountain)
                {
                    cellHeight = 1f; // Set water level; adjust as needed
                }

                // Now use the cellHeight for vertices
                Vector3 a = new Vector3(x - 0.5f, cellHeight, y + 0.5f);
                Vector3 b = new Vector3(x + 0.5f, cellHeight, y + 0.5f);
                Vector3 c = new Vector3(x - 0.5f, cellHeight, y - 0.5f);
                Vector3 d = new Vector3(x + 0.5f, cellHeight, y - 0.5f);


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
}
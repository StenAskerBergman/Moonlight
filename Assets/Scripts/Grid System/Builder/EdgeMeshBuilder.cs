using UnityEngine;
using System.Collections.Generic;
using static Cell;

public class EdgeMeshBuilder
{
    private Cell[,] grid;

    public EdgeMeshBuilder(Cell[,] grid)
    {
        this.grid = grid;
    }

    public (Mesh coastMesh, Mesh oceanMesh, Mesh mountainMesh, Mesh beachMesh) Build()
    {
        int size = grid.GetLength(0);

        // Cliffs
        Mesh mountainMesh = new Mesh();
        List<Vector3> mountainVertices = new List<Vector3>();
        List<int> mountainTriangles = new List<int>();

        // Coast
        Mesh coastMesh = new Mesh();
        List<Vector3> coastVertices = new List<Vector3>();
        List<int> coastTriangles = new List<int>();

        // Ocean
        Mesh oceanMesh = new Mesh();
        List<Vector3> oceanVertices = new List<Vector3>();
        List<int> oceanTriangles = new List<int>();

        // Beach
        Mesh beachMesh = new Mesh();
        List<Vector3> beachVertices = new List<Vector3>();
        List<int> beachTriangles = new List<int>();

        // Cycle through each cell
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Check each direction for edges
                AddEdges(x, y, coastVertices, coastTriangles, oceanVertices, oceanTriangles, mountainVertices, mountainTriangles, beachVertices, beachTriangles);
            }
        }

        coastMesh.vertices = coastVertices.ToArray();
        coastMesh.triangles = coastTriangles.ToArray();
        coastMesh.RecalculateNormals();

        oceanMesh.vertices = oceanVertices.ToArray();
        oceanMesh.triangles = oceanTriangles.ToArray();
        oceanMesh.RecalculateNormals();

        mountainMesh.vertices = mountainVertices.ToArray();
        mountainMesh.triangles = mountainTriangles.ToArray();
        mountainMesh.RecalculateNormals();

        beachMesh.vertices = beachVertices.ToArray();
        beachMesh.triangles = beachTriangles.ToArray();
        beachMesh.RecalculateNormals();

        return (coastMesh, oceanMesh, mountainMesh, beachMesh);
    }

    private void AddEdges(int x, int y, List<Vector3> coastVertices, List<int> coastTriangles, List<Vector3> oceanVertices, List<int> oceanTriangles, List<Vector3> mountainVertices, List<int> mountainTriangles, List<Vector3> beachVertices, List<int> beachTriangles)
    {
        int size = grid.GetLength(0);
        if (x > 0)
            AddEdgeIfDifferent(x, y, Vector2Int.left, coastVertices, coastTriangles, oceanVertices, oceanTriangles, mountainVertices, mountainTriangles, beachVertices, beachTriangles);
        if (x < size - 1)
            AddEdgeIfDifferent(x, y, Vector2Int.right, coastVertices, coastTriangles, oceanVertices, oceanTriangles, mountainVertices, mountainTriangles, beachVertices, beachTriangles);
        if (y > 0)
            AddEdgeIfDifferent(x, y, Vector2Int.down, coastVertices, coastTriangles, oceanVertices, oceanTriangles, mountainVertices, mountainTriangles, beachVertices, beachTriangles);
        if (y < size - 1)
            AddEdgeIfDifferent(x, y, Vector2Int.up, coastVertices, coastTriangles, oceanVertices, oceanTriangles, mountainVertices, mountainTriangles, beachVertices, beachTriangles);

    }
    private void AddEdgeIfDifferent(int x, int y, Vector2Int direction,
    List<Vector3> coastVertices, List<int> coastTriangles,
    List<Vector3> oceanVertices, List<int> oceanTriangles,
    List<Vector3> mountainVertices, List<int> mountainTriangles,
    List<Vector3> beachVertices, List<int> beachTriangles)
    {
        Cell cell = grid[x, y];
        float cellHeight = GetHeightForTerrainType(cell.currentTerrainType);
        int nx = x + direction.x;
        int ny = y + direction.y;
        int size = grid.GetLength(0);

        // Declare neighbor and edgeVertices here, so they are accessible throughout the method
        Cell neighbor = null;
        Vector3[] edgeVertices = null;

        // Check bounds and define neighbor
        if (nx >= 0 && nx < size && ny >= 0 && ny < size)
        {
            neighbor = grid[nx, ny];
        }

        // If neighbor is defined, proceed with edge checks
        if (neighbor != null)
        {
            float neighborHeight = GetHeightForTerrainType(neighbor.currentTerrainType);

            // Could be modified to account for ocean and mountain edges like so:
            bool isOceanEdge = IsOceanEdge(cell, neighbor);
            bool isMountainEdge = IsMountainEdge(cell, neighbor);

            // Calculate edge vertices
            edgeVertices = CalculateEdgeVertices(x, y, cellHeight, neighborHeight, direction, isOceanEdge, false);

            // Determine the type of edge and add the appropriate vertices and triangles
            if (cell.currentTerrainType != neighbor.currentTerrainType) // Simplify the checks by using a single condition for different terrain types
            {
                if (IsCoastEdge(cell, neighbor))
                {
                    AddVerticesAndTriangles(edgeVertices, coastVertices, coastTriangles);
                }
                else if (IsOceanEdge(cell, neighbor))
                {
                    AddVerticesAndTriangles(edgeVertices, oceanVertices, oceanTriangles);
                }
                else if (IsBeachEdge(cell, neighbor))
                {
                    AddVerticesAndTriangles(edgeVertices, beachVertices, beachTriangles);
                }
                else if (IsMountainEdge(cell, neighbor))
                {
                    AddVerticesAndTriangles(edgeVertices, mountainVertices, mountainTriangles);
                }
            }
        }
    }

    // You would need to define the IsCoastEdge, IsOceanEdge, and IsMountainEdge methods similar to IsBeachEdge
    private bool IsCoastEdge(Cell cell, Cell neighbor)
    {
        return (cell.currentTerrainType == Cell.TerrainType.Land && neighbor.currentTerrainType == Cell.TerrainType.Water) ||
               (cell.currentTerrainType == Cell.TerrainType.Water && neighbor.currentTerrainType == Cell.TerrainType.Land);
    }

    private bool IsOceanEdge(Cell cell, Cell neighbor)
    {
        return (cell.currentTerrainType == Cell.TerrainType.Water && neighbor.currentTerrainType == Cell.TerrainType.Deep) ||
               (cell.currentTerrainType == Cell.TerrainType.Deep && neighbor.currentTerrainType == Cell.TerrainType.Water);
    }

    private bool IsMountainEdge(Cell cell, Cell neighbor)
    {
        return (cell.currentTerrainType == Cell.TerrainType.Land && neighbor.currentTerrainType == Cell.TerrainType.Mountain) ||
               (cell.currentTerrainType == Cell.TerrainType.Mountain && neighbor.currentTerrainType == Cell.TerrainType.Land);
    }



    private void AddVerticesAndTriangles(Vector3[] edgeVertices, List<Vector3> vertices, List<int> triangles)
    {
        int currentVertexCount = vertices.Count;
        foreach (var vertex in edgeVertices)
        {
            vertices.Add(vertex);
            triangles.Add(currentVertexCount++);
        }
    }

    // Define the IsBeachEdge method to determine if an edge is a beach edge
    private bool IsBeachEdge(Cell cell, Cell neighbor)
    {
        // Implement logic to determine if an edge is a beach edge
        // For example, if one cell is Land and the other is Water
        return (cell.currentTerrainType == TerrainType.Land && neighbor.currentTerrainType == TerrainType.Beach) ||
               (cell.currentTerrainType == TerrainType.Beach && neighbor.currentTerrainType == TerrainType.Water);
    }

    private Vector3[] CalculateEdgeVertices(int x, int y, float cellHeight, float neighborHeight, Vector2Int direction, bool isOceanEdge = false, bool isMountainEdge = false)
    {

        // Initialize the edge vertices array
        Vector3[] edgeVertices = new Vector3[6]; // 6 vertices for two triangles forming a quad
                                                 
        // Heights before adjustments
        float originalCellHeight = cellHeight;
        float originalNeighborHeight = neighborHeight;

        // Adjust the heights for ocean edge if necessary
        if (isOceanEdge)
        {
            // Assuming ocean is always lower than the coast, reduce the neighborHeight by 1 unit
            neighborHeight -= 1.0f;
            cellHeight -= 1.0f;
        }

        // Adjust heights for mountain edge if needed
        if (isMountainEdge)
        {
            float mountainHeightAdjustment = 1.0f; // Define as 1 unit higher than land
            cellHeight += mountainHeightAdjustment;
            neighborHeight += mountainHeightAdjustment;
        }

        // Calculate the edge vertices based on the direction
        if (direction == Vector2Int.left)
        {
            edgeVertices[0] = new Vector3(x - 0.5f, cellHeight, y + 0.5f);
            edgeVertices[1] = new Vector3(x - 0.5f, cellHeight, y - 0.5f);
            edgeVertices[2] = new Vector3(x - 0.5f, neighborHeight, y + 0.5f);
            edgeVertices[3] = edgeVertices[1]; // Reuse the second vertex
            edgeVertices[4] = new Vector3(x - 0.5f, neighborHeight, y - 0.5f);
            edgeVertices[5] = edgeVertices[2]; // Reuse the third vertex
        }
        else if (direction == Vector2Int.right)
        {
            edgeVertices[0] = new Vector3(x + 0.5f, cellHeight, y - 0.5f);
            edgeVertices[1] = new Vector3(x + 0.5f, cellHeight, y + 0.5f);
            edgeVertices[2] = new Vector3(x + 0.5f, neighborHeight, y - 0.5f);
            edgeVertices[3] = edgeVertices[1]; // Reuse the second vertex
            edgeVertices[4] = new Vector3(x + 0.5f, neighborHeight, y + 0.5f);
            edgeVertices[5] = edgeVertices[2]; // Reuse the third vertex
        }
        else if (direction == Vector2Int.down)
        {
            edgeVertices[0] = new Vector3(x - 0.5f, cellHeight, y - 0.5f);
            edgeVertices[1] = new Vector3(x + 0.5f, cellHeight, y - 0.5f);
            edgeVertices[2] = new Vector3(x - 0.5f, neighborHeight, y - 0.5f);
            edgeVertices[3] = edgeVertices[1]; // Reuse the second vertex
            edgeVertices[4] = new Vector3(x + 0.5f, neighborHeight, y - 0.5f);
            edgeVertices[5] = edgeVertices[2]; // Reuse the third vertex
        }
        else if (direction == Vector2Int.up)
        {
            edgeVertices[0] = new Vector3(x + 0.5f, cellHeight, y + 0.5f);
            edgeVertices[1] = new Vector3(x - 0.5f, cellHeight, y + 0.5f);
            edgeVertices[2] = new Vector3(x + 0.5f, neighborHeight, y + 0.5f);
            edgeVertices[3] = edgeVertices[1]; // Reuse the second vertex
            edgeVertices[4] = new Vector3(x - 0.5f, neighborHeight, y + 0.5f);
            edgeVertices[5] = edgeVertices[2]; // Reuse the third vertex
        }
        else
        {
            // If direction is not recognized, return an empty array
            return new Vector3[0];
        }

        // Reverse the triangle order for the ocean edge to correct normals, if it is indeed an ocean edge.
        if (isOceanEdge)
        {
            return new Vector3[]
            {
            edgeVertices[0], edgeVertices[2], edgeVertices[1], // Reversed
            edgeVertices[1], edgeVertices[2], edgeVertices[4]  // Reversed
            };
        }
        

        return edgeVertices;
    }

    private float GetHeightForTerrainType(Cell.TerrainType type)
    {
        // Define the height for each terrain type
        switch (type)
        {
            case Cell.TerrainType.Land:
                return 0f;
            case Cell.TerrainType.Water:
                return -1f; 
            case Cell.TerrainType.Mountain:
                return 1f; 
            default:
                return 0f;
        }
    }
}

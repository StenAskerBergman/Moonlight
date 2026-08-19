using UnityEngine;
using System.Collections.Generic;
using static Cell;

public class EdgeMeshBuilder
{
    // Grid Ref.
    private Cell[,] grid;

    // Constructor
    public EdgeMeshBuilder(Cell[,] grid)
    {
        this.grid = grid;
    }

    // Build
    public (Mesh coastMesh, Mesh oceanMesh, Mesh mountainMesh, Mesh beachMesh, Mesh shallowMesh, Mesh deepMesh, Mesh plateauMesh, Mesh abyssalMesh) Build()
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

        // Shallow
        Mesh shallowMesh = new Mesh();
        List<Vector3> shallowVertices = new List<Vector3>();
        List<int> shallowTriangles = new List<int>();

        // Deep
        Mesh deepMesh = new Mesh();
        List<Vector3> deepVertices = new List<Vector3>();
        List<int> deepTriangles = new List<int>();

        // Plateau
        Mesh plateauMesh = new Mesh();
        List<Vector3> plateauVertices = new List<Vector3>();
        List<int> plateauTriangles = new List<int>();

        // Abyssal
        Mesh abyssalMesh = new Mesh();
        List<Vector3> abyssalVertices = new List<Vector3>();
        List<int> abyssalTriangles = new List<int>();

        // Maybe? - Get the height from the cell's position, which should be set correctly in the Grid - Edit: New Name MapGrid
        // float cellHeight = 0f;

        // Cycle through each cell
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Check each direction for edges
                AddEdges(x, y, 
                    coastVertices,      coastTriangles,     // Coast
                    oceanVertices,      oceanTriangles,     // Ocean
                    mountainVertices,   mountainTriangles,  // Mountain
                    beachVertices,      beachTriangles,     // Beach
                    shallowVertices,    shallowTriangles,   // Shallow
                    deepVertices,       deepTriangles,      // Deep
                    plateauVertices,    plateauTriangles,   // Plateau
                    abyssalVertices,    abyssalTriangles    // Abyssal
                );
            }
        }

        #region Set mesh vertices and triangles

        // Coast
        coastMesh.vertices = coastVertices.ToArray();
        coastMesh.triangles = coastTriangles.ToArray();
        coastMesh.RecalculateNormals();

        // Ocean
        oceanMesh.vertices = oceanVertices.ToArray();
        oceanMesh.triangles = oceanTriangles.ToArray();
        oceanMesh.RecalculateNormals();

        // Mountain
        mountainMesh.vertices = mountainVertices.ToArray();
        mountainMesh.triangles = mountainTriangles.ToArray();
        mountainMesh.RecalculateNormals();

        // Beach
        beachMesh.vertices = beachVertices.ToArray();
        beachMesh.triangles = beachTriangles.ToArray();
        beachMesh.RecalculateNormals();

        // Shallow
        shallowMesh.vertices = shallowVertices.ToArray();
        shallowMesh.triangles = shallowTriangles.ToArray();
        shallowMesh.RecalculateNormals();

        // Deep
        deepMesh.vertices = deepVertices.ToArray();
        deepMesh.triangles = deepTriangles.ToArray();
        deepMesh.RecalculateNormals();

        // Plateau
        plateauMesh.vertices = plateauVertices.ToArray();
        plateauMesh.triangles = plateauTriangles.ToArray();
        plateauMesh.RecalculateNormals();

        // Abssal
        abyssalMesh.vertices = abyssalVertices.ToArray();
        abyssalMesh.triangles = abyssalTriangles.ToArray();
        abyssalMesh.RecalculateNormals();

        #endregion

        return (coastMesh, oceanMesh, mountainMesh, beachMesh, shallowMesh, deepMesh, plateauMesh, abyssalMesh);
    }

    private void AddEdges(int x, int y, 
        List<Vector3> coastVertices,    List<int> coastTriangles, 
        List<Vector3> oceanVertices,    List<int> oceanTriangles, 
        List<Vector3> mountainVertices, List<int> mountainTriangles, 
        List<Vector3> beachVertices,    List<int> beachTriangles, 
        List<Vector3> shallowVertices,  List<int> shallowTriangles, 
        List<Vector3> deepVertices,     List<int> deepTriangles, 
        List<Vector3> plateauVertices,  List<int> plateauTriangles, 
        List<Vector3> abyssalVertices,  List<int> abyssalTriangles 
        )
    {
        int size = grid.GetLength(0);
        if (x > 0)
            AddEdgeIfDifferent(x, y, Vector2Int.left, 
                coastVertices, coastTriangles, 
                oceanVertices, oceanTriangles, 
                mountainVertices, mountainTriangles, 
                beachVertices, beachTriangles, 
                shallowVertices, shallowTriangles, 
                deepVertices, deepTriangles, 
                plateauVertices, plateauTriangles, 
                abyssalVertices, abyssalTriangles);
        if (x < size - 1)
            AddEdgeIfDifferent(x, y, Vector2Int.right, 
                coastVertices, coastTriangles, 
                oceanVertices, oceanTriangles, 
                mountainVertices, mountainTriangles, 
                beachVertices, beachTriangles, 
                shallowVertices, shallowTriangles, 
                deepVertices, deepTriangles, 
                plateauVertices, plateauTriangles, 
                abyssalVertices, abyssalTriangles);
        if (y > 0)
            AddEdgeIfDifferent(x, y, Vector2Int.down,
                coastVertices, coastTriangles,
                oceanVertices, oceanTriangles,
                mountainVertices, mountainTriangles,
                beachVertices, beachTriangles,
                shallowVertices, shallowTriangles,
                deepVertices, deepTriangles,
                plateauVertices, plateauTriangles,
                abyssalVertices, abyssalTriangles); 
        if (y < size - 1)
            AddEdgeIfDifferent(x, y, Vector2Int.up,
                coastVertices, coastTriangles,
                oceanVertices, oceanTriangles,
                mountainVertices, mountainTriangles,
                beachVertices, beachTriangles,
                shallowVertices, shallowTriangles,
                deepVertices, deepTriangles,
                plateauVertices, plateauTriangles,
                abyssalVertices, abyssalTriangles);
    }

    private void AddEdgeIfDifferent(int x, int y, Vector2Int direction,
        List<Vector3> coastVertices,    List<int> coastTriangles,
        List<Vector3> oceanVertices,    List<int> oceanTriangles,
        List<Vector3> mountainVertices, List<int> mountainTriangles,
        List<Vector3> beachVertices,    List<int> beachTriangles,
        List<Vector3> shallowVertices,  List<int> shallowTriangles,
        List<Vector3> deepVertices,     List<int> deepTriangles,
        List<Vector3> plateauVertices,  List<int> plateauTriangles,
        List<Vector3> abyssalVertices,  List<int> abyssalTriangles
    )
    {
        Cell cell = grid[x, y];
        float cellHeight = GetEdgeHeightForTerrainType(cell.currentTerrainType);
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
            float neighborHeight = GetEdgeHeightForTerrainType(neighbor.currentTerrainType);

            // Could be modified to account for ocean and mountain edges like so:
            bool isOceanEdge = IsOceanEdge(cell, neighbor);
            bool isMountainEdge = IsMountainEdge(cell, neighbor);
            bool isShallowEdge = IsShallowEdge(cell, neighbor);
            bool isDeepEdge = IsDeepEdge(cell, neighbor);
            bool isPlateauEdge = IsPlateauEdge(cell, neighbor);
            bool isAbyssalEdge = IsAbyssalEdge(cell, neighbor);

            // Calculate edge vertices
            edgeVertices = CalculateEdgeVertices(x, y, cellHeight, neighborHeight, direction, isOceanEdge, isAbyssalEdge, isPlateauEdge, isMountainEdge);
            // private Vector3[] CalculateEdgeVertices(int x, int y, float cellHeight, float neighborHeight, Vector2Int direction, bool isOceanEdge = false, bool isMountainEdge = false, bool isBeachEdge = false)

            // bool _log = true;

            // Determine the type of edge and add the appropriate vertices and triangles
            if (cell.currentTerrainType != neighbor.currentTerrainType) // Simplify the checks by using a single condition for different terrain types
            {
                if (IsCoastEdge(cell, neighbor))
                {
                    AddVerticesAndTriangles(edgeVertices, coastVertices, coastTriangles);
                }
                
                if (IsOceanEdge(cell, neighbor))
                {
                    AddVerticesAndTriangles(edgeVertices, oceanVertices, oceanTriangles);
                }
                
                if (IsBeachEdge(cell, neighbor))
                {
                    AddVerticesAndTriangles(edgeVertices, beachVertices, beachTriangles);
                }
                
                if (IsShallowEdge(cell, neighbor))
                {
                    AddVerticesAndTriangles(edgeVertices, shallowVertices, shallowTriangles);
                }
                
                if (IsDeepEdge(cell, neighbor))
                {
                    AddVerticesAndTriangles(edgeVertices, deepVertices, deepTriangles);
                }
                
                if (IsPlateauEdge(cell, neighbor))
                {
                    AddVerticesAndTriangles(edgeVertices, plateauVertices, plateauTriangles);
                }
                
                if (IsAbyssalEdge(cell, neighbor)) // deepest edge
                {
                    AddVerticesAndTriangles(edgeVertices, abyssalVertices, abyssalTriangles);
                }
                
                if (IsMountainEdge(cell, neighbor)) // highest edge
                {
                    AddVerticesAndTriangles(edgeVertices, mountainVertices, mountainTriangles);
                }
            }
        }
    }

    /*
    // CONDITIONS FOR WALLS 

    // HOW IT WORKS

    // RETURN: On Conditions

        // return Touch Condition X OR
        //        Touch Condition Y

    // > CONDITION: YX |OR| XY

        // Condition X AND Y OR
        // Condition Y AND X
        
        // X & Y
        // OR
        // Y & X

    // > CONDITION: XY |or| YX

        // Condition X AND Z OR
        // Condition Y AND X
                
        // X & Z
        // OR
        // Y & X

    tldr: 

        Condition 1: Top connection
        Condition 2: Bot connection


    */


    // Layers

        // Land
        // Beach
        // - Water
        // Shallow
        // Deep
        // Plateau
        // Abyssal

    //
    // Single Row Conditions
    //

    // IsMountainEdge - From Land, to Mountain
    private bool IsMountainEdge(Cell cell, Cell neighbor)
    {
        return (cell.currentTerrainType == Cell.TerrainType.Land && neighbor.currentTerrainType == Cell.TerrainType.Mountain) ||
                (cell.currentTerrainType == Cell.TerrainType.Mountain && neighbor.currentTerrainType == Cell.TerrainType.Land);
    }
    
    // IsBeachEdge - Beach to Water
    private bool IsBeachEdge(Cell cell, Cell neighbor)
    {
        return (cell.currentTerrainType == TerrainType.Land && neighbor.currentTerrainType == TerrainType.Beach) ||
                (cell.currentTerrainType == TerrainType.Beach && neighbor.currentTerrainType == TerrainType.Water);
    }

    // IsShallowEdge - Water to Shallow
    private bool IsShallowEdge(Cell cell, Cell neighbor)
    {
        return (cell.currentTerrainType == TerrainType.Water && neighbor.currentTerrainType == TerrainType.Shallow) ||
                (cell.currentTerrainType == TerrainType.Shallow && neighbor.currentTerrainType == TerrainType.Water);
    }

    // IsPlateauEdge - from Plateau to abyss
    private bool IsPlateauEdge(Cell cell, Cell neighbor)
    {
        return (cell.currentTerrainType == TerrainType.Deep && neighbor.currentTerrainType == TerrainType.Plateau) ||
                (cell.currentTerrainType == TerrainType.Plateau && neighbor.currentTerrainType == TerrainType.Abyssal);
    }

    // IsAbyssalEdge    
    private bool IsAbyssalEdge(Cell cell, Cell neighbor)
    {
        return (cell.currentTerrainType == TerrainType.Deep && neighbor.currentTerrainType == TerrainType.Abyssal) ||
                (cell.currentTerrainType == TerrainType.Abyssal && neighbor.currentTerrainType == TerrainType.Deep);
    }

    //
    // Double Row Conditions
    // 

    // IsDeepEdge - from Shallow, to Deep
    private bool IsDeepEdge(Cell cell, Cell neighbor)
    {
        return (cell.currentTerrainType == TerrainType.Shallow && neighbor.currentTerrainType == TerrainType.Deep) ||
                (cell.currentTerrainType == TerrainType.Deep && neighbor.currentTerrainType == TerrainType.Shallow);
    }

    // IsOceanEdge - from Water, over Shallow, to Deep
    private bool IsOceanEdge(Cell cell, Cell neighbor)
    {
        return (cell.currentTerrainType == Cell.TerrainType.Water && neighbor.currentTerrainType == Cell.TerrainType.Deep) ||
                (cell.currentTerrainType == Cell.TerrainType.Deep && neighbor.currentTerrainType == Cell.TerrainType.Water);
    }

    // IsCoastEdge - from Beach, over Water, to Shallow - Should Work
    // IsCoastEdge - from Land, over Water, to Shallow - Should Work

    private bool IsCoastEdge(Cell cell, Cell neighbor)
    {
               // Beach to Shallow
        return (cell.currentTerrainType == Cell.TerrainType.Beach && neighbor.currentTerrainType == Cell.TerrainType.Shallow) || // SHALLOW to WATER
               (cell.currentTerrainType == Cell.TerrainType.Shallow && neighbor.currentTerrainType == Cell.TerrainType.Beach) || // WATER to SHALLOW

                // Land to Shallow
                (cell.currentTerrainType == Cell.TerrainType.Land && neighbor.currentTerrainType == Cell.TerrainType.Shallow) || // BEACH to SHALLOW 
                (cell.currentTerrainType == Cell.TerrainType.Shallow && neighbor.currentTerrainType == Cell.TerrainType.Land);  // BEACH to WATER
    }
    
    // IsCoastEdge - from Beach, over Water, to Shallow
    private bool IsCoastEdge2(Cell cell, Cell neighbor)
    {
                // s1 w1 b0
        return (cell.currentTerrainType == Cell.TerrainType.Beach && neighbor.currentTerrainType == Cell.TerrainType.Shallow) || // SHALLOW to WATER
               (cell.currentTerrainType == Cell.TerrainType.Shallow && neighbor.currentTerrainType == Cell.TerrainType.Beach) || // WATER to SHALLOW

                // s2 w1 b1 
                (cell.currentTerrainType == Cell.TerrainType.Land && neighbor.currentTerrainType == Cell.TerrainType.Shallow) || // BEACH to SHALLOW 
                (cell.currentTerrainType == Cell.TerrainType.Shallow && neighbor.currentTerrainType == Cell.TerrainType.Land) || // SHALLOW to BEACH

                // s2 w2 b2 
                (cell.currentTerrainType == Cell.TerrainType.Water && neighbor.currentTerrainType == Cell.TerrainType.Beach) || // WATER to BEACH
                (cell.currentTerrainType == Cell.TerrainType.Beach && neighbor.currentTerrainType == Cell.TerrainType.Water); // BEACH to WATER
    }

    // Double Edge Conditions
    // Ocean
    // = From Water, Over Shallow, to Deep ||
    // = From Deep, Over Shallow, to Water ||

    // Coast
    // = From Shallow over Water to Beach ||
    // = From Beach over Water to Shallow ||

        // = From Water, to Shallow
        // = From Shallow, to Beach
        // = From Beach, to Water

    private void AddVerticesAndTriangles(Vector3[] edgeVertices, List<Vector3> vertices, List<int> triangles)
    {
        int currentVertexCount = vertices.Count;
        foreach (var vertex in edgeVertices)
        {
            vertices.Add(vertex);
            triangles.Add(currentVertexCount++);
        }
    }

    // > FACE DIRECTION
    // THIS METHOD DETERMINS THE EDGE VERTICES - FACE DIRECTION - TRIANGLES
    private Vector3[] CalculateEdgeVertices(int x, int y, float cellHeight, float neighborHeight, Vector2Int direction, bool isOceanEdge = false, bool isAbyssalEdge = false, bool isPlateauEdge = false, bool isMountainEdge = false, bool isBeachEdge = false)
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
            neighborHeight -= 0.0f;
            cellHeight -= 0.0f;
        }

        // Adjust the heights for Plateau edge if necessary
        if (isPlateauEdge)
        {
            neighborHeight += 0.0f;
            cellHeight += 0.0f;
        }

        // Adjust the heights for abyssal edge if necessary
        if (isAbyssalEdge)
        {
            neighborHeight -= 0.0f;
            cellHeight -= 0.0f;
        }

        // (if required)
        // Adjust heights for mountain edge if needed 
        if (isMountainEdge)
        {
            float mountainHeightAdjustment = 0.0f; // Define as 1 unit higher than land
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

        // Directional Face Code for future reference
        //// Local variables
        //bool _log = false;
        //bool _invert = false;
        //
        //// Reverse the triangle order for the ocean edge to correct normals, if it is indeed an ocean edge.
        //if (isOceanEdge && _invert)
        //{
        //    return new Vector3[]
        //    {
        //        edgeVertices[0], edgeVertices[2], edgeVertices[1], // Inwards
        //        edgeVertices[1], edgeVertices[2], edgeVertices[4]  // Inwards
        //    };
        //} 
        //else if (isOceanEdge)
        //{
        //    return new Vector3[]
        //    {
        //        edgeVertices[0], edgeVertices[1], edgeVertices[2], // Outwards
        //        edgeVertices[1], edgeVertices[2], edgeVertices[4]  // Outwards
        //    };
        //}
        //
        //
        //if (isPlateauEdge) Debug.Log("Plateau Edge Found!");
        //
        //if (isAbyssalEdge)
        //{
        //    if (_log) Debug.Log("Abyssmal Edge Found!");
        //
        //    return new Vector3[]
        //    {
        //        edgeVertices[0], edgeVertices[1], edgeVertices[2], // Outwards
        //        edgeVertices[1], edgeVertices[2], edgeVertices[4]  // Outwards
        //    };
        //}

        return edgeVertices;
    }

    // For some reason this plays into the edge placement way more than I expected
    private float GetEdgeHeightForTerrainType(Cell.TerrainType type)
    {
        // Define the height for each terrain type
        switch (type)
        {
            case Cell.TerrainType.Mountain:
                return 1f;
            case Cell.TerrainType.Land:
                return 0f;
            case Cell.TerrainType.Water:
                return -1f;
            case Cell.TerrainType.Coast:
                return -1.5f;
            case Cell.TerrainType.Shallow:
                return -2f;
            case Cell.TerrainType.Deep:
                return -3f;
            case Cell.TerrainType.Plateau:
                return -4f;
            case Cell.TerrainType.Abyssal:
                return -5f;

            default:
                return 0f;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextureBuilder
{
    private Cell[,] grid;

    public TextureBuilder(Cell[,] grid)
    {
        this.grid = grid;
    }

    public Texture2D Build()
    {
        // Create a new texture with size x size dimensions
        int size = grid.GetLength(0);
        Texture2D texture = new Texture2D(size, size);

        // Create a color map with size x size number of colors
        Color[] colorMap = new Color[size * size];

        Color unknownColor = new Color(0.5f, 0.5f, 0.5f, 1f); // Grey for Unknown
        Color noneColor = new Color(1f, 1f, 1f, 1f); // White for None

        // Water Types
        Color riverColor = new Color(0f, 0f, 1f, 1f); // Blue for River
        Color waterColor = new Color(0f, 0f, 0.8f, 1f); // Dark Blue for Water
        Color streamColor = new Color(0f, 0f, 0.9f, 1f); // Light Blue for Stream
        Color seaColor = new Color(0f, 0f, 0.7f, 1f); // Deep Blue for Sea
        Color oceanColor = new Color(0f, 0f, 0.6f, 1f); // Very Deep Blue for Ocean
        Color shallowColor = new Color(0f, 0.5f, 1f, 1f); // Cyan Blue for Shallow
        Color deepColor = new Color(0f, 0f, 0.5f, 1f); // Very Deep Blue for Deep
        Color plateauColor = new Color(0.5f, 0.5f, 0.8f, 1f); // Light Purple for Plateau

        // Terrain Types
        Color landColor = new Color(0.2f, 0.8f, 0.2f, 1f); // Green for Land
        Color shoreColor = new Color(0.8f, 0.8f, 0.2f, 1f); // Yellow-Green for Shore
        Color coastColor = new Color(0.6f, 0.6f, 0.2f, 1f); // Dark Yellow-Green for Coast
        Color desertColor = new Color(1f, 1f, 0f, 1f); // Yellow for Desert
        Color forestColor = new Color(0f, 0.5f, 0f, 1f); // Dark Green for Forest
        Color abyssColor = new Color(1f, 1f, 0.4f, 1f); // Dark Yellow for Abyss
        Color beachColor = new Color(1f, 1f, 0.6f, 1f); // Light Yellow for Beach
        Color shorefloorColor = new Color(1f, 1f, 0.7f, 1f); // Light Yellow for Beach

        Color plainColor = new Color(0.2f, 0.6f, 0.2f, 1f); // Light Green for Plain
        Color rockyColor = new Color(0.5f, 0.5f, 0.5f, 1f); // Grey for Rocky

        // Set the texture's filter mode to Point
        texture.filterMode = FilterMode.Point;

        // Create a dictionary to map terrain types to colors
        Dictionary<Cell.TerrainType, Color> terrainColorMap = new Dictionary<Cell.TerrainType, Color>
        {
            {Cell.TerrainType.Unknown, unknownColor},
            {Cell.TerrainType.None, noneColor},
            {Cell.TerrainType.Sea, shorefloorColor},
            {Cell.TerrainType.Land, landColor},
            {Cell.TerrainType.Deep, beachColor},
            {Cell.TerrainType.Coast, coastColor},
            {Cell.TerrainType.Shore, beachColor},
            {Cell.TerrainType.Water, shorefloorColor},
            {Cell.TerrainType.Ocean, beachColor},
            {Cell.TerrainType.Beach, beachColor},
            {Cell.TerrainType.Plain, plainColor},
            {Cell.TerrainType.Rocky, rockyColor},
            {Cell.TerrainType.River, riverColor},
            {Cell.TerrainType.Desert, desertColor},
            {Cell.TerrainType.Forest, forestColor},
            {Cell.TerrainType.Stream, streamColor},
            {Cell.TerrainType.Shallow, beachColor},
            {Cell.TerrainType.Abyssal, beachColor},
            {Cell.TerrainType.Plateau, plateauColor},
            {Cell.TerrainType.Mountain, rockyColor},

        };

        // Iterate through the cells in the grid
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Get the current cell
                Cell cell = grid[x, y];

                // Determine the terrain type of the cell
                Cell.TerrainType terrainType = cell.currentTerrainType;

                // Check if the terrain type is in the dictionary
                if (terrainColorMap.ContainsKey(terrainType))
                {
                    // Set the corresponding color in the color map based on the dictionary
                    colorMap[y * size + x] = terrainColorMap[terrainType];
                }
                else
                {
                    // If the terrain type is not in the dictionary, set the color to a default
                    colorMap[y * size + x] = new Color(0.5f, 0.5f, 0.5f, 1f); // Default to gray
                                                                              // Optionally log a warning to the console
                    Debug.LogWarning($"Unmapped terrain type '{terrainType}' at {x},{y}. Defaulting to gray.");
                }
            }
        }

        // Set the texture's pixels to the color map
        texture.SetPixels(colorMap);

        // Apply the changes to the texture
        texture.Apply();

        return texture;
    }
}

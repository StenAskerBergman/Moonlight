using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class IslandTerrainProviderTests
{
    [Test]
    public void SameSeedProducesSameSemanticGrid()
    {
        TerrainGenerationSettings settings = new TerrainGenerationSettings();
        TerrainSample[,] first = new IslandTerrainProvider(settings, GridType.Type.Island, 48, 90210, settings.seed, UnityEngine.Vector2.zero)
            .GenerateGameplaySamples();
        TerrainSample[,] second = new IslandTerrainProvider(settings, GridType.Type.Island, 48, 90210, settings.seed, UnityEngine.Vector2.zero)
            .GenerateGameplaySamples();

        for (int z = 0; z < 48; z++)
        {
            for (int x = 0; x < 48; x++)
            {
                Assert.AreEqual(first[x, z].TerrainType, second[x, z].TerrainType);
                Assert.AreEqual(first[x, z].Height, second[x, z].Height);
                Assert.AreEqual(first[x, z].SourceValue, second[x, z].SourceValue);
                Assert.AreEqual(first[x, z].PlateauInfluence, second[x, z].PlateauInfluence);
            }
        }
    }

    [Test]
    public void DeliberatePlateausStayWithinConfiguredRegionBoundsAndAreNotAnnular()
    {
        const int size = 96;
        TerrainGenerationSettings settings = CreateRegionSettings(1, 2);
        TerrainSample[,] samples = new IslandTerrainProvider(settings, GridType.Type.Island, size, 1337, settings.seed, UnityEngine.Vector2.zero)
            .GenerateGameplaySamples();

        int componentCount = CountPlateauComponents(samples);
        Assert.GreaterOrEqual(componentCount, settings.underwaterPlateaus.minimumCount);
        Assert.LessOrEqual(componentCount, settings.underwaterPlateaus.maximumCount);
        Assert.IsTrue(HasRadialDirectionWithoutPlateau(samples),
            "Deliberate regions should not form a complete annular elevation band around the island.");
    }

    [Test]
    public void PlateauInteriorIsBroadContiguousFlatAndBuildable()
    {
        const int size = 96;
        TerrainGenerationSettings settings = CreateRegionSettings(1, 1);
        TerrainSample[,] samples = new IslandTerrainProvider(settings, GridType.Type.Island, size, 1337, settings.seed, UnityEngine.Vector2.zero)
            .GenerateGameplaySamples();
        Cell[,] cells = BuildCells(samples, settings.maxBuildableHeightVariance);

        int largestBuildableRegion = LargestBuildablePlateauComponent(cells);
        Assert.Greater(largestBuildableRegion, 20, "Expected a useful contiguous underwater industrial district.");

        foreach (Cell cell in cells)
        {
            if (!cell.IsBuildableUnderwaterPlateau) continue;
            Assert.IsTrue(cell.IsUnderwater);
            Assert.AreEqual(settings.underwaterPlateauHeight, cell.height);
        }
    }

    [Test]
    public void PlateauTransitionInterpolatesHeightWithoutBecomingBuildablePlateauTerrain()
    {
        TerrainGenerationSettings settings = CreateRegionSettings(1, 1);
        TerrainSample[,] samples = new IslandTerrainProvider(settings, GridType.Type.Island, 96, 1337, settings.seed, UnityEngine.Vector2.zero)
            .GenerateGameplaySamples();
        Cell[,] cells = BuildCells(samples, settings.maxBuildableHeightVariance);

        int transitionCount = 0;
        int steepTransitionCount = 0;
        for (int z = 0; z < samples.GetLength(1); z++)
        {
            for (int x = 0; x < samples.GetLength(0); x++)
            {
                TerrainSample sample = samples[x, z];
                if (sample.PlateauInfluence <= 0f || sample.PlateauInfluence >= 0.9999f) continue;

                transitionCount++;
                Assert.AreNotEqual(Cell.TerrainType.Plateau, sample.TerrainType);
                Assert.IsFalse(cells[x, z].IsBuildableUnderwaterPlateau);
                if (!cells[x, z].IsSlopeSuitableForBuilding) steepTransitionCount++;
            }
        }

        Assert.Greater(transitionCount, 0);
        Assert.Greater(steepTransitionCount, 0);
    }

    [Test]
    public void ZeroRegionConfigurationStillPreservesBuildableSurfaceLand()
    {
        TerrainGenerationSettings settings = CreateRegionSettings(0, 0);
        TerrainSample[,] samples = new IslandTerrainProvider(settings, GridType.Type.Island, 64, 77, settings.seed, UnityEngine.Vector2.zero)
            .GenerateGameplaySamples();

        foreach (TerrainSample sample in samples)
        {
            if (sample.TerrainType != Cell.TerrainType.Land) continue;

            Cell land = new Cell(new Vector3(0f, sample.Height, 0f), null, sample.TerrainType);
            land.SetTerrainMetrics(0f, settings.maxBuildableHeightVariance);
            Assert.IsTrue(land.IsBuildableSurface);
            return;
        }

        Assert.Fail("Expected the island generator to retain surface Land cells.");
    }

    [Test]
    public void StandalonePlateauHasFlatSubmergedTopAndDeepPerimeter()
    {
        const int size = 64;
        TerrainGenerationSettings settings = new TerrainGenerationSettings();
        TerrainSample[,] samples = new IslandTerrainProvider(settings, GridType.Type.Plateau, size, 42, settings.seed, UnityEngine.Vector2.zero)
            .GenerateGameplaySamples();

        int plateauCount = 0;
        int deepPerimeterCount = 0;
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                TerrainSample sample = samples[x, z];
                if (sample.TerrainType == Cell.TerrainType.Plateau)
                {
                    plateauCount++;
                    Assert.AreEqual(settings.underwaterPlateauHeight, sample.Height);
                    Assert.Less(sample.Height, 0f);
                }

                if ((x == 0 || z == 0 || x == size - 1 || z == size - 1)
                    && (sample.TerrainType == Cell.TerrainType.Deep || sample.TerrainType == Cell.TerrainType.Abyssal))
                {
                    deepPerimeterCount++;
                }
            }
        }

        Assert.Greater(plateauCount, size * size / 5);
        Assert.Greater(deepPerimeterCount, size);
    }

    [Test]
    public void CellBuildabilityDistinguishesFlatPlateauFromSteepBoundary()
    {
        Cell cell = new Cell(new Vector3(2f, -2.5f, 3f), null, Cell.TerrainType.Plateau);
        cell.SetDeliberatePlateauInfluence(1f);

        cell.SetTerrainMetrics(0.05f, 0.2f);
        Assert.IsTrue(cell.IsUnderwater);
        Assert.IsTrue(cell.IsBuildableFlatRegion);
        Assert.IsTrue(cell.IsBuildableUnderwaterPlateau);

        cell.SetTerrainMetrics(1.5f, 0.2f);
        Assert.IsFalse(cell.IsSlopeSuitableForBuilding);
        Assert.IsFalse(cell.IsBuildableUnderwaterPlateau);
    }

    [Test]
    public void TerrainMeshUsesAuthoritativeCellHeight()
    {
        Cell[,] grid = { { new Cell(new Vector3(0f, 1.75f, 0f), null, Cell.TerrainType.Hill) } };
        Mesh mesh = new TerrainMeshBuilder(grid).Build();

        foreach (Vector3 vertex in mesh.vertices)
        {
            Assert.AreEqual(1.75f, vertex.y);
        }
    }

    [Test]
    public void GridRequirementSelectsUnderwaterTerrainIndependentlyOfBuildingType()
    {
        GridRequirement requirement = ScriptableObject.CreateInstance<GridRequirement>();
        try
        {
            Cell plateau = new Cell(new Vector3(0f, -2.5f, 0f), null, Cell.TerrainType.Plateau);
            plateau.SetDeliberatePlateauInfluence(1f);
            plateau.SetTerrainMetrics(0f, 0.2f);
            requirement.gridType = GridRequirement.GridType.underwaterPlateau;
            requirement.SetTargetCell(plateau);
            Assert.IsTrue(requirement.IsSatisfied());

            requirement.gridType = GridRequirement.GridType.deep;
            requirement.SetTargetCell(new Cell(new Vector3(0f, -4f, 0f), null, Cell.TerrainType.Deep));
            Assert.IsTrue(requirement.IsSatisfied());

            requirement.gridType = GridRequirement.GridType.abyssal;
            requirement.SetTargetCell(new Cell(new Vector3(0f, -6f, 0f), null, Cell.TerrainType.Abyssal));
            Assert.IsTrue(requirement.IsSatisfied());

            requirement.gridType = GridRequirement.GridType.shallow;
            requirement.SetTargetCell(new Cell(new Vector3(0f, -1.4f, 0f), null, Cell.TerrainType.Shallow));
            Assert.IsTrue(requirement.IsSatisfied());
        }
        finally
        {
            Object.DestroyImmediate(requirement);
        }
    }

    private static TerrainGenerationSettings CreateRegionSettings(int minimumCount, int maximumCount)
    {
        TerrainGenerationSettings settings = new TerrainGenerationSettings();
        settings.underwaterPlateaus.minimumCount = minimumCount;
        settings.underwaterPlateaus.maximumCount = maximumCount;
        settings.underwaterPlateaus.minimumRadius = 6f;
        settings.underwaterPlateaus.maximumRadius = 9f;
        settings.underwaterPlateaus.minimumAspectRatio = 0.7f;
        settings.underwaterPlateaus.maximumAspectRatio = 0.95f;
        settings.underwaterPlateaus.minimumInteriorRadius = 3f;
        settings.underwaterPlateaus.minimumPlacementDistance = 0.68f;
        settings.underwaterPlateaus.maximumPlacementDistance = 0.8f;
        settings.underwaterPlateaus.minimumRegionSeparation = 4f;
        settings.underwaterPlateaus.maximumSurfaceOverlap = 0.15f;
        settings.underwaterPlateaus.candidateAttemptsPerRegion = 96;
        return settings;
    }

    private static Cell[,] BuildCells(TerrainSample[,] samples, float maxBuildableVariance)
    {
        int width = samples.GetLength(0);
        int height = samples.GetLength(1);
        Cell[,] cells = new Cell[width, height];

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                TerrainSample sample = samples[x, z];
                cells[x, z] = new Cell(new Vector3(x, sample.Height, z), null, sample.TerrainType);
                cells[x, z].SetDeliberatePlateauInfluence(sample.PlateauInfluence);
            }
        }

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                cells[x, z].UpdateNeighbors(cells, width);
            }
        }

        foreach (Cell cell in cells)
        {
            float variance = 0f;
            foreach (Cell neighbor in cell.neighbors)
            {
                variance = Mathf.Max(variance, Mathf.Abs(cell.height - neighbor.height));
            }
            cell.SetTerrainMetrics(variance, maxBuildableVariance);
        }

        return cells;
    }

    private static int CountPlateauComponents(TerrainSample[,] samples)
    {
        return CountComponents(
            samples.GetLength(0),
            samples.GetLength(1),
            (x, z) => samples[x, z].TerrainType == Cell.TerrainType.Plateau,
            out _);
    }

    private static int LargestBuildablePlateauComponent(Cell[,] cells)
    {
        CountComponents(
            cells.GetLength(0),
            cells.GetLength(1),
            (x, z) => cells[x, z].IsBuildableUnderwaterPlateau,
            out int largest);
        return largest;
    }

    private static int CountComponents(
        int width,
        int height,
        System.Func<int, int, bool> includes,
        out int largest)
    {
        bool[,] visited = new bool[width, height];
        Vector2Int[] directions =
        {
            Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down
        };
        int componentCount = 0;
        largest = 0;

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (visited[x, z] || !includes(x, z)) continue;

                componentCount++;
                int componentSize = 0;
                Queue<Vector2Int> pending = new Queue<Vector2Int>();
                pending.Enqueue(new Vector2Int(x, z));
                visited[x, z] = true;

                while (pending.Count > 0)
                {
                    Vector2Int point = pending.Dequeue();
                    componentSize++;

                    foreach (Vector2Int direction in directions)
                    {
                        Vector2Int next = point + direction;
                        if (next.x < 0 || next.x >= width || next.y < 0 || next.y >= height) continue;
                        if (visited[next.x, next.y] || !includes(next.x, next.y)) continue;
                        visited[next.x, next.y] = true;
                        pending.Enqueue(next);
                    }
                }

                largest = Mathf.Max(largest, componentSize);
            }
        }

        return componentCount;
    }

    private static bool HasRadialDirectionWithoutPlateau(TerrainSample[,] samples)
    {
        float center = (samples.GetLength(0) - 1f) * 0.5f;
        float maximumRadius = center;

        for (int ray = 0; ray < 48; ray++)
        {
            float angle = ray / 48f * Mathf.PI * 2f;
            bool foundPlateau = false;

            for (float radius = 0f; radius <= maximumRadius; radius += 0.5f)
            {
                int x = Mathf.RoundToInt(center + Mathf.Cos(angle) * radius);
                int z = Mathf.RoundToInt(center + Mathf.Sin(angle) * radius);
                if (x < 0 || x >= samples.GetLength(0) || z < 0 || z >= samples.GetLength(1)) break;
                if (samples[x, z].TerrainType == Cell.TerrainType.Plateau)
                {
                    foundPlateau = true;
                    break;
                }
            }

            if (!foundPlateau) return true;
        }

        return false;
    }

}

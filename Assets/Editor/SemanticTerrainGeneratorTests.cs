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
    public void IslandGenerationPreservesBuildableSurfaceLand()
    {
        TerrainGenerationSettings settings = new TerrainGenerationSettings();
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

}

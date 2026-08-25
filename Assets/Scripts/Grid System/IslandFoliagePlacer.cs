using UnityEngine;

public class IslandFoliagePlacer : MonoBehaviour
{
    public ClimateProfile climateProfile;

    private Transform foliageRoot;

    public void ScatterFoliage(Cell[,] grid)
    {
        if (climateProfile == null || climateProfile.treePrefabs == null || climateProfile.treePrefabs.Length == 0)
        {
            return;
        }

        // Clear existing foliage
        if (foliageRoot != null)
        {
            if (Application.isPlaying) Destroy(foliageRoot.gameObject);
            else DestroyImmediate(foliageRoot.gameObject);
        }

        int size = grid.GetLength(0);
        
        foliageRoot = new GameObject("Foliage").transform;
        foliageRoot.SetParent(transform, false);
        foliageRoot.localPosition = Vector3.zero;

        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                Cell cell = grid[x, z];
                bool isForest = cell.currentTerrainType == Cell.TerrainType.Forest;
                bool isGrass = cell.currentTerrainType == Cell.TerrainType.Land || cell.currentTerrainType == Cell.TerrainType.Plain;

                float spawnChance = isForest ? climateProfile.forestDensity : (isGrass ? climateProfile.plainsTreeDensity : 0f);

                if (spawnChance > 0f && Random.value <= spawnChance)
                {
                    Vector3 position = transform.position + cell.cellPosition;
                    
                    position.x += Random.Range(-0.4f, 0.4f);
                    position.z += Random.Range(-0.4f, 0.4f);
                    
                    GameObject prefab = climateProfile.treePrefabs[Random.Range(0, climateProfile.treePrefabs.Length)];
                    if (prefab != null)
                    {
                        GameObject tree = Instantiate(prefab, position, Quaternion.Euler(0, Random.Range(0, 360f), 0), foliageRoot);
                        
                        float scale = Random.Range(climateProfile.treeScaleMin, climateProfile.treeScaleMax);
                        tree.transform.localScale = new Vector3(scale, scale, scale);
                    }
                }
            }
        }
    }
}

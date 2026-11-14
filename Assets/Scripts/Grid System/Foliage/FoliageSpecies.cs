// tldr: Authoring data for one kind of plant - what it looks like, where it scatters, and what
// felling it is worth. Moves rules that used to be hardcoded in IslandFoliagePlacer into assets.
using UnityEngine;

[CreateAssetMenu(fileName = "New Foliage Species", menuName = "Terrain/Foliage Species")]
public class FoliageSpecies : ScriptableObject
{
    /// <summary>Per-terrain chance that a candidate cell of this terrain grows this species.</summary>
    [System.Serializable]
    public struct TerrainDensity
    {
        public Cell.TerrainType terrain;
        [Range(0f, 1f)] public float density;
    }

    [Header("Identity")]
    public string displayName = "Tree";
    public FoliageKind kind = FoliageKind.Tree;

    [Header("Visuals")]
    [Tooltip("Drawn for Sapling, Living and Dead. Must expose MeshRenderer/MeshFilter pairs; an " +
             "LODGroup is used LOD0-for-near, last-LOD-for-far by IslandFoliagePlacer.")]
    public GameObject prefab;
    [Tooltip("Drawn once the plant has been felled. Falls back to no visual when empty.")]
    public GameObject stumpPrefab;
    [Tooltip("Drawn for felled trunks lying on the ground. Falls back to no visual when empty.")]
    public GameObject logPrefab;

    [Header("Scatter")]
    [Tooltip("Relative likelihood of being chosen once a cell has decided to grow something.")]
    [Min(0f)] public float scatterWeight = 1f;
    [Tooltip("Chance per candidate cell, by terrain. Terrains absent from this list never grow it.")]
    public TerrainDensity[] terrainAffinity;
    [Tooltip("Reject cells steeper than this. IslandFoliagePlacer used a fixed 0.4 for every tree.")]
    [Range(0f, 1f)] public float slopeMax = 0.4f;
    [Tooltip("Reject cells nearer the shore than this, in cells.")]
    [Min(0f)] public float shoreDistanceMin = 3f;
    public float scaleMin = 0.8f;
    public float scaleMax = 1.3f;

    [Header("Forestry")]
    [Tooltip("Whether a logger may fell this.")]
    public bool harvestable = true;
    [Min(0)] public int woodYield = 1;
    [Min(0f)] public float fellSeconds = 4f;
    [Tooltip("Seconds a stump takes to become a sapling again. Zero disables regrowth.")]
    [Min(0f)] public float regrowSeconds = 90f;

    [Header("Player")]
    public bool playerPlantable = true;
    [Tooltip("How near the cursor must be to the trunk for a click to select this, in world units.")]
    [Min(0.05f)] public float pickRadius = 0.6f;

    /// <summary>
    /// Chance a candidate cell of this terrain grows this species; 0 when the terrain is not listed.
    /// </summary>
    public float DensityFor(Cell.TerrainType terrain)
    {
        if (terrainAffinity == null) return 0f;
        for (int i = 0; i < terrainAffinity.Length; i++)
            if (terrainAffinity[i].terrain == terrain) return Mathf.Clamp01(terrainAffinity[i].density);
        return 0f;
    }

    /// <summary>The prefab to draw for a given life stage.</summary>
    public GameObject PrefabFor(FoliageState state)
    {
        switch (state)
        {
            case FoliageState.Stump: return stumpPrefab;
            case FoliageState.Log: return logPrefab;
            default: return prefab;
        }
    }
}

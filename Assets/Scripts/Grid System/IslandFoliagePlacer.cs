// tldr: Places seeded life from cached terrain and renders land foliage in locally invalidated spatial batches.
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class IslandFoliagePlacer : MonoBehaviour
{
    private const string RootName = "Generated Foliage & Wildlife";
    public ClimateProfile climateProfile;
    [Min(0)] public int maximumLandInstances = 600;
    [Min(1f)] public float foliageLodDistance = 55f;
    [Min(1f)] public float foliageCullDistance = 180f;
    private TerrainSampleCache terrainCache;
    private readonly FoliageStore store = new FoliageStore();
    private readonly Dictionary<Vector2Int, Cell> subscribedCells = new Dictionary<Vector2Int, Cell>();
    private readonly Dictionary<Vector2Int, FoliagePatch> patches = new Dictionary<Vector2Int, FoliagePatch>();
    private readonly Dictionary<Material, Material> instancedMaterials = new Dictionary<Material, Material>();
    private readonly List<FoliageSpecies> generatedSpecies = new List<FoliageSpecies>();
    private readonly List<FoliageSpecies> speciesTable = new List<FoliageSpecies>();
    private readonly List<int> scratchIds = new List<int>();
    private const int PatchSize = 8;

    /// <summary>
    /// This island's foliage model. Every other system - construction clearance, the player's tools,
    /// forestry - mutates foliage through here or through <see cref="Plant"/>/<see cref="ClearCell"/>
    /// below; this component only draws whatever the store says exists.
    /// </summary>
    public FoliageStore Store => store;

    private sealed class FoliagePatch
    {
        public readonly List<int> Ids = new List<int>();
        public readonly List<FoliageBatch> Batches = new List<FoliageBatch>();
        public bool Dirty = true;
    }
    private sealed class FoliageBatch
    {
        public Mesh Mesh;
        public Material Material;
        public int Submesh;
        public bool Distant;
        public readonly List<Matrix4x4> Matrices = new List<Matrix4x4>();
        public Matrix4x4[] Upload;
    }

    private Transform foliageRoot;
    private readonly List<Material> generatedMaterials = new List<Material>();
    private readonly List<Mesh> generatedMeshes = new List<Mesh>();

    public void ScatterFoliage(Cell[,] grid, bool isStandalonePlateau, int seed, TerrainSampleCache cache = null)
    {
        ClearGeneratedLife();
        terrainCache = cache;
        if (grid == null) return;
        foliageRoot = new GameObject(RootName).transform;
        foliageRoot.SetParent(transform, false);
        System.Random random = new System.Random(unchecked(seed ^ 0x4A71C3));
        if (isStandalonePlateau && (climateProfile == null || climateProfile.populateUnderwaterPlateaus)) ScatterPlateauLife(grid, cache, random);
        if (climateProfile != null) ScatterLand(grid, seed);
    }

    private void ScatterLand(Cell[,] grid, int seed)
    {
        ResolveSpecies();
        if (terrainCache == null || speciesTable.Count == 0) return;
        // Per-cell streams keep placement stable when another cell becomes occupied or blocked.
        var candidates = new List<(Cell cell, int x, int z, int rank)>();
        for (int z = 0; z < grid.GetLength(1); z++)
        for (int x = 0; x < grid.GetLength(0); x++)
            candidates.Add((grid[x, z], x, z, unchecked((seed ^ x * 73856093 ^ z * 19349663) * 83492791)));
        candidates.Sort((a, b) => a.rank != b.rank ? a.rank.CompareTo(b.rank) : a.z != b.z ? a.z.CompareTo(b.z) : a.x.CompareTo(b.x));
        foreach (var candidate in candidates)
        {
            if (store.Count >= maximumLandInstances) break;
            Cell cell = candidate.cell;
            if (cell.isOccupied || cell.isBlocked || cell.isRoad || cell.isDeposit || !cell.IsBuildableSurface) continue;
            var random = new System.Random(unchecked(seed ^ candidate.x * 73856093 ^ candidate.z * 19349663));
            Vector3 local = cell.localCenter;
            local.x += Mathf.Lerp(-0.38f, 0.38f, Next(random));
            local.z += Mathf.Lerp(-0.38f, 0.38f, Next(random));
            int sx = Mathf.Clamp(Mathf.RoundToInt(local.x / terrainCache.Step), 0, terrainCache.Resolution - 1);
            int sz = Mathf.Clamp(Mathf.RoundToInt(local.z / terrainCache.Step), 0, terrainCache.Resolution - 1);
            int index = terrainCache.GetIndex(sx, sz);
            float slope = terrainCache.Slopes[index];
            float shore = terrainCache.ShoreDistances[index];
            var kind = terrainCache.TerrainTypes[index];
            // The densest eligible species decides whether the cell grows anything at all, then
            // scatterWeight decides which one it is. When every species shares one density - which is
            // what the legacy treePrefabs fallback produces - this is the old single-roll behaviour.
            float chance = 0f;
            for (int i = 0; i < speciesTable.Count; i++)
            {
                FoliageSpecies candidateSpecies = speciesTable[i];
                if (slope > candidateSpecies.slopeMax || shore < candidateSpecies.shoreDistanceMin) continue;
                chance = Mathf.Max(chance, candidateSpecies.DensityFor(kind));
            }
            if (chance <= 0f || Next(random) > chance) continue;
            FoliageSpecies species = PickSpecies(kind, slope, shore, random);
            if (species == null || species.prefab == null) continue;
            // Same triangle interpolation as the terrain mesh; no fresh noise or cell-height approximation.
            local.y = CachedHeight(local.x, local.z);
            float scale = Mathf.Lerp(species.scaleMin, species.scaleMax, Next(random));
            Plant(cell, species, local, Next(random) * 360f, scale, FoliageOrigin.Generated);
        }
    }

    /// <summary>
    /// Weighted choice among the species that tolerate this terrain, slope and shore distance.
    /// </summary>
    private FoliageSpecies PickSpecies(Cell.TerrainType kind, float slope, float shore, System.Random random)
    {
        float total = 0f;
        for (int i = 0; i < speciesTable.Count; i++)
        {
            FoliageSpecies species = speciesTable[i];
            if (slope > species.slopeMax || shore < species.shoreDistanceMin) continue;
            total += species.DensityFor(kind) * Mathf.Max(0f, species.scatterWeight);
        }
        if (total <= 0f) return null;

        float roll = Next(random) * total;
        for (int i = 0; i < speciesTable.Count; i++)
        {
            FoliageSpecies species = speciesTable[i];
            if (slope > species.slopeMax || shore < species.shoreDistanceMin) continue;
            roll -= species.DensityFor(kind) * Mathf.Max(0f, species.scatterWeight);
            if (roll <= 0f) return species;
        }
        return null;
    }

    /// <summary>
    /// Fills <see cref="speciesTable"/> from the climate profile, synthesising throwaway species from
    /// the legacy treePrefabs array when the profile has not been migrated yet, so an unmigrated
    /// profile still generates an island instead of an empty one.
    /// </summary>
    private void ResolveSpecies()
    {
        speciesTable.Clear();
        if (climateProfile == null) return;

        if (climateProfile.foliageSpecies != null)
            foreach (FoliageSpecies species in climateProfile.foliageSpecies)
                if (species != null && species.prefab != null) speciesTable.Add(species);
        if (speciesTable.Count > 0) return;

        if (climateProfile.treePrefabs == null) return;
        foreach (GameObject prefab in climateProfile.treePrefabs)
        {
            if (prefab == null) continue;
            FoliageSpecies species = ScriptableObject.CreateInstance<FoliageSpecies>();
            species.name = prefab.name;
            species.displayName = prefab.name;
            species.kind = FoliageKind.Tree;
            species.prefab = prefab;
            species.scaleMin = climateProfile.treeScaleMin;
            species.scaleMax = climateProfile.treeScaleMax;
            species.terrainAffinity = new[]
            {
                new FoliageSpecies.TerrainDensity { terrain = Cell.TerrainType.Forest, density = climateProfile.forestDensity },
                new FoliageSpecies.TerrainDensity { terrain = Cell.TerrainType.Land, density = climateProfile.plainsTreeDensity },
                new FoliageSpecies.TerrainDensity { terrain = Cell.TerrainType.Plain, density = climateProfile.plainsTreeDensity },
            };
            generatedSpecies.Add(species);
            speciesTable.Add(species);
        }
    }

    /// <summary>
    /// Adds one plant and makes sure its cell notifies us when a building takes it. This and
    /// <see cref="ClearCell"/> are the only places cell subscriptions are managed.
    /// </summary>
    public int Plant(Cell cell, FoliageSpecies species, Vector3 localPosition, float yaw, float scale,
                     FoliageOrigin origin, FoliageState state = FoliageState.Living)
    {
        if (cell == null || species == null) return 0;
        var coordinate = new Vector2Int(cell.cellPosition.x, cell.cellPosition.z);
        if (!subscribedCells.ContainsKey(coordinate))
        {
            subscribedCells.Add(coordinate, cell);
            cell.PlacementChanged += InvalidateCell;
        }
        return store.Add(store.RegisterSpecies(species), coordinate, localPosition, yaw, scale, origin, state);
    }

    /// <summary>Removes everything on a cell, reporting why. Building placement routes through here.</summary>
    public int ClearCell(Cell cell, FoliageRemovalCause cause)
    {
        if (cell == null) return 0;
        return store.RemoveAll(new Vector2Int(cell.cellPosition.x, cell.cellPosition.z), cause);
    }

    private float CachedHeight(float x, float z)
    {
        float fx = Mathf.Clamp(x / terrainCache.Step, 0f, terrainCache.Resolution - 1.001f);
        float fz = Mathf.Clamp(z / terrainCache.Step, 0f, terrainCache.Resolution - 1.001f);
        int ix = Mathf.FloorToInt(fx), iz = Mathf.FloorToInt(fz);
        float u = fx - ix, v = fz - iz;
        float a = terrainCache.GetHeight(ix, iz), b = terrainCache.GetHeight(ix + 1, iz);
        float c = terrainCache.GetHeight(ix, iz + 1), d = terrainCache.GetHeight(ix + 1, iz + 1);
        return v >= u ? a * (1f - v) + c * (v - u) + d * u : a * (1f - u) + b * (u - v) + d * v;
    }

    /// <summary>
    /// A building (or road, or terrain change) took this cell, so whatever grew on it is gone.
    /// Subscribed by <see cref="Plant"/> to <see cref="Cell.PlacementChanged"/>.
    /// </summary>
    public void InvalidateCell(Cell cell) => ClearCell(cell, FoliageRemovalCause.Construction);

    public void InvalidateCells(IEnumerable<Cell> cells)
    {
        foreach (var cell in cells) InvalidateCell(cell);
    }

    private void OnEnable()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += RenderFoliage;
        store.Added += OnFoliageAdded;
        store.Removed += OnFoliageRemoved;
        store.StateChanged += OnFoliageStateChanged;
        RebuildPatchIndex();
    }

    private void OnDisable()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= RenderFoliage;
        store.Added -= OnFoliageAdded;
        store.Removed -= OnFoliageRemoved;
        store.StateChanged -= OnFoliageStateChanged;
    }

    private void OnFoliageAdded(FoliageInstance instance)
    {
        FoliagePatch patch = PatchFor(instance.Cell);
        patch.Ids.Add(instance.Id);
        patch.Dirty = true;
    }

    private void OnFoliageRemoved(FoliageInstance instance, FoliageRemovalCause cause)
    {
        if (patches.TryGetValue(PatchKey(instance.Cell), out FoliagePatch patch))
        {
            patch.Ids.Remove(instance.Id);
            patch.Dirty = true;
        }
        // Drop the cell subscription once nothing is left standing on it.
        if (subscribedCells.TryGetValue(instance.Cell, out Cell cell)
            && store.QueryCell(instance.Cell, scratchIds) == 0)
        {
            cell.PlacementChanged -= InvalidateCell;
            subscribedCells.Remove(instance.Cell);
        }
    }

    private void OnFoliageStateChanged(FoliageInstance instance, FoliageState state)
    {
        if (patches.TryGetValue(PatchKey(instance.Cell), out FoliagePatch patch)) patch.Dirty = true;
    }

    /// <summary>Rebuilds the render index from the store, so the view can never drift out of sync.</summary>
    private void RebuildPatchIndex()
    {
        foreach (FoliagePatch patch in patches.Values) { patch.Ids.Clear(); patch.Dirty = true; }
        IReadOnlyList<FoliageInstance> all = store.Instances;
        for (int i = 0; i < all.Count; i++) OnFoliageAdded(all[i]);
    }

    private static Vector2Int PatchKey(Vector2Int cell) => new Vector2Int(cell.x / PatchSize, cell.y / PatchSize);

    private FoliagePatch PatchFor(Vector2Int cell)
    {
        Vector2Int key = PatchKey(cell);
        if (!patches.TryGetValue(key, out FoliagePatch patch)) patches.Add(key, patch = new FoliagePatch());
        return patch;
    }

    private void RenderFoliage(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
    {
        if (camera == null || camera.cameraType == CameraType.Preview) return;
        foreach (var pair in patches)
        {
            var patch = pair.Value;
            if (patch.Ids.Count == 0) continue;
            Vector3 centre = transform.TransformPoint(new Vector3((pair.Key.x + 0.5f) * PatchSize, 0f, (pair.Key.y + 0.5f) * PatchSize));
            float distance = Vector3.Distance(camera.transform.position, centre);
            if (distance > foliageCullDistance) continue;
            if (patch.Dirty) RebuildPatch(patch);
            bool distant = distance > foliageLodDistance;
            foreach (var batch in patch.Batches)
            {
                if (batch.Distant != distant) continue;
                for (int i = 0; i < batch.Matrices.Count; i++) batch.Upload[i] = transform.localToWorldMatrix * batch.Matrices[i];
                if (SystemInfo.supportsInstancing && batch.Material.enableInstancing)
                    Graphics.DrawMeshInstanced(batch.Mesh, batch.Submesh, batch.Material, batch.Upload,
                        batch.Upload.Length, null, UnityEngine.Rendering.ShadowCastingMode.Off, true, gameObject.layer, camera);
                else
                    foreach (var matrix in batch.Upload) Graphics.DrawMesh(batch.Mesh, matrix, batch.Material,
                        gameObject.layer, camera, batch.Submesh, null, false, true);
            }
        }
    }

    private void RebuildPatch(FoliagePatch patch)
    {
        patch.Batches.Clear();
        for (int idIndex = 0; idIndex < patch.Ids.Count; idIndex++)
        {
            if (!store.TryGet(patch.Ids[idIndex], out FoliageInstance instance)) continue;
            FoliageSpecies species = store.GetSpecies(instance.SpeciesId);
            GameObject prefab = species != null ? species.PrefabFor(instance.State) : null;
            // A state with no authored prefab - a stump on a species that has none - simply draws nothing.
            if (prefab == null) continue;
            Matrix4x4 instanceMatrix = Matrix4x4.TRS(instance.LocalPosition,
                Quaternion.Euler(0f, instance.Yaw, 0f), prefab.transform.localScale * instance.Scale);

            var lod = prefab.GetComponentInChildren<LODGroup>();
            var levels = lod != null ? lod.GetLODs() : null;
            for (int mode = 0; mode < 2; mode++)
            {
                Renderer[] renderers = levels != null && levels.Length > 0
                    ? levels[mode == 0 ? 0 : levels.Length - 1].renderers
                    : prefab.GetComponentsInChildren<MeshRenderer>();
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    Mesh mesh = filter.sharedMesh;
                    var materials = renderer.sharedMaterials;
                    for (int sub = 0; sub < Mathf.Min(mesh.subMeshCount, materials.Length); sub++)
                    {
                        if (materials[sub] == null) continue;
                        if (!instancedMaterials.TryGetValue(materials[sub], out var material))
                        {
                            material = new Material(materials[sub]) { enableInstancing = true };
                            instancedMaterials.Add(materials[sub], material); generatedMaterials.Add(material);
                        }
                        var batch = patch.Batches.Find(b => b.Mesh == mesh && b.Material == material && b.Submesh == sub
                            && b.Distant == (mode == 1) && b.Matrices.Count < 1023);
                        if (batch == null)
                        {
                            batch = new FoliageBatch { Mesh = mesh, Material = material, Submesh = sub, Distant = mode == 1 };
                            patch.Batches.Add(batch);
                        }
                        batch.Matrices.Add(instanceMatrix * prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix);
                    }
                }
            }
        }
        foreach (var batch in patch.Batches) batch.Upload = new Matrix4x4[batch.Matrices.Count];
        patch.Dirty = false;
    }

    private void ScatterPlateauLife(Cell[,] grid, TerrainSampleCache cache, System.Random random)
    {
        List<Cell> allPlateauCells = new List<Cell>();
        List<Cell> kelpHabitatCells = new List<Cell>();
        List<Cell> coralHabitatCells = new List<Cell>();
        List<Cell> wildlifeHabitatCells = new List<Cell>();

        for (int z = 0; z < grid.GetLength(1); z++)
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            Cell cell = grid[x, z];
            if (cell.currentTerrainType != Cell.TerrainType.Plateau) continue;
            allPlateauCells.Add(cell);

            if (!cell.isDeposit && !cell.isOccupied && !cell.isBlocked)
            {
                PlateauSampleData data = SamplePlateauData(cache, cell.localCenter.x, cell.localCenter.z);
                if (data.IsDefined)
                {
                    // Prefer rocky rim collars & gravel seams for benthic beds while keeping
                    // the open central building ground clean and readable.
                    if (data.RockWeight > 0.36f && data.BuildableWeight < 0.28f)
                    {
                        coralHabitatCells.Add(cell);
                    }
                    else if ((data.GravelWeight > 0.14f || data.RockWeight * data.SandWeight > 0.08f)
                        && data.BuildableWeight < 0.55f)
                    {
                        kelpHabitatCells.Add(cell);
                    }
                    else if (data.SandWeight > 0.45f && !cell.IsBuildableUnderwaterPlateau)
                    {
                        kelpHabitatCells.Add(cell);
                    }
                }
                else if (!cell.IsBuildableUnderwaterPlateau)
                {
                    coralHabitatCells.Add(cell);
                    kelpHabitatCells.Add(cell);
                }
            }

            if (cell.IsDeliberateUnderwaterPlateau || cell.IsBuildableUnderwaterPlateau)
            {
                wildlifeHabitatCells.Add(cell);
            }
        }

        if (allPlateauCells.Count == 0) return;

        Transform plants = CreateGroup("Underwater Foliage");
        Transform corals = CreateGroup("Underwater Reef Corals");
        Transform wildlife = CreateGroup("Underwater Wildlife");
        Material kelpMaterial = null;
        Material coralWarmMaterial = null;
        Material coralTealMaterial = null;
        Material fishMaterial = null;
        Mesh fishMesh = null;

        // 1. Habitat-clustered kelp beds along rocky rim bases & gravel seams.
        List<Cell> kelpPool = kelpHabitatCells.Count > 0 ? kelpHabitatCells : allPlateauCells;
        int targetKelpClusters = Mathf.Clamp(
            Mathf.RoundToInt(kelpPool.Count * (climateProfile != null ? climateProfile.underwaterFoliageDensity * 0.45f : 0.08f)),
            Mathf.Max(6, (climateProfile != null ? climateProfile.minimumFoliagePerPlateau : 12) / 2),
            24);

        for (int cluster = 0; cluster < targetKelpClusters; cluster++)
        {
            Cell anchor = kelpPool[random.Next(kelpPool.Count)];
            int shootsInBed = 2 + random.Next(2);
            for (int shoot = 0; shoot < shootsInBed; shoot++)
            {
                SpawnPlant(anchor, plants, random, ref kelpMaterial, shoot > 0 ? 0.52f : 0.25f);
            }
        }

        // 2. Non-buildable rocky ledge coral & tube-sponge reef colonies.
        List<Cell> coralPool = coralHabitatCells.Count > 0 ? coralHabitatCells : kelpPool;
        int coralClusters = Mathf.Clamp(Mathf.RoundToInt(coralPool.Count * 0.09f), 8, 26);
        for (int i = 0; i < coralClusters; i++)
        {
            Cell reefCell = coralPool[random.Next(coralPool.Count)];
            SpawnReefCoralCluster(reefCell, corals, random, ref coralWarmMaterial, ref coralTealMaterial);
        }

        // 3. Swimming deep-sea fish above tabletop basins.
        List<Cell> swimPool = wildlifeHabitatCells.Count > 0 ? wildlifeHabitatCells : allPlateauCells;
        int wildlifeCount = climateProfile != null ? climateProfile.wildlifePerPlateau : 5;
        for (int i = 0; i < Mathf.Max(0, wildlifeCount); i++)
        {
            SpawnWildlife(swimPool[random.Next(swimPool.Count)], wildlife, random, ref fishMaterial, ref fishMesh);
        }
    }

    private static PlateauSampleData SamplePlateauData(TerrainSampleCache cache, float localX, float localZ)
    {
        if (cache == null || cache.PlateauData == null) return default;
        int sampleX = Mathf.Clamp(Mathf.RoundToInt(localX / cache.Step), 0, cache.Resolution - 1);
        int sampleZ = Mathf.Clamp(Mathf.RoundToInt(localZ / cache.Step), 0, cache.Resolution - 1);
        return cache.PlateauData[cache.GetIndex(sampleX, sampleZ)];
    }

    private void SpawnPlant(Cell cell, Transform parent, System.Random random, ref Material material, float jitterRadius = 0.38f)
    {
        GameObject prefab = Pick(climateProfile != null ? climateProfile.underwaterFoliagePrefabs : null, random);
        Vector2 scaleRange = climateProfile != null ? climateProfile.underwaterFoliageScale : new Vector2(0.7f, 1.35f);
        float scale = RandomRange(random, scaleRange);
        Vector3 position = CellWorldPosition(cell, random, 0.02f, jitterRadius);
        if (prefab != null)
        {
            Transform instance = Instantiate(prefab, position, RandomYaw(random), parent).transform;
            instance.localScale *= scale;
            return;
        }
        if (material == null) material = CreateSeaweedMaterial();
        GameObject kelp = new GameObject("Seaweed Clump", typeof(MeshFilter), typeof(MeshRenderer));
        kelp.transform.SetParent(parent, true);
        kelp.transform.SetPositionAndRotation(position, RandomYaw(random));
        kelp.transform.localScale = Vector3.one * scale;
        Mesh mesh = BuildSeaweedClumpMesh(random.Next());
        generatedMeshes.Add(mesh);
        kelp.GetComponent<MeshFilter>().sharedMesh = mesh;
        kelp.GetComponent<Renderer>().sharedMaterial = material;
    }

    private void SpawnReefCoralCluster(
        Cell cell,
        Transform parent,
        System.Random random,
        ref Material warmMaterial,
        ref Material tealMaterial)
    {
        bool isTealVariant = Next(random) < 0.45f;
        Material targetMat;
        if (isTealVariant)
        {
            if (tealMaterial == null) tealMaterial = CreateMaterial("Plateau Biolum Coral", new Color(0.14f, 0.56f, 0.52f));
            targetMat = tealMaterial;
        }
        else
        {
            if (warmMaterial == null) warmMaterial = CreateMaterial("Plateau Tube Sponge", new Color(0.72f, 0.34f, 0.24f));
            targetMat = warmMaterial;
        }

        Vector3 position = CellWorldPosition(cell, random, -0.02f, 0.42f);
        GameObject coral = new GameObject(isTealVariant ? "Sea Fan Cluster" : "Tube Sponge Cluster", typeof(MeshFilter), typeof(MeshRenderer));
        coral.transform.SetParent(parent, true);
        coral.transform.SetPositionAndRotation(position, RandomYaw(random));
        float scale = Mathf.Lerp(0.65f, 1.30f, Next(random));
        coral.transform.localScale = Vector3.one * scale;

        Mesh mesh = BuildTubeSpongeOrFanMesh(random.Next(), isTealVariant);
        generatedMeshes.Add(mesh);
        coral.GetComponent<MeshFilter>().sharedMesh = mesh;
        coral.GetComponent<Renderer>().sharedMaterial = targetMat;
    }

    private void SpawnWildlife(Cell cell, Transform parent, System.Random random, ref Material material, ref Mesh fishMesh)
    {
        GameObject prefab = Pick(climateProfile != null ? climateProfile.underwaterWildlifePrefabs : null, random);
        Vector2 scaleRange = climateProfile != null ? climateProfile.underwaterWildlifeScale : new Vector2(0.55f, 1.1f);
        float scale = RandomRange(random, scaleRange);
        float configuredHeight = climateProfile != null ? climateProfile.wildlifeHeightAboveSeabed : 1.2f;
        float height = configuredHeight * Mathf.Lerp(0.65f, 1.35f, Next(random));
        Vector3 position = CellWorldPosition(cell, random, height);
        if (prefab != null)
        {
            Transform instance = Instantiate(prefab, position, RandomYaw(random), parent).transform;
            instance.localScale *= scale;
            return;
        }
        if (material == null) material = CreateMaterial("Procedural Fish", new Color(0.24f, 0.68f, 0.78f));
        if (fishMesh == null)
        {
            fishMesh = BuildFishMesh();
            generatedMeshes.Add(fishMesh);
        }

        GameObject fish = new GameObject("Decorative Fish", typeof(MeshFilter), typeof(MeshRenderer));
        fish.transform.SetParent(parent, true);
        fish.transform.SetPositionAndRotation(position, RandomYaw(random));
        fish.transform.localScale = Vector3.one * scale;
        fish.GetComponent<MeshFilter>().sharedMesh = fishMesh;
        fish.GetComponent<Renderer>().sharedMaterial = material;
        fish.AddComponent<PlateauWildlifeSwimmer>().Configure(0.35f + Next(random) * 0.35f, 0.7f + Next(random) * 0.8f, Next(random) * Mathf.PI * 2f);
    }

    private static Mesh BuildTubeSpongeOrFanMesh(int seed, bool isFan)
    {
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        System.Random rng = new System.Random(seed);

        int count = isFan ? 4 : 5;
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count + Next(rng) * 0.6f;
            float offsetR = Mathf.Lerp(0.03f, 0.14f, Next(rng));
            Vector3 basePos = new Vector3(Mathf.Cos(angle) * offsetR, 0f, Mathf.Sin(angle) * offsetR);
            float height = Mathf.Lerp(isFan ? 0.24f : 0.18f, isFan ? 0.52f : 0.44f, Next(rng));
            float tiltX = Mathf.Cos(angle) * Mathf.Lerp(0.04f, 0.14f, Next(rng));
            float tiltZ = Mathf.Sin(angle) * Mathf.Lerp(0.04f, 0.14f, Next(rng));

            if (isFan)
            {
                // Flared sea-fan paddle frond
                float fanW = Mathf.Lerp(0.12f, 0.24f, Next(rng));
                Vector3 right = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 up = new Vector3(tiltX, height, tiltZ);
                int b = verts.Count;
                verts.Add(basePos - right * (fanW * 0.14f));
                verts.Add(basePos + right * (fanW * 0.14f));
                verts.Add(basePos + up * 0.6f - right * (fanW * 0.52f));
                verts.Add(basePos + up * 0.6f + right * (fanW * 0.52f));
                verts.Add(basePos + up - right * (fanW * 0.34f));
                verts.Add(basePos + up + right * (fanW * 0.34f));

                int[] quadPairs = { 0, 2, 3, 0, 3, 1, 2, 4, 5, 2, 5, 3 };
                for (int q = 0; q < quadPairs.Length; q++) tris.Add(b + quadPairs[q]);
                // Backface for two-sided underwater fan reading
                for (int q = 0; q < quadPairs.Length; q += 3)
                {
                    tris.Add(b + quadPairs[q]);
                    tris.Add(b + quadPairs[q + 2]);
                    tris.Add(b + quadPairs[q + 1]);
                }
            }
            else
            {
                // Flared 5-sided sponge tube
                const int sides = 5;
                float rBot = Mathf.Lerp(0.035f, 0.055f, Next(rng));
                float rTop = rBot * Mathf.Lerp(1.35f, 1.85f, Next(rng));
                int b = verts.Count;
                for (int s = 0; s < sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    verts.Add(basePos + new Vector3(Mathf.Cos(a) * rBot, -0.02f, Mathf.Sin(a) * rBot));
                }
                Vector3 crown = basePos + new Vector3(tiltX, height, tiltZ);
                for (int s = 0; s < sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    verts.Add(crown + new Vector3(Mathf.Cos(a) * rTop, 0f, Mathf.Sin(a) * rTop));
                }
                for (int s = 0; s < sides; s++)
                {
                    int next = (s + 1) % sides;
                    tris.Add(b + s); tris.Add(b + sides + s); tris.Add(b + sides + next);
                    tris.Add(b + s); tris.Add(b + sides + next); tris.Add(b + next);
                }
            }
        }

        Mesh mesh = new Mesh { name = isFan ? "Generated Sea Fan" : "Generated Tube Sponge" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildFishMesh()
    {
        // Streamlined fusiform diamond body along local +X with forked tail at -X and dorsal fin.
        Vector3[] verts =
        {
            new Vector3(0.24f, 0.0f, 0.0f),    // 0 nose
            new Vector3(0.04f, 0.07f, 0.0f),   // 1 top spine
            new Vector3(0.04f, -0.06f, 0.0f),  // 2 belly
            new Vector3(0.04f, 0.0f, 0.055f),  // 3 right flank
            new Vector3(0.04f, 0.0f, -0.055f), // 4 left flank
            new Vector3(-0.14f, 0.0f, 0.0f),   // 5 tail peduncle
            new Vector3(-0.26f, 0.075f, 0.0f), // 6 upper tail fork
            new Vector3(-0.26f, -0.075f, 0.0f),// 7 lower tail fork
            new Vector3(-0.02f, 0.12f, 0.0f),  // 8 dorsal peak
        };
        int[] tris =
        {
            // Right front & rear body
            0, 1, 3,  0, 3, 2,  1, 5, 3,  3, 5, 2,
            // Left front & rear body
            0, 4, 1,  0, 2, 4,  1, 4, 5,  4, 2, 5,
            // Tail fin & dorsal fin (double-sided thin fins)
            5, 6, 7,  5, 7, 6,
            1, 8, 5,  1, 5, 8,
        };
        Mesh mesh = new Mesh { name = "Generated Plateau Fish" };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Transform CreateGroup(string name)
    {
        Transform group = new GameObject(name).transform;
        group.SetParent(foliageRoot, false);
        return group;
    }

    private Material CreateMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader) { name = name, color = color };
        generatedMaterials.Add(material);
        return material;
    }

    private Material CreateSeaweedMaterial()
    {
        Shader shader = Shader.Find("Custom/SeaweedSway") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader) { name = "Plateau Seaweed" };
        if (material.HasProperty("_BaseTint")) material.SetColor("_BaseTint", new Color(0.08f, 0.34f, 0.18f));
        if (material.HasProperty("_TipTint")) material.SetColor("_TipTint", new Color(0.26f, 0.52f, 0.22f));
        if (material.HasProperty("_SwayAmp")) material.SetFloat("_SwayAmp", 0.42f);
        if (material.HasProperty("_SwayFreq")) material.SetFloat("_SwayFreq", 0.58f);
        generatedMaterials.Add(material);
        return material;
    }

    private static Mesh BuildSeaweedClumpMesh(int seed)
    {
        const int bladeCount = 5;
        const int segments = 10;
        const int columns = 3;
        int rows = segments + 1;
        int verticesPerBlade = rows * columns;
        Vector3[] vertices = new Vector3[bladeCount * verticesPerBlade];
        Vector2[] uvs = new Vector2[vertices.Length];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[bladeCount * segments * 4 * 3];
        System.Random random = new System.Random(seed);
        int triangleIndex = 0;

        for (int blade = 0; blade < bladeCount; blade++)
        {
            float angle = blade * Mathf.PI * 2f / bladeCount + Next(random) * 0.35f;
            float height = Mathf.Lerp(0.7f, 1.45f, Next(random));
            float width = Mathf.Lerp(0.13f, 0.24f, Next(random));
            Vector3 right = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 forward = new Vector3(-right.z, 0f, right.x);
            Vector3 root = right * Mathf.Lerp(0.02f, 0.13f, Next(random));
            float phase = Next(random) * Mathf.PI * 2f;
            for (int row = 0; row < rows; row++)
            {
                float t = row / (float)segments;
                float bladeWidth = width * Mathf.Lerp(1f, 0.18f, t);
                Vector3 center = root + Vector3.up * (height * t) + forward * (Mathf.Sin(t * 2.8f + phase) * 0.09f * t);
                for (int column = 0; column < columns; column++)
                {
                    float across = column * 0.5f;
                    int index = blade * verticesPerBlade + row * columns + column;
                    vertices[index] = center + right * ((across - 0.5f) * bladeWidth) + forward * (column == 1 ? bladeWidth * 0.08f : 0f);
                    uvs[index] = new Vector2(across, t);
                    colors[index] = new Color(blade / (float)bladeCount, t, Next(random), 1f);
                }
            }
            int baseVertex = blade * verticesPerBlade;
            for (int row = 0; row < segments; row++)
            for (int column = 0; column < columns - 1; column++)
            {
                int a = baseVertex + row * columns + column;
                int b = a + 1;
                int c = a + columns;
                int d = c + 1;
                triangles[triangleIndex++] = a; triangles[triangleIndex++] = c; triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = b; triangles[triangleIndex++] = c; triangles[triangleIndex++] = d;
            }
        }

        Mesh mesh = new Mesh { name = "Generated Plateau Seaweed" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Vector3 CellWorldPosition(Cell cell, System.Random random, float height, float jitter = 0.38f)
    {
        Vector3 local = cell.localCenter;
        local.x += Mathf.Lerp(-jitter, jitter, Next(random));
        local.z += Mathf.Lerp(-jitter, jitter, Next(random));
        local.y = cell.height + height;
        return transform.TransformPoint(local);
    }

    private void ClearGeneratedLife()
    {
        foreach (var cell in subscribedCells.Values) cell.PlacementChanged -= InvalidateCell;
        subscribedCells.Clear();
        // Clear() is deliberately silent - teardown must not fan Removed out to listeners.
        store.Clear();
        patches.Clear(); instancedMaterials.Clear(); terrainCache = null;
        speciesTable.Clear();
        foreach (FoliageSpecies species in generatedSpecies) DestroyGeneratedObject(species);
        generatedSpecies.Clear();
        if (foliageRoot == null) foliageRoot = transform.Find(RootName);
        if (foliageRoot != null) DestroyGeneratedObject(foliageRoot.gameObject);
        foliageRoot = null;
        foreach (Material material in generatedMaterials) DestroyGeneratedObject(material);
        generatedMaterials.Clear();
        foreach (Mesh mesh in generatedMeshes) DestroyGeneratedObject(mesh);
        generatedMeshes.Clear();
    }

    private void OnDestroy() => ClearGeneratedLife();
    private static GameObject Pick(GameObject[] prefabs, System.Random random) => prefabs == null || prefabs.Length == 0 ? null : prefabs[random.Next(prefabs.Length)];
    private static Quaternion RandomYaw(System.Random random) => Quaternion.Euler(0f, Next(random) * 360f, 0f);
    private static float Next(System.Random random) => (float)random.NextDouble();
    private static float RandomRange(System.Random random, Vector2 range) => Mathf.Lerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), Next(random));
    private static void RemoveCollider(GameObject target) { Collider value = target.GetComponent<Collider>(); if (value != null) DestroyGeneratedObject(value); }
    private static void DestroyGeneratedObject(Object target) { if (target == null) return; if (Application.isPlaying) Destroy(target); else DestroyImmediate(target); }
}

public sealed class PlateauWildlifeSwimmer : MonoBehaviour
{
    private Vector3 origin;
    private float radius;
    private float speed;
    private float phase;
    public void Configure(float orbitRadius, float orbitSpeed, float orbitPhase) { origin = transform.localPosition; radius = orbitRadius; speed = orbitSpeed; phase = orbitPhase; }
    private void Update()
    {
        float angle = phase + Time.time * speed;
        Vector3 next = origin + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle * 1.7f) * 0.12f, Mathf.Sin(angle) * radius);
        Vector3 direction = next - transform.localPosition;
        transform.localPosition = next;
        if (direction.sqrMagnitude > 0.0001f) transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);
    }
}

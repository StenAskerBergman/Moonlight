using System.Collections.Generic;
using UnityEngine;

public class IslandFoliagePlacer : MonoBehaviour
{
    private const string RootName = "Generated Foliage & Wildlife";
    public ClimateProfile climateProfile;
    private Transform foliageRoot;
    private readonly List<Material> generatedMaterials = new List<Material>();
    private readonly List<Mesh> generatedMeshes = new List<Mesh>();

    public void ScatterFoliage(Cell[,] grid, bool isStandalonePlateau, int seed)
    {
        ClearGeneratedLife();
        if (grid == null) return;
        foliageRoot = new GameObject(RootName).transform;
        foliageRoot.SetParent(transform, false);
        System.Random random = new System.Random(unchecked(seed ^ gameObject.name.GetHashCode() ^ 0x4A71C3));
        if (isStandalonePlateau && (climateProfile == null || climateProfile.populateUnderwaterPlateaus)) ScatterPlateauLife(grid, random);
        else if (climateProfile != null) ScatterLand(grid, random);
    }

    private void ScatterLand(Cell[,] grid, System.Random random)
    {
        if (climateProfile.treePrefabs == null || climateProfile.treePrefabs.Length == 0) return;
        float patchOffsetX = RandomRange(random, new Vector2(0f, 1000f));
        float patchOffsetZ = RandomRange(random, new Vector2(0f, 1000f));
        for (int z = 0; z < grid.GetLength(1); z++)
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            Cell cell = grid[x, z];
            bool forest = cell.currentTerrainType == Cell.TerrainType.Forest;
            bool grass = cell.currentTerrainType == Cell.TerrainType.Land || cell.currentTerrainType == Cell.TerrainType.Plain;
            float baseChance = forest ? climateProfile.forestDensity : (grass ? climateProfile.plainsTreeDensity : 0f);
            float patchNoise = Mathf.PerlinNoise(
                (x + patchOffsetX) * 0.075f,
                (z + patchOffsetZ) * 0.075f);
            float patchWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.72f, patchNoise));
            float chance = baseChance * Mathf.Lerp(0.18f, 2.35f, patchWeight);
            if (Next(random) > chance) continue;
            GameObject prefab = Pick(climateProfile.treePrefabs, random);
            if (prefab == null) continue;
            Transform instance = Instantiate(prefab, CellWorldPosition(cell, random, 0f), RandomYaw(random), foliageRoot).transform;
            instance.localScale = Vector3.one * Mathf.Lerp(climateProfile.treeScaleMin, climateProfile.treeScaleMax, Next(random));
        }
    }

    private void ScatterPlateauLife(Cell[,] grid, System.Random random)
    {
        List<Cell> tabletop = new List<Cell>();
        for (int z = 0; z < grid.GetLength(1); z++)
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            Cell cell = grid[x, z];
            if (cell.currentTerrainType == Cell.TerrainType.Plateau && cell.IsDeliberateUnderwaterPlateau) tabletop.Add(cell);
        }
        if (tabletop.Count == 0) return;

        Transform plants = CreateGroup("Underwater Foliage");
        Transform wildlife = CreateGroup("Underwater Wildlife");
        Material kelpMaterial = null;
        Material fishMaterial = null;
        int foliageCount = 0;
        foreach (Cell cell in tabletop)
        {
            float density = climateProfile != null ? climateProfile.underwaterFoliageDensity : 0.16f;
            if (Next(random) > density) continue;
            SpawnPlant(cell, plants, random, ref kelpMaterial);
            foliageCount++;
        }
        int configuredMinimum = climateProfile != null ? climateProfile.minimumFoliagePerPlateau : 12;
        int minimum = Mathf.Min(tabletop.Count, Mathf.Max(0, configuredMinimum));
        while (foliageCount++ < minimum) SpawnPlant(tabletop[random.Next(tabletop.Count)], plants, random, ref kelpMaterial);
        int wildlifeCount = climateProfile != null ? climateProfile.wildlifePerPlateau : 5;
        for (int i = 0; i < Mathf.Max(0, wildlifeCount); i++)
            SpawnWildlife(tabletop[random.Next(tabletop.Count)], wildlife, random, ref fishMaterial);
    }

    private void SpawnPlant(Cell cell, Transform parent, System.Random random, ref Material material)
    {
        GameObject prefab = Pick(climateProfile != null ? climateProfile.underwaterFoliagePrefabs : null, random);
        Vector2 scaleRange = climateProfile != null ? climateProfile.underwaterFoliageScale : new Vector2(0.7f, 1.35f);
        float scale = RandomRange(random, scaleRange);
        Vector3 position = CellWorldPosition(cell, random, 0.02f);
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

    private void SpawnWildlife(Cell cell, Transform parent, System.Random random, ref Material material)
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
        if (material == null) material = CreateMaterial("Procedural Fish", new Color(0.22f, 0.64f, 0.72f));
        GameObject fish = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fish.name = "Decorative Fish";
        fish.transform.SetParent(parent, true);
        fish.transform.SetPositionAndRotation(position, RandomYaw(random));
        fish.transform.localScale = new Vector3(0.42f, 0.16f, 0.18f) * scale;
        fish.GetComponent<Renderer>().sharedMaterial = material;
        RemoveCollider(fish);
        fish.AddComponent<PlateauWildlifeSwimmer>().Configure(0.35f + Next(random) * 0.35f, 0.7f + Next(random) * 0.8f, Next(random) * Mathf.PI * 2f);
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

    private Vector3 CellWorldPosition(Cell cell, System.Random random, float height)
    {
        Vector3 local = cell.localCenter;
        local.x += Mathf.Lerp(-0.38f, 0.38f, Next(random));
        local.z += Mathf.Lerp(-0.38f, 0.38f, Next(random));
        local.y = cell.height + height;
        return transform.TransformPoint(local);
    }

    private void ClearGeneratedLife()
    {
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

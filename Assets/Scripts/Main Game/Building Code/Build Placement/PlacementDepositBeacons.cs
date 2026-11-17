using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Displays glowing pulsing Anno-style ring markers over unexploited resource deposits
/// matching the currently hovered building blueprint (Mines, Coastal Fisheries, Crude Oil,
/// Hydrothermal Vents, Ore Seabed, etc.).
/// </summary>
public sealed class PlacementDepositBeacons : MonoBehaviour
{
    private static PlacementDepositBeacons instance;

    private struct DepositCluster
    {
        public Vector3 WorldPosition;
        public float Radius;
    }

    private readonly List<DepositCluster> clusters = new List<DepositCluster>();
    private readonly List<GameObject> activeBeacons = new List<GameObject>();
    private readonly HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
    private readonly Queue<Vector2Int> queue = new Queue<Vector2Int>();

    private Mesh sharedRingMesh;
    private Material sharedMaterial;
    private MaterialPropertyBlock propertyBlock;
    private MaterialPropertyBlock PropertyBlock => propertyBlock ??= new MaterialPropertyBlock();

    private GridSystem builtForGrid;
    private ResourceNodeType builtForNodeType = ResourceNodeType.None;
    private bool isVisible;

    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

    public static void Show(GridSystem grid, BuildingData buildingData)
    {
        if (grid == null || buildingData == null || buildingData.requiredNodeType == ResourceNodeType.None)
        {
            Hide();
            return;
        }

        EnsureInstance().Display(grid, buildingData.requiredNodeType);
    }

    public static void Hide()
    {
        if (instance == null || !instance.isVisible) return;
        instance.SetAllActive(false);
        instance.isVisible = false;
    }

    private static PlacementDepositBeacons EnsureInstance()
    {
        if (instance == null)
        {
            GameObject host = new GameObject("Placement Deposit Beacons");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<PlacementDepositBeacons>();
        }
        return instance;
    }

    private void Display(GridSystem grid, ResourceNodeType nodeType)
    {
        EnsureAssets();

        if (!isVisible || builtForGrid != grid || builtForNodeType != nodeType)
        {
            builtForGrid = grid;
            builtForNodeType = nodeType;
            RebuildClusters(grid, nodeType);
            SyncBeaconObjects();
        }

        isVisible = true;
        SetAllActive(true);
    }

    private void Update()
    {
        if (!isVisible || activeBeacons.Count == 0) return;

        float pulse = 0.72f + 0.28f * Mathf.Sin(Time.time * 4.2f);
        float scalePulse = 1f + 0.06f * Mathf.Sin(Time.time * 4.2f);
        Color glow = new Color(0.22f * pulse, 1.0f * pulse, 0.45f * pulse, 0.85f * pulse);

        for (int i = 0; i < activeBeacons.Count; i++)
        {
            GameObject go = activeBeacons[i];
            if (go == null || !go.activeSelf) continue;

            float baseRadius = i < clusters.Count ? clusters[i].Radius : 1.5f;
            float s = baseRadius * scalePulse;
            go.transform.localScale = new Vector3(s, 1f, s);

            MeshRenderer r = go.GetComponent<MeshRenderer>();
            if (r != null)
            {
                MaterialPropertyBlock block = PropertyBlock;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorProperty, glow);
                r.SetPropertyBlock(block);
            }
        }
    }

    private void RebuildClusters(GridSystem grid, ResourceNodeType nodeType)
    {
        clusters.Clear();
        visited.Clear();

        int n = grid.gridSize;
        QuaySystem quay = QuaySystem.GetOrCreate(grid);
        float waterWorldY = quay != null ? grid.transform.TransformPoint(new Vector3(0f, quay.WaterSurfaceLocal, 0f)).y : 0f;
        MapGrid mapGrid = grid.GetComponent<MapGrid>();
        bool clampToWaterSurface = (mapGrid == null || mapGrid.currentGridType != GridType.Type.Plateau)
            && (nodeType == ResourceNodeType.CoastalFishery || nodeType == ResourceNodeType.LakeMouth);

        for (int z = 0; z < n; z++)
        {
            for (int x = 0; x < n; x++)
            {
                var start = new Vector2Int(x, z);
                if (visited.Contains(start)) continue;

                Cell cell = grid.GetCell(x, z);
                if (!MatchesUnexploitedDeposit(cell, nodeType)) continue;

                queue.Clear();
                queue.Enqueue(start);
                visited.Add(start);

                Vector3 accum = Vector3.zero;
                int count = 0;
                float maxHeight = float.MinValue;
                int minX = x, maxX = x, minZ = z, maxZ = z;

                while (queue.Count > 0)
                {
                    Vector2Int curr = queue.Dequeue();
                    Cell c = grid.GetCell(curr.x, curr.y);
                    if (c == null) continue;

                    Vector3 cellWorld = grid.GetCellCenterWorld(curr.x, curr.y);
                    accum += cellWorld;
                    count++;
                    maxHeight = Mathf.Max(maxHeight, cellWorld.y);

                    if (curr.x < minX) minX = curr.x;
                    if (curr.x > maxX) maxX = curr.x;
                    if (curr.y < minZ) minZ = curr.y;
                    if (curr.y > maxZ) maxZ = curr.y;

                    TryEnqueueNeighbor(grid, curr.x + 1, curr.y, nodeType);
                    TryEnqueueNeighbor(grid, curr.x - 1, curr.y, nodeType);
                    TryEnqueueNeighbor(grid, curr.x, curr.y + 1, nodeType);
                    TryEnqueueNeighbor(grid, curr.x, curr.y - 1, nodeType);
                }

                if (count == 0) continue;

                Vector3 center = accum / count;
                center.y = (clampToWaterSurface ? Mathf.Max(maxHeight, waterWorldY) : maxHeight) + 0.14f;
                float span = Mathf.Max(maxX - minX + 1, maxZ - minZ + 1) * grid.cellSize;

                clusters.Add(new DepositCluster
                {
                    WorldPosition = center,
                    Radius = Mathf.Max(1.3f, span * 0.65f)
                });
            }
        }
    }

    private void TryEnqueueNeighbor(GridSystem grid, int x, int z, ResourceNodeType nodeType)
    {
        if (x < 0 || z < 0 || x >= grid.gridSize || z >= grid.gridSize) return;
        var coord = new Vector2Int(x, z);
        if (visited.Contains(coord)) return;

        Cell c = grid.GetCell(x, z);
        if (!MatchesUnexploitedDeposit(c, nodeType)) return;

        visited.Add(coord);
        queue.Enqueue(coord);
    }

    private static bool MatchesUnexploitedDeposit(Cell cell, ResourceNodeType targetType)
    {
        return cell != null && cell.isDeposit && !cell.isOccupied && cell.depositNodeType == targetType;
    }

    private void SyncBeaconObjects()
    {
        while (activeBeacons.Count < clusters.Count)
        {
            var beacon = new GameObject($"DepositBeacon_{activeBeacons.Count}");
            beacon.transform.SetParent(transform, false);
            beacon.AddComponent<MeshFilter>().sharedMesh = sharedRingMesh;
            MeshRenderer renderer = beacon.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = sharedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            activeBeacons.Add(beacon);
        }

        for (int i = 0; i < activeBeacons.Count; i++)
        {
            if (i < clusters.Count)
            {
                activeBeacons[i].transform.position = clusters[i].WorldPosition;
                activeBeacons[i].transform.localScale = new Vector3(clusters[i].Radius, 1f, clusters[i].Radius);
                activeBeacons[i].SetActive(true);
            }
            else
            {
                activeBeacons[i].SetActive(false);
            }
        }
    }

    private void SetAllActive(bool visible)
    {
        int count = Mathf.Min(activeBeacons.Count, clusters.Count);
        for (int i = 0; i < activeBeacons.Count; i++)
        {
            if (activeBeacons[i] != null)
            {
                activeBeacons[i].SetActive(visible && i < count);
            }
        }
    }

    private void EnsureAssets()
    {
        if (sharedRingMesh == null)
        {
            sharedRingMesh = BuildRingMesh(32, 0.72f, 1.0f);
        }

        if (sharedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            sharedMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            if (sharedMaterial.HasProperty("_Surface")) sharedMaterial.SetFloat("_Surface", 1f);
            if (sharedMaterial.HasProperty("_Blend")) sharedMaterial.SetFloat("_Blend", 0f);
            if (sharedMaterial.HasProperty("_SrcBlend")) sharedMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (sharedMaterial.HasProperty("_DstBlend")) sharedMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (sharedMaterial.HasProperty("_ZWrite")) sharedMaterial.SetFloat("_ZWrite", 0f);
            if (sharedMaterial.HasProperty("_ZTest")) sharedMaterial.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            if (sharedMaterial.HasProperty("_Cull")) sharedMaterial.SetFloat("_Cull", (float)CullMode.Off);
            sharedMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            sharedMaterial.renderQueue = (int)RenderQueue.Transparent + 45;
        }
    }

    private static Mesh BuildRingMesh(int segments, float innerRadius, float outerRadius)
    {
        var vertices = new Vector3[(segments + 1) * 2];
        var triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float c = Mathf.Cos(angle);
            float s = Mathf.Sin(angle);
            vertices[i * 2] = new Vector3(c * innerRadius, 0f, s * innerRadius);
            vertices[i * 2 + 1] = new Vector3(c * outerRadius, 0f, s * outerRadius);
        }

        for (int i = 0; i < segments; i++)
        {
            int v = i * 2;
            int t = i * 6;
            triangles[t] = v;
            triangles[t + 1] = v + 1;
            triangles[t + 2] = v + 3;
            triangles[t + 3] = v;
            triangles[t + 4] = v + 3;
            triangles[t + 5] = v + 2;
        }

        Mesh mesh = new Mesh { name = "DepositBeaconRing" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (sharedRingMesh != null) Destroy(sharedRingMesh);
        if (sharedMaterial != null) Destroy(sharedMaterial);
    }
}

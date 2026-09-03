using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws the quay platform a harbor blueprint would stand on, before it is placed.
///
/// It asks <see cref="QuaySystem.CollectFoundationCells"/> for the exact cells the real
/// foundation would claim and builds the same two surfaces the quay itself builds - a
/// flat deck, and a retaining skirt on the outer perimeter only, skipped where the deck
/// meets shore. The previous stand-in was a cube scaled to the building's own footprint,
/// which showed neither the surrounding deck nor the walls and so told the player nothing
/// about what they were actually placing.
///
/// It is a child of the blueprint and is destroyed with it. Nothing here touches
/// QuaySystem's occupancy - a preview claims no cells.
/// </summary>
[DisallowMultipleComponent]
public sealed class QuayFoundationPreview : MonoBehaviour
{
    private const string PreviewObjectName = "Preview Quay Foundation";

    private GameObject previewObject;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;

    private readonly List<Vector2Int> cells = new List<Vector2Int>();
    private readonly HashSet<Vector2Int> cellSet = new HashSet<Vector2Int>();
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<int> deckTriangles = new List<int>();
    private readonly List<int> skirtTriangles = new List<int>();

    // What the drawn mesh was built from. The blueprint moves a cell at a time, so
    // rebuilding only when the claimed cells actually change keeps this off the frame.
    private GridSystem builtForGrid;
    private Vector3Int builtForOrigin = new Vector3Int(int.MinValue, 0, int.MinValue);
    private Vector2Int builtForFootprint = new Vector2Int(int.MinValue, int.MinValue);
    private int builtForPadding = -1;

    /// <summary>
    /// Rebuilds the platform under a blueprint standing at <paramref name="origin"/>.
    /// </summary>
    public void Show(GridSystem grid, Vector3Int origin, Vector2Int footprint, int padding, Material material)
    {
        if (grid == null)
        {
            Hide();
            return;
        }

        QuaySystem quay = QuaySystem.GetOrCreate(grid);
        if (quay == null)
        {
            Hide();
            return;
        }

        EnsureRenderObjects(grid);

        bool sameShape = builtForGrid == grid
                         && builtForOrigin == origin
                         && builtForFootprint == footprint
                         && builtForPadding == padding;

        if (!sameShape)
        {
            builtForGrid = grid;
            builtForOrigin = origin;
            builtForFootprint = footprint;
            builtForPadding = padding;
            Rebuild(grid, quay, origin, footprint, padding);
        }

        previewObject.SetActive(true);

        // Validity tint follows the blueprint, so the deck reads green/red with it.
        if (material != null && meshRenderer.sharedMaterial != material)
        {
            meshRenderer.sharedMaterial = material;
        }
    }

    public void Hide()
    {
        if (previewObject != null) previewObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (previewObject != null) Destroy(previewObject);
    }

    /// <summary>
    /// The preview is parented to the GRID, not to the blueprint. Its geometry is in grid
    /// cell coordinates, and the blueprint carries a rotation the platform must not inherit.
    /// </summary>
    private void EnsureRenderObjects(GridSystem grid)
    {
        if (previewObject == null)
        {
            previewObject = new GameObject(PreviewObjectName);
            meshFilter = previewObject.AddComponent<MeshFilter>();
            meshRenderer = previewObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            mesh = new Mesh { name = "Quay Foundation Preview" };
            meshFilter.sharedMesh = mesh;
        }

        if (previewObject.transform.parent != grid.transform)
        {
            previewObject.transform.SetParent(grid.transform, false);
        }
        previewObject.transform.localPosition = Vector3.zero;
        previewObject.transform.localRotation = Quaternion.identity;
        previewObject.transform.localScale = Vector3.one;
    }

    private void Rebuild(GridSystem grid, QuaySystem quay, Vector3Int origin, Vector2Int footprint, int padding)
    {
        quay.CollectFoundationCells(origin, footprint, padding, cells);

        cellSet.Clear();
        for (int i = 0; i < cells.Count; i++) cellSet.Add(cells[i]);

        vertices.Clear();
        deckTriangles.Clear();
        skirtTriangles.Clear();

        float cellSize = grid.cellSize;
        float top = quay.TopElevationLocal;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            AddDeck(cell, cellSize, top);

            // Only edges facing off the platform get a wall, and only where the platform
            // is not simply running into the shore - the same test the built quay uses,
            // so no wall ever appears between two connected deck cells.
            for (int d = 0; d < 4; d++)
            {
                Vector2Int outward = DirectionVector(d);
                Vector2Int neighbor = cell + outward;
                if (cellSet.Contains(neighbor)) continue;

                // An existing quay next door merges with this one, so that edge is internal too.
                if (quay.HasQuay(neighbor)) continue;
                if (quay.IsLandConnection(neighbor)) continue;

                AddSkirt(cell, outward, cellSize, top, quay.GetWallBottom(cell, neighbor));
            }
        }

        mesh.Clear();
        if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(deckTriangles, 0, false);
        mesh.SetTriangles(skirtTriangles, 1, false);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void AddDeck(Vector2Int cell, float cellSize, float y)
    {
        float x0 = cell.x * cellSize;
        float z0 = cell.y * cellSize;
        float x1 = x0 + cellSize;
        float z1 = z0 + cellSize;

        int start = vertices.Count;
        vertices.Add(new Vector3(x0, y, z0));
        vertices.Add(new Vector3(x0, y, z1));
        vertices.Add(new Vector3(x1, y, z1));
        vertices.Add(new Vector3(x1, y, z0));
        deckTriangles.Add(start); deckTriangles.Add(start + 1); deckTriangles.Add(start + 2);
        deckTriangles.Add(start); deckTriangles.Add(start + 2); deckTriangles.Add(start + 3);
    }

    private void AddSkirt(Vector2Int cell, Vector2Int outward, float cellSize, float top, float bottom)
    {
        float x0 = cell.x * cellSize;
        float z0 = cell.y * cellSize;
        float x1 = x0 + cellSize;
        float z1 = z0 + cellSize;

        Vector3 a, b;
        if (outward == Vector2Int.up)         { a = new Vector3(x1, top, z1); b = new Vector3(x0, top, z1); }
        else if (outward == Vector2Int.right) { a = new Vector3(x1, top, z0); b = new Vector3(x1, top, z1); }
        else if (outward == Vector2Int.down)  { a = new Vector3(x0, top, z0); b = new Vector3(x1, top, z0); }
        else                                  { a = new Vector3(x0, top, z1); b = new Vector3(x0, top, z0); }

        // The blueprint only needs to read as a wall, not to reach the seabed the way the
        // built quay does - a full-depth skirt on deep water is a curtain across the view.
        float skirtBottom = Mathf.Max(bottom, top - 1.5f);

        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(new Vector3(b.x, skirtBottom, b.z));
        vertices.Add(new Vector3(a.x, skirtBottom, a.z));
        skirtTriangles.Add(start); skirtTriangles.Add(start + 1); skirtTriangles.Add(start + 2);
        skirtTriangles.Add(start); skirtTriangles.Add(start + 2); skirtTriangles.Add(start + 3);
    }

    private static Vector2Int DirectionVector(int index)
    {
        switch (index)
        {
            case 0: return Vector2Int.up;
            case 1: return Vector2Int.right;
            case 2: return Vector2Int.down;
            default: return Vector2Int.left;
        }
    }
}

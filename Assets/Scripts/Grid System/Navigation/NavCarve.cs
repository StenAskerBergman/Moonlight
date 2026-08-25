using System.Collections.Generic;
using UnityEngine;

// Assets/NavMeshComponents duplicates these types in UnityEngine.AI, so the bare
// name is ambiguous. The scene's surfaces resolve to the package, so alias to it.
using NavMeshModifierVolume = Unity.AI.Navigation.NavMeshModifierVolume;

/// <summary>
/// Shared carving primitives for every stacked NavMesh layer.
///
/// Marking geometry "Not Walkable" with a NavMeshModifier does not remove a
/// walkable surface underneath it - Recast only drops a span when something above
/// it is within the agent's height, so a flat layer several units below the
/// blocker survives. A NavMeshModifierVolume rewrites the area of every span
/// inside the box regardless of which geometry produced it, so it is the only
/// thing that reliably punches a hole. Both the water carver and the depth /
/// altitude bands need that, hence one place for it.
/// </summary>
public static class NavCarve
{
    /// <summary>
    /// Greedy decomposition of a blocked-cell grid into as few axis-aligned
    /// rectangles as possible. A 100x100 island is 10,000 cells but only a few
    /// dozen rectangles, which is the difference between a bake that finishes and
    /// one that does not.
    /// </summary>
    public static List<RectInt> MergeIntoRectangles(bool[,] blocked, int width, int depth)
    {
        List<RectInt> rects = new List<RectInt>();
        bool[,] used = new bool[width, depth];

        for (int y = 0; y < depth; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!blocked[x, y] || used[x, y]) continue;

                // Longest run to the right.
                int x1 = x;
                while (x1 + 1 < width && blocked[x1 + 1, y] && !used[x1 + 1, y]) x1++;

                // Then grow downwards while the whole run stays blocked and unused.
                int y1 = y;
                while (y1 + 1 < depth && RowIsFree(blocked, used, x, x1, y1 + 1)) y1++;

                for (int yy = y; yy <= y1; yy++)
                    for (int xx = x; xx <= x1; xx++)
                        used[xx, yy] = true;

                rects.Add(new RectInt(x, y, x1 - x + 1, y1 - y + 1));
            }
        }

        return rects;
    }

    private static bool RowIsFree(bool[,] blocked, bool[,] used, int x0, int x1, int y)
    {
        for (int x = x0; x <= x1; x++)
        {
            if (!blocked[x, y] || used[x, y]) return false;
        }
        return true;
    }

    /// <summary>
    /// One modifier volume covering a rectangle of grid cells. Cell (x,y) spans
    /// local x/z of [x-0.5, x+0.5] - the same quad corners TerrainMeshBuilder
    /// emits - so a carve lines up with the visible mesh rather than with the
    /// island's bounding box.
    /// </summary>
    /// <param name="parent">Carve root; the rect is placed in its local space.</param>
    /// <param name="worldY">World height to centre the box on.</param>
    /// <param name="thickness">Vertical size. Keep it tight so one layer's carve
    /// does not reach into the layer above or below.</param>
    /// <param name="margin">Extra cells added on every side.</param>
    public static GameObject CreateVolume(Transform parent, RectInt rect, int layer,
                                          float worldY, float thickness, float margin, int area)
    {
        GameObject go = new GameObject($"Carve {rect.x},{rect.y} {rect.width}x{rect.height}");
        go.layer = layer;
        go.transform.SetParent(parent, false);

        float centreX = rect.x + (rect.width - 1) * 0.5f;
        float centreZ = rect.y + (rect.height - 1) * 0.5f;
        go.transform.localPosition = new Vector3(centreX, 0f, centreZ);

        // Sit the box at the layer's height in world space, whatever height the
        // parent happens to be at.
        Vector3 world = go.transform.position;
        go.transform.position = new Vector3(world.x, worldY, world.z);

        // AppendModifierVolumes multiplies size by lossyScale, so divide it back
        // out to get the world-space box we actually want.
        Vector3 scale = go.transform.lossyScale;
        float sx = (rect.width + margin * 2f) / Mathf.Max(Mathf.Abs(scale.x), 0.0001f);
        float sy = thickness / Mathf.Max(Mathf.Abs(scale.y), 0.0001f);
        float sz = (rect.height + margin * 2f) / Mathf.Max(Mathf.Abs(scale.z), 0.0001f);

        NavMeshModifierVolume volume = go.AddComponent<NavMeshModifierVolume>();
        volume.center = Vector3.zero;
        volume.size = new Vector3(sx, sy, sz);
        volume.area = area;

        return go;
    }
}

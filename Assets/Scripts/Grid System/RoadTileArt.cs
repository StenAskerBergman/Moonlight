using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the fallback road art from the already-resolved connectivity mask. The
/// placement and pathing systems remain authoritative; this component only turns
/// their result into low-profile, RTS-readable modular geometry.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class RoadTileArt : MonoBehaviour
{
    private const int Surface = 0;
    private const int Concrete = 1;
    private const int Marking = 2;
    private const int Grime = 3;
    private const int Metal = 4;
    private const int Accent = 5;
    private const int Earth = 6;
    private const int Lamp = 7;

    private static readonly Dictionary<RoadVisualStyle, Material[]> Palettes =
        new Dictionary<RoadVisualStyle, Material[]>();

    private Mesh generatedMesh;

    public void Configure(RoadTopologyResolver.Result topology, BridgeAppearance bridge)
    {
        int mask = bridge.IsBridge && topology.BridgeAxisMask != 0
            ? topology.BridgeAxisMask
            : topology.ConnectionMask;
        if (mask == 0) mask = RoadTopologyResolver.North;

        bool highway = topology.VisualStyle != RoadVisualStyle.CityRoad;
        float roadWidth = highway ? 0.78f : 0.62f;
        float halfWidth = roadWidth * 0.5f;
        float surfaceTop = bridge.IsBridge ? 0.045f : 0.035f;
        MeshBuilder mesh = new MeshBuilder(8);

        AddEmbeddedBase(mesh, mask, roadWidth, bridge.IsBridge);
        AddRoadSurface(mesh, mask, roadWidth, surfaceTop);
        AddSurfaceSeams(mesh, mask, roadWidth, surfaceTop, highway, bridge.IsBridge);
        AddLaneDefinition(mesh, mask, roadWidth, surfaceTop, highway);
        AddWear(mesh, mask, roadWidth, surfaceTop, topology.Wear, bridge.IsBridge);

        if (!bridge.IsBridge)
        {
            AddCurbs(mesh, mask, roadWidth, surfaceTop, highway);
            AddDrainage(mesh, mask, roadWidth, surfaceTop, highway);
            AddBridgeApproach(mesh, topology.BridgeApproachMask, roadWidth, surfaceTop);
            if (highway)
            {
                AddHighwayHardware(mesh, mask, topology.ParallelMask, halfWidth, surfaceTop, topology.VisualStyle);
            }
        }

        generatedMesh = mesh.Create($"{topology.VisualStyle} Road {mask}");
        MeshFilter filter = GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        filter.sharedMesh = generatedMesh;
        meshRenderer.sharedMaterials = GetPalette(topology.VisualStyle);
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        meshRenderer.receiveShadows = true;
    }

    private void OnDestroy()
    {
        if (generatedMesh == null) return;
        if (Application.isPlaying) Destroy(generatedMesh);
        else DestroyImmediate(generatedMesh);
    }

    private static void AddEmbeddedBase(MeshBuilder mesh, int mask, float width, bool bridge)
    {
        float extra = bridge ? 0.035f : 0.055f;
        float thickness = bridge ? 0.075f : 0.055f;
        AddRouteModules(mesh, mask, width + extra * 2f, -thickness * 0.5f, thickness, bridge ? Concrete : Earth);
    }

    private static void AddRoadSurface(MeshBuilder mesh, int mask, float width, float top)
    {
        AddRouteModules(mesh, mask, width, top - 0.026f, 0.052f, Surface);
    }

    private static void AddRouteModules(MeshBuilder mesh, int mask, float width, float centerY, float height, int material)
    {
        float half = width * 0.5f;
        mesh.AddBox(Vector3.up * centerY, new Vector3(width, height, width), material);

        if ((mask & RoadTopologyResolver.North) != 0)
            mesh.AddBox(new Vector3(0f, centerY, (half + 0.5f) * 0.5f), new Vector3(width, height, 0.5f - half), material);
        if ((mask & RoadTopologyResolver.South) != 0)
            mesh.AddBox(new Vector3(0f, centerY, -(half + 0.5f) * 0.5f), new Vector3(width, height, 0.5f - half), material);
        if ((mask & RoadTopologyResolver.East) != 0)
            mesh.AddBox(new Vector3((half + 0.5f) * 0.5f, centerY, 0f), new Vector3(0.5f - half, height, width), material);
        if ((mask & RoadTopologyResolver.West) != 0)
            mesh.AddBox(new Vector3(-(half + 0.5f) * 0.5f, centerY, 0f), new Vector3(0.5f - half, height, width), material);
    }

    private static void AddCurbs(MeshBuilder mesh, int mask, float width, float top, bool highway)
    {
        float half = width * 0.5f;
        float curbWidth = highway ? 0.04f : 0.055f;
        float curbHeight = highway ? 0.055f : 0.075f;
        float armLength = 0.5f - half;
        float armCenter = (half + 0.5f) * 0.5f;
        float y = top + curbHeight * 0.5f;

        if ((mask & RoadTopologyResolver.North) != 0)
        {
            AddLongCurb(mesh, -half, armCenter, curbWidth, armLength, y, curbHeight, true);
            AddLongCurb(mesh, half, armCenter, curbWidth, armLength, y, curbHeight, true);
        }
        else AddLongCurb(mesh, 0f, half, width + curbWidth * 2f, curbWidth, y, curbHeight, false);

        if ((mask & RoadTopologyResolver.South) != 0)
        {
            AddLongCurb(mesh, -half, -armCenter, curbWidth, armLength, y, curbHeight, true);
            AddLongCurb(mesh, half, -armCenter, curbWidth, armLength, y, curbHeight, true);
        }
        else AddLongCurb(mesh, 0f, -half, width + curbWidth * 2f, curbWidth, y, curbHeight, false);

        if ((mask & RoadTopologyResolver.East) != 0)
        {
            AddLongCurb(mesh, armCenter, -half, armLength, curbWidth, y, curbHeight, false);
            AddLongCurb(mesh, armCenter, half, armLength, curbWidth, y, curbHeight, false);
        }
        else AddLongCurb(mesh, half, 0f, curbWidth, width + curbWidth * 2f, y, curbHeight, true);

        if ((mask & RoadTopologyResolver.West) != 0)
        {
            AddLongCurb(mesh, -armCenter, -half, armLength, curbWidth, y, curbHeight, false);
            AddLongCurb(mesh, -armCenter, half, armLength, curbWidth, y, curbHeight, false);
        }
        else AddLongCurb(mesh, -half, 0f, curbWidth, width + curbWidth * 2f, y, curbHeight, true);
    }

    private static void AddLongCurb(
        MeshBuilder mesh, float x, float z, float xSize, float zSize, float y, float height, bool vertical)
    {
        mesh.AddBox(new Vector3(x, y, z), new Vector3(xSize, height, zSize), Concrete);
        float insetX = vertical ? x + (x < 0f ? 0.004f : -0.004f) : x;
        float insetZ = vertical ? z : z + (z < 0f ? 0.004f : -0.004f);
        mesh.AddBox(new Vector3(insetX, y + height * 0.3f, insetZ),
            new Vector3(vertical ? 0.008f : xSize * 0.82f, 0.012f, vertical ? zSize * 0.82f : 0.008f), Grime);
    }

    private static void AddSurfaceSeams(
        MeshBuilder mesh, int mask, float width, float top, bool highway, bool bridge)
    {
        float lineHeight = 0.004f;
        float lineY = top + lineHeight * 0.5f;
        float seam = highway ? 0.008f : 0.012f;

        if ((mask & (RoadTopologyResolver.North | RoadTopologyResolver.South)) != 0)
        {
            mesh.AddBox(new Vector3(0f, lineY, 0.475f), new Vector3(width, lineHeight, seam), Grime);
            mesh.AddBox(new Vector3(0f, lineY, -0.475f), new Vector3(width, lineHeight, seam), Grime);
        }
        if ((mask & (RoadTopologyResolver.East | RoadTopologyResolver.West)) != 0)
        {
            mesh.AddBox(new Vector3(0.475f, lineY, 0f), new Vector3(seam, lineHeight, width), Grime);
            mesh.AddBox(new Vector3(-0.475f, lineY, 0f), new Vector3(seam, lineHeight, width), Grime);
        }

        if (!highway)
        {
            for (int i = -1; i <= 1; i++)
            {
                float offset = i * 0.2f;
                mesh.AddBox(new Vector3(offset, lineY, 0f), new Vector3(0.006f, lineHeight, width * 0.94f), Grime);
                mesh.AddBox(new Vector3(0f, lineY, offset), new Vector3(width * 0.94f, lineHeight, 0.006f), Grime);
            }
        }

        if (bridge)
        {
            mesh.AddBox(new Vector3(0f, lineY + 0.002f, 0.445f), new Vector3(width, 0.008f, 0.025f), Metal);
            mesh.AddBox(new Vector3(0f, lineY + 0.002f, -0.445f), new Vector3(width, 0.008f, 0.025f), Metal);
        }
    }

    private static void AddLaneDefinition(MeshBuilder mesh, int mask, float width, float top, bool highway)
    {
        float y = top + 0.004f;
        float lineWidth = highway ? 0.025f : 0.014f;
        float dashLength = highway ? 0.19f : 0.13f;
        int degree = CountBits(mask);

        if (degree == 2 && mask == (RoadTopologyResolver.North | RoadTopologyResolver.South))
        {
            for (int i = -1; i <= 1; i++)
                mesh.AddBox(new Vector3(0f, y, i * 0.32f), new Vector3(lineWidth, 0.006f, dashLength), Marking);
            if (highway) AddEdgeLines(mesh, true, width, y);
            return;
        }
        if (degree == 2 && mask == (RoadTopologyResolver.East | RoadTopologyResolver.West))
        {
            for (int i = -1; i <= 1; i++)
                mesh.AddBox(new Vector3(i * 0.32f, y, 0f), new Vector3(dashLength, 0.006f, lineWidth), Marking);
            if (highway) AddEdgeLines(mesh, false, width, y);
            return;
        }

        AddBranchMarking(mesh, mask, RoadTopologyResolver.North, new Vector3(0f, y, 0.39f), new Vector3(lineWidth, 0.006f, dashLength));
        AddBranchMarking(mesh, mask, RoadTopologyResolver.South, new Vector3(0f, y, -0.39f), new Vector3(lineWidth, 0.006f, dashLength));
        AddBranchMarking(mesh, mask, RoadTopologyResolver.East, new Vector3(0.39f, y, 0f), new Vector3(dashLength, 0.006f, lineWidth));
        AddBranchMarking(mesh, mask, RoadTopologyResolver.West, new Vector3(-0.39f, y, 0f), new Vector3(dashLength, 0.006f, lineWidth));

        if (degree <= 1)
        {
            float stopSize = width * 0.72f;
            if ((mask & RoadTopologyResolver.North) != 0)
                mesh.AddBox(new Vector3(0f, y, 0.13f), new Vector3(stopSize, 0.006f, lineWidth), Marking);
            if ((mask & RoadTopologyResolver.South) != 0)
                mesh.AddBox(new Vector3(0f, y, -0.13f), new Vector3(stopSize, 0.006f, lineWidth), Marking);
            if ((mask & RoadTopologyResolver.East) != 0)
                mesh.AddBox(new Vector3(0.13f, y, 0f), new Vector3(lineWidth, 0.006f, stopSize), Marking);
            if ((mask & RoadTopologyResolver.West) != 0)
                mesh.AddBox(new Vector3(-0.13f, y, 0f), new Vector3(lineWidth, 0.006f, stopSize), Marking);
        }
    }

    private static void AddBranchMarking(MeshBuilder mesh, int mask, int direction, Vector3 center, Vector3 size)
    {
        if ((mask & direction) != 0) mesh.AddBox(center, size, Marking);
    }

    private static void AddEdgeLines(MeshBuilder mesh, bool northSouth, float width, float y)
    {
        float offset = width * 0.5f - 0.06f;
        if (northSouth)
        {
            mesh.AddBox(new Vector3(-offset, y, 0f), new Vector3(0.018f, 0.006f, 0.94f), Marking);
            mesh.AddBox(new Vector3(offset, y, 0f), new Vector3(0.018f, 0.006f, 0.94f), Marking);
        }
        else
        {
            mesh.AddBox(new Vector3(0f, y, -offset), new Vector3(0.94f, 0.006f, 0.018f), Marking);
            mesh.AddBox(new Vector3(0f, y, offset), new Vector3(0.94f, 0.006f, 0.018f), Marking);
        }
    }

    private void AddWear(MeshBuilder mesh, int mask, float width, float top, float wear, bool bridge)
    {
        int count = Mathf.RoundToInt(Mathf.Lerp(1f, 5f, bridge ? wear * 0.35f : wear));
        int seed = Mathf.RoundToInt(transform.position.x * 31f + transform.position.z * 73f) ^ mask * 197;
        for (int i = 0; i < count; i++)
        {
            float x = Hash01(seed + i * 11) - 0.5f;
            float z = Hash01(seed + i * 17 + 5) - 0.5f;
            if (!ContainsRoadPoint(x, z, mask, width)) continue;
            float xSize = Mathf.Lerp(0.035f, 0.14f, Hash01(seed + i * 23 + 2));
            float zSize = Mathf.Lerp(0.025f, 0.10f, Hash01(seed + i * 29 + 7));
            mesh.AddBox(new Vector3(x, top + 0.0025f, z), new Vector3(xSize, 0.003f, zSize), i % 3 == 0 ? Earth : Grime);
        }
    }

    private static bool ContainsRoadPoint(float x, float z, int mask, float width)
    {
        float half = width * 0.5f - 0.02f;
        if (Mathf.Abs(x) <= half && Mathf.Abs(z) <= half) return true;
        if ((mask & (RoadTopologyResolver.North | RoadTopologyResolver.South)) != 0 && Mathf.Abs(x) <= half) return true;
        return (mask & (RoadTopologyResolver.East | RoadTopologyResolver.West)) != 0 && Mathf.Abs(z) <= half;
    }

    private static void AddDrainage(MeshBuilder mesh, int mask, float width, float top, bool highway)
    {
        float half = width * 0.5f;
        float y = top + 0.006f;
        float length = highway ? 0.13f : 0.10f;
        if ((mask & RoadTopologyResolver.North) != 0)
            AddGrate(mesh, new Vector3(half - 0.025f, y, 0.31f), new Vector3(0.05f, 0.008f, length), true);
        if ((mask & RoadTopologyResolver.South) != 0)
            AddGrate(mesh, new Vector3(-half + 0.025f, y, -0.31f), new Vector3(0.05f, 0.008f, length), true);
        if ((mask & RoadTopologyResolver.East) != 0)
            AddGrate(mesh, new Vector3(0.31f, y, -half + 0.025f), new Vector3(length, 0.008f, 0.05f), false);
        if ((mask & RoadTopologyResolver.West) != 0)
            AddGrate(mesh, new Vector3(-0.31f, y, half - 0.025f), new Vector3(length, 0.008f, 0.05f), false);
    }

    private static void AddGrate(MeshBuilder mesh, Vector3 center, Vector3 size, bool vertical)
    {
        mesh.AddBox(center, size, Metal);
        for (int i = -1; i <= 1; i++)
        {
            Vector3 offset = vertical ? new Vector3(i * 0.012f, 0.005f, 0f) : new Vector3(0f, 0.005f, i * 0.012f);
            Vector3 slotSize = vertical ? new Vector3(0.004f, 0.004f, size.z * 0.8f) : new Vector3(size.x * 0.8f, 0.004f, 0.004f);
            mesh.AddBox(center + offset, slotSize, Grime);
        }
    }

    private static void AddBridgeApproach(MeshBuilder mesh, int bridgeMask, float width, float top)
    {
        if (bridgeMask == 0) return;
        float y = top + 0.005f;
        if ((bridgeMask & RoadTopologyResolver.North) != 0)
            AddApproachJoint(mesh, new Vector3(0f, y, 0.43f), new Vector3(width, 0.01f, 0.035f));
        if ((bridgeMask & RoadTopologyResolver.South) != 0)
            AddApproachJoint(mesh, new Vector3(0f, y, -0.43f), new Vector3(width, 0.01f, 0.035f));
        if ((bridgeMask & RoadTopologyResolver.East) != 0)
            AddApproachJoint(mesh, new Vector3(0.43f, y, 0f), new Vector3(0.035f, 0.01f, width));
        if ((bridgeMask & RoadTopologyResolver.West) != 0)
            AddApproachJoint(mesh, new Vector3(-0.43f, y, 0f), new Vector3(0.035f, 0.01f, width));
    }

    private static void AddApproachJoint(MeshBuilder mesh, Vector3 center, Vector3 size)
    {
        mesh.AddBox(center, size, Metal);
        Vector3 concreteSize = size.x > size.z
            ? new Vector3(size.x, 0.008f, 0.11f)
            : new Vector3(0.11f, 0.008f, size.z);
        mesh.AddBox(center - Vector3.up * 0.004f, concreteSize, Concrete);
        mesh.AddBox(center + Vector3.up * 0.005f, size, Metal);
    }

    private void AddHighwayHardware(
        MeshBuilder mesh, int mask, int parallelMask, float halfWidth, float top, RoadVisualStyle style)
    {
        bool northSouth = mask == (RoadTopologyResolver.North | RoadTopologyResolver.South);
        bool eastWest = mask == (RoadTopologyResolver.East | RoadTopologyResolver.West);
        if (!northSouth && !eastWest) return;

        float y = top + 0.105f;
        if (northSouth)
        {
            AddGuardrail(mesh, new Vector3(-halfWidth - 0.025f, y, 0f), new Vector3(0.035f, 0.085f, 0.96f), style);
            AddGuardrail(mesh, new Vector3(halfWidth + 0.025f, y, 0f), new Vector3(0.035f, 0.085f, 0.96f), style);
        }
        else
        {
            AddGuardrail(mesh, new Vector3(0f, y, -halfWidth - 0.025f), new Vector3(0.96f, 0.085f, 0.035f), style);
            AddGuardrail(mesh, new Vector3(0f, y, halfWidth + 0.025f), new Vector3(0.96f, 0.085f, 0.035f), style);
        }

        AddParallelMedian(mesh, parallelMask, top, style);
        int tileParity = (Mathf.RoundToInt(transform.position.x) + Mathf.RoundToInt(transform.position.z)) & 1;
        if (tileParity == 0) AddRoadLamp(mesh, northSouth, halfWidth, top, style);
    }

    private static void AddGuardrail(MeshBuilder mesh, Vector3 center, Vector3 size, RoadVisualStyle style)
    {
        mesh.AddBox(center, size, style == RoadVisualStyle.TycoonHighway ? Concrete : Metal);
        Vector3 accentSize = new Vector3(size.x * 1.08f, 0.018f, size.z * 0.86f);
        mesh.AddBox(center + Vector3.up * 0.012f, accentSize, Accent);
    }

    private static void AddParallelMedian(MeshBuilder mesh, int parallelMask, float top, RoadVisualStyle style)
    {
        if (parallelMask == 0) return;
        float y = top + 0.045f;
        if ((parallelMask & RoadTopologyResolver.East) != 0)
            AddMedian(mesh, new Vector3(0.455f, y, 0f), new Vector3(0.07f, 0.07f, 0.84f), style, true);
        if ((parallelMask & RoadTopologyResolver.West) != 0)
            AddMedian(mesh, new Vector3(-0.455f, y, 0f), new Vector3(0.07f, 0.07f, 0.84f), style, true);
        if ((parallelMask & RoadTopologyResolver.North) != 0)
            AddMedian(mesh, new Vector3(0f, y, 0.455f), new Vector3(0.84f, 0.07f, 0.07f), style, false);
        if ((parallelMask & RoadTopologyResolver.South) != 0)
            AddMedian(mesh, new Vector3(0f, y, -0.455f), new Vector3(0.84f, 0.07f, 0.07f), style, false);
    }

    private static void AddMedian(MeshBuilder mesh, Vector3 center, Vector3 size, RoadVisualStyle style, bool vertical)
    {
        mesh.AddBox(center, size, Concrete);
        Vector3 inset = vertical
            ? new Vector3(size.x * 0.62f, 0.025f, size.z * 0.82f)
            : new Vector3(size.x * 0.82f, 0.025f, size.z * 0.62f);
        mesh.AddBox(center + Vector3.up * 0.045f, inset, style == RoadVisualStyle.EcoHighway ? Earth : Accent);
        if (style != RoadVisualStyle.EcoHighway) return;

        for (int i = -1; i <= 1; i++)
        {
            Vector3 offset = vertical ? new Vector3(0f, 0.09f, i * 0.24f) : new Vector3(i * 0.24f, 0.09f, 0f);
            mesh.AddBox(center + offset, new Vector3(0.055f, 0.10f, 0.055f), Accent);
        }
    }

    private static void AddRoadLamp(
        MeshBuilder mesh, bool northSouth, float halfWidth, float top, RoadVisualStyle style)
    {
        Vector3 basePosition = northSouth
            ? new Vector3(halfWidth + 0.075f, top + 0.18f, 0.20f)
            : new Vector3(0.20f, top + 0.18f, halfWidth + 0.075f);
        mesh.AddBox(basePosition, new Vector3(0.026f, 0.36f, 0.026f), Metal);
        Vector3 armSize = northSouth ? new Vector3(0.12f, 0.025f, 0.025f) : new Vector3(0.025f, 0.025f, 0.12f);
        Vector3 armOffset = northSouth ? new Vector3(-0.05f, 0.17f, 0f) : new Vector3(0f, 0.17f, -0.05f);
        mesh.AddBox(basePosition + armOffset, armSize, style == RoadVisualStyle.TycoonHighway ? Metal : Accent);
        Vector3 lampOffset = northSouth ? new Vector3(-0.105f, 0.155f, 0f) : new Vector3(0f, 0.155f, -0.105f);
        mesh.AddBox(basePosition + lampOffset, new Vector3(0.045f, 0.018f, 0.045f), Lamp);
    }

    private static int CountBits(int mask)
    {
        int count = 0;
        while (mask != 0) { count += mask & 1; mask >>= 1; }
        return count;
    }

    private static float Hash01(int value)
    {
        unchecked
        {
            uint x = (uint)value;
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return (x & 0x00ffffff) / 16777215f;
        }
    }

    private static Material[] GetPalette(RoadVisualStyle style)
    {
        if (Palettes.TryGetValue(style, out Material[] palette)) return palette;

        Color surface;
        Color concrete;
        Color marking;
        Color accent;
        switch (style)
        {
            case RoadVisualStyle.Highway:
                surface = new Color(0.18f, 0.19f, 0.18f);
                concrete = new Color(0.39f, 0.40f, 0.37f);
                marking = new Color(0.82f, 0.79f, 0.62f);
                accent = new Color(0.37f, 0.45f, 0.45f);
                break;
            case RoadVisualStyle.TycoonHighway:
                surface = new Color(0.22f, 0.19f, 0.17f);
                concrete = new Color(0.43f, 0.38f, 0.32f);
                marking = new Color(0.82f, 0.70f, 0.49f);
                accent = new Color(0.57f, 0.31f, 0.14f);
                break;
            case RoadVisualStyle.EcoHighway:
                surface = new Color(0.29f, 0.31f, 0.27f);
                concrete = new Color(0.51f, 0.52f, 0.45f);
                marking = new Color(0.83f, 0.82f, 0.69f);
                accent = new Color(0.18f, 0.40f, 0.29f);
                break;
            case RoadVisualStyle.TechHighway:
                surface = new Color(0.27f, 0.29f, 0.29f);
                concrete = new Color(0.52f, 0.53f, 0.50f);
                marking = new Color(0.83f, 0.84f, 0.76f);
                accent = new Color(0.12f, 0.42f, 0.48f);
                break;
            default:
                surface = new Color(0.25f, 0.24f, 0.22f);
                concrete = new Color(0.43f, 0.42f, 0.37f);
                marking = new Color(0.68f, 0.65f, 0.54f);
                accent = new Color(0.35f, 0.37f, 0.34f);
                break;
        }

        palette = new[]
        {
            CreateMaterial($"{style} worn surface", surface, 0.16f, 0f, true, (int)style * 31 + 3),
            CreateMaterial($"{style} curb concrete", concrete, 0.20f, 0f, true, (int)style * 31 + 7),
            CreateMaterial($"{style} road marking", marking, 0.28f, 0f, false, 0),
            CreateMaterial($"{style} seams and tyre wear", new Color(0.105f, 0.095f, 0.08f), 0.08f, 0f, false, 0),
            CreateMaterial($"{style} road metal", new Color(0.20f, 0.23f, 0.22f), 0.34f, 0.42f, true, 91),
            CreateMaterial($"{style} restrained accent", accent, 0.31f, 0.16f, true, (int)style * 31 + 13),
            CreateMaterial($"{style} verge dirt", new Color(0.19f, 0.16f, 0.115f), 0.05f, 0f, true, 47),
            CreateMaterial($"{style} warm road lamp", new Color(0.78f, 0.72f, 0.48f), 0.5f, 0f, false, 0, true)
        };
        Palettes[style] = palette;
        return palette;
    }

    private static Material CreateMaterial(
        string name, Color color, float smoothness, float metallic, bool textured, int textureSeed, bool emissive = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader)
        {
            name = name,
            enableInstancing = true,
            hideFlags = HideFlags.HideAndDontSave
        };
        material.SetColor("_BaseColor", textured ? Color.white : color);
        material.SetColor("_Color", textured ? Color.white : color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Glossiness", smoothness);
        material.SetFloat("_Metallic", metallic);
        if (textured)
        {
            Texture2D texture = CreateVariationTexture(color, textureSeed);
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
        }
        if (emissive)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.65f);
        }
        return material;
    }

    private static Texture2D CreateVariationTexture(Color baseColor, int seed)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = "Road micro variation",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 2,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fine = Hash01(seed + x * 92821 + y * 68917);
                float broad = Hash01(seed + (x / 8) * 193 + (y / 8) * 389);
                float variation = (fine - 0.5f) * 0.12f + (broad - 0.5f) * 0.09f;
                if ((x + seed) % 29 == 0 && (y + seed) % 7 < 4) variation -= 0.06f;
                pixels[y * size + x] = new Color(
                    Mathf.Clamp01(baseColor.r + variation),
                    Mathf.Clamp01(baseColor.g + variation),
                    Mathf.Clamp01(baseColor.b + variation), 1f);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(true, true);
        return texture;
    }

    private sealed class MeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int>[] triangles;

        public MeshBuilder(int materialCount)
        {
            triangles = new List<int>[materialCount];
            for (int i = 0; i < materialCount; i++) triangles[i] = new List<int>();
        }

        public void AddBox(Vector3 center, Vector3 size, int material)
        {
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f) return;
            Vector3 half = size * 0.5f;
            Vector3 min = center - half;
            Vector3 max = center + half;
            AddFace(new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z), Vector3.up, material);
            AddFace(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), Vector3.down, material);
            AddFace(new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), Vector3.forward, material);
            AddFace(new Vector3(max.x, min.y, min.z), new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), Vector3.back, material);
            AddFace(new Vector3(max.x, min.y, max.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), Vector3.right, material);
            AddFace(new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), Vector3.left, material);
        }

        private void AddFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, int material)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            normals.Add(normal); normals.Add(normal); normals.Add(normal); normals.Add(normal);
            uvs.Add(new Vector2(a.x, a.z) * 3f);
            uvs.Add(new Vector2(b.x, b.z) * 3f);
            uvs.Add(new Vector2(c.x, c.z) * 3f);
            uvs.Add(new Vector2(d.x, d.z) * 3f);
            triangles[material].Add(start);
            triangles[material].Add(start + 1);
            triangles[material].Add(start + 2);
            triangles[material].Add(start);
            triangles[material].Add(start + 2);
            triangles[material].Add(start + 3);
        }

        public Mesh Create(string meshName)
        {
            Mesh mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i, false);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

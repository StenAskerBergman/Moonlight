using UnityEngine;

/// <summary>
/// Visible fallback for bridge definitions that do not yet have authored prefabs.
/// It deliberately uses modular one-cell pieces so crossings of any supported span
/// remain continuous while artists can replace individual tiers later.
/// </summary>
public static class ProceduralBridgeTileBuilder
{
    public static void Build(Transform parent, BridgeAppearance bridge, float rotation)
    {
        Build(parent, bridge, rotation, RoadVisualStyle.CityRoad);
    }

    public static void Build(Transform parent, BridgeAppearance bridge, float rotation, RoadVisualStyle style)
    {
        GameObject root = new GameObject($"{bridge.TransportMode} {bridge.Tier} {bridge.Structure}");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.up * bridge.DeckHeight;
        root.transform.localRotation = Quaternion.Euler(0f, rotation, 0f);

        switch (bridge.Structure)
        {
            case BridgeStructureType.TimberTrestle:
                BuildTimber(root.transform, bridge, style);
                break;
            case BridgeStructureType.MasonryArch:
                BuildMasonry(root.transform, bridge, style);
                break;
            case BridgeStructureType.SteelTruss:
                BuildSteel(root.transform, bridge, true, style);
                break;
            default:
                BuildSteel(root.transform, bridge, false, style);
                break;
        }

        if (bridge.TransportMode == BridgeTransportMode.Railway)
            BuildRailDeck(root.transform);
    }

    private static void BuildTimber(Transform root, BridgeAppearance bridge, RoadVisualStyle style)
    {
        Color wood = new Color(0.31f, 0.17f, 0.08f);
        AddBeam(root, "Timber deck structure", new Vector3(0f, -0.045f, 0f), new Vector3(0.76f, 0.10f, 1.02f), wood);
        AddBeam(root, "Left handrail", new Vector3(-0.39f, 0.15f, 0f), new Vector3(0.045f, 0.055f, 1.04f), wood);
        AddBeam(root, "Right handrail", new Vector3(0.39f, 0.15f, 0f), new Vector3(0.045f, 0.055f, 1.04f), wood);
        AddRailPosts(root, 0.39f, wood, 0.22f);
        AddExpansionJoints(root, 0.62f);
        AddSupports(root, bridge, wood, 0.09f);
    }

    private static void BuildMasonry(Transform root, BridgeAppearance bridge, RoadVisualStyle style)
    {
        Color stone = BridgeConcrete(style);
        AddBeam(root, "Stone deck structure", new Vector3(0f, -0.06f, 0f), new Vector3(0.84f, 0.13f, 1.04f), stone);
        AddBeam(root, "Left parapet", new Vector3(-0.43f, 0.13f, 0f), new Vector3(0.075f, 0.18f, 1.04f), stone);
        AddBeam(root, "Right parapet", new Vector3(0.43f, 0.13f, 0f), new Vector3(0.075f, 0.18f, 1.04f), stone);
        AddExpansionJoints(root, 0.72f);
        AddSupports(root, bridge, stone, 0.17f);
    }

    private static void BuildSteel(Transform root, BridgeAppearance bridge, bool truss, RoadVisualStyle style)
    {
        Color steel = BridgeSteel(style);
        AddBeam(root, "Steel deck structure", new Vector3(0f, -0.05f, 0f), new Vector3(0.84f, 0.11f, 1.04f), BridgeConcrete(style));
        AddBeam(root, "Left girder", new Vector3(-0.43f, 0.13f, 0f), new Vector3(0.065f, truss ? 0.58f : 0.18f, 1.04f), steel);
        AddBeam(root, "Right girder", new Vector3(0.43f, 0.13f, 0f), new Vector3(0.065f, truss ? 0.58f : 0.18f, 1.04f), steel);
        if (truss)
        {
            AddBeam(root, "Left top chord", new Vector3(-0.43f, 0.43f, 0f), new Vector3(0.065f, 0.065f, 1.04f), steel);
            AddBeam(root, "Right top chord", new Vector3(0.43f, 0.43f, 0f), new Vector3(0.065f, 0.065f, 1.04f), steel);
            AddDiagonal(root, new Vector3(-0.43f, 0.29f, 0f), 27f, steel);
            AddDiagonal(root, new Vector3(0.43f, 0.29f, 0f), -27f, steel);
        }
        else AddRailPosts(root, 0.43f, steel, 0.20f);
        AddExpansionJoints(root, 0.72f);
        AddSupports(root, bridge, steel, 0.12f);
    }

    private static void AddRailPosts(Transform root, float x, Color color, float height)
    {
        for (int i = -2; i <= 2; i++)
        {
            AddBeam(root, "Left railing post", new Vector3(-x, height * 0.5f, i * 0.22f), new Vector3(0.035f, height, 0.035f), color);
            AddBeam(root, "Right railing post", new Vector3(x, height * 0.5f, i * 0.22f), new Vector3(0.035f, height, 0.035f), color);
        }
    }

    private static void AddExpansionJoints(Transform root, float width)
    {
        Color joint = new Color(0.08f, 0.085f, 0.08f);
        AddBeam(root, "North expansion joint", new Vector3(0f, 0.027f, 0.465f), new Vector3(width, 0.018f, 0.026f), joint);
        AddBeam(root, "South expansion joint", new Vector3(0f, 0.027f, -0.465f), new Vector3(width, 0.018f, 0.026f), joint);
    }

    private static Color BridgeConcrete(RoadVisualStyle style)
    {
        switch (style)
        {
            case RoadVisualStyle.TycoonHighway: return new Color(0.40f, 0.34f, 0.28f);
            case RoadVisualStyle.EcoHighway: return new Color(0.47f, 0.49f, 0.43f);
            case RoadVisualStyle.TechHighway: return new Color(0.47f, 0.49f, 0.48f);
            default: return new Color(0.38f, 0.38f, 0.35f);
        }
    }

    private static Color BridgeSteel(RoadVisualStyle style)
    {
        switch (style)
        {
            case RoadVisualStyle.TycoonHighway: return new Color(0.34f, 0.22f, 0.14f);
            case RoadVisualStyle.EcoHighway: return new Color(0.16f, 0.32f, 0.24f);
            case RoadVisualStyle.TechHighway: return new Color(0.13f, 0.31f, 0.35f);
            default: return new Color(0.20f, 0.25f, 0.23f);
        }
    }

    private static void BuildRailDeck(Transform root)
    {
        Color rail = new Color(0.10f, 0.10f, 0.09f);
        AddBeam(root, "Left rail", new Vector3(-0.22f, 0.13f, 0f), new Vector3(0.045f, 0.045f, 1.08f), rail);
        AddBeam(root, "Right rail", new Vector3(0.22f, 0.13f, 0f), new Vector3(0.045f, 0.045f, 1.08f), rail);
        for (int i = -2; i <= 2; i++)
            AddBeam(root, "Sleeper", new Vector3(0f, 0.105f, i * 0.2f), new Vector3(0.72f, 0.035f, 0.075f), new Color(0.25f, 0.14f, 0.07f));
    }

    private static void AddSupports(Transform root, BridgeAppearance bridge, Color color, float width)
    {
        bool placePier = bridge.SpanIndex == 0
            || bridge.SpanIndex == bridge.SpanLength - 1
            || bridge.SpanIndex % bridge.PierSpacing == 0;
        if (!placePier) return;
        AddBeam(root, "Left pier", new Vector3(-0.34f, -0.55f, 0f), new Vector3(width, 1.1f, width), color);
        AddBeam(root, "Right pier", new Vector3(0.34f, -0.55f, 0f), new Vector3(width, 1.1f, width), color);
    }

    private static void AddDiagonal(Transform root, Vector3 position, float zRotation, Color color)
    {
        GameObject beam = AddBeam(root, "Truss diagonal", position, new Vector3(0.055f, 0.62f, 0.055f), color);
        beam.transform.localRotation = Quaternion.Euler(zRotation, 0f, 0f);
    }

    private static GameObject AddBeam(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
    {
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = name;
        beam.transform.SetParent(parent, false);
        beam.transform.localPosition = position;
        beam.transform.localScale = scale;
        Collider collider = beam.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
        }
        Renderer renderer = beam.GetComponent<Renderer>();
        if (renderer != null)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
        }
        return beam;
    }
}

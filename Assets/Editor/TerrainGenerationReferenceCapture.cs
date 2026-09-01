using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class TerrainGenerationReferenceCaptureScheduler
{
    private static MapManager pendingMapManager;

    static TerrainGenerationReferenceCaptureScheduler()
    {
        MapManager.OnMapGenerated -= ScheduleCapture;
        MapManager.OnMapGenerated += ScheduleCapture;
        MapManager.OnMapGenerationFailed -= CaptureFailedMap;
        MapManager.OnMapGenerationFailed += CaptureFailedMap;
    }

    private static void ScheduleCapture()
    {
        if (Application.isPlaying) return;

        pendingMapManager = UnityEngine.Object.FindObjectOfType<MapManager>();

        // OnMapGenerated fires immediately before MapManager stores the final elapsed
        // time. Delay one editor tick so the capture receives the completed timing.
        EditorApplication.delayCall -= CaptureCompletedMap;
        EditorApplication.delayCall += CaptureCompletedMap;
    }

    private static void CaptureCompletedMap()
    {
        if (Application.isPlaying) return;

        MapManager mapManager = pendingMapManager;
        pendingMapManager = null;
        if (mapManager == null || !string.IsNullOrEmpty(mapManager.LastGenerationBreakStatus)) return;
        if (!TerrainGenerationReferenceCapture.HasCaptureableIslands(mapManager)) return;

        try
        {
            TerrainGenerationReferenceCapture.CaptureLatest(mapManager);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, mapManager);
        }
    }

    private static void CaptureFailedMap(MapManager mapManager, string failureStatus)
    {
        if (Application.isPlaying || mapManager == null) return;

        EditorApplication.delayCall -= CaptureCompletedMap;
        pendingMapManager = null;

        // This event is synchronous because MapManager destroys partial chunks as soon
        // as its failure handlers return.
        TerrainGenerationReferenceCapture.CaptureFailed(mapManager, failureStatus);
    }
}

/// <summary>
/// Produces disposable, consistently framed terrain references for human and AI review.
/// Output intentionally lives under Temp and is replaced on every capture.
/// </summary>
public static class TerrainGenerationReferenceCapture
{
    private const int DefaultResolution = 512;
    private const int CaptureLayer = 31;
    private const float FramingPadding = 1.08f;
    private static readonly int TerrainBaseMapProperty = Shader.PropertyToID("_BaseMap");

    public static string LatestOutputDirectory
    {
        get
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Could not resolve the Unity project root.");
            return Path.Combine(projectRoot, "Temp", "TerrainGenerationReferences", "Latest");
        }
    }

    public static string LatestFailedOutputDirectory
    {
        get
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Could not resolve the Unity project root.");
            return Path.Combine(projectRoot, "Temp", "TerrainGenerationReferences", "LatestFailed");
        }
    }

    public static string CaptureLatest(MapManager mapManager, int resolution = DefaultResolution)
    {
        return Capture(
            mapManager,
            LatestOutputDirectory,
            LatestOutputDirectory,
            "Completed",
            false,
            resolution);
    }

    public static string CaptureFailed(
        MapManager mapManager,
        string failureStatus,
        int resolution = DefaultResolution)
    {
        return Capture(
            mapManager,
            LatestFailedOutputDirectory,
            LatestOutputDirectory,
            failureStatus,
            true,
            resolution);
    }

    public static bool HasCaptureableIslands(MapManager mapManager)
    {
        return mapManager != null && ResolveGeneratedIslands(mapManager).Count > 0;
    }

    private static string Capture(
        MapManager mapManager,
        string outputDirectory,
        string baselineDirectory,
        string generationStatus,
        bool isPartialFailure,
        int resolution)
    {
        if (mapManager == null) throw new ArgumentNullException(nameof(mapManager));

        resolution = Mathf.Clamp(resolution, 128, 2048);
        List<Island> islands = ResolveGeneratedIslands(mapManager);
        if (!isPartialFailure && islands.Count == 0)
        {
            throw new InvalidOperationException("No generated islands were found. Generate the map before capturing references.");
        }

        CaptureManifest previousManifest = ReadPreviousManifest(baselineDirectory);
        long previousGenerationMs = previousManifest != null ? previousManifest.totalGenerationMs : -1L;
        ReplaceOutputDirectory(outputDirectory);

        var manifest = new CaptureManifest
        {
            generatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            scene = SceneManager.GetActiveScene().path,
            resolutionPerView = resolution,
            viewLayout = "TOP on left; SIDE (looking north from world -Z) on right",
            generationStatus = generationStatus,
            isPartialFailure = isPartialFailure,
            failureChunk = isPartialFailure ? GenerationWatchdog.CurrentChunk : string.Empty,
            failurePhase = isPartialFailure ? GenerationWatchdog.CurrentPhase : string.Empty,
            totalGenerationMs = mapManager.LastGenerationTimeMs,
            previousTotalGenerationMs = previousGenerationMs,
            islandCount = islands.Count,
            selectionInverted = mapManager.IsSelectionInverted,
        };
        manifest.hasPreviousGeneration = previousGenerationMs >= 0L;
        if (manifest.hasPreviousGeneration)
        {
            manifest.generationDeltaMs = manifest.totalGenerationMs - previousGenerationMs;
            manifest.generationDeltaPercent = previousGenerationMs > 0L
                ? manifest.generationDeltaMs * 100f / previousGenerationMs
                : 0f;
        }

        GameObject cameraObject = null;
        if (islands.Count > 0)
        {
            cameraObject = new GameObject("Terrain Reference Capture Camera")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            Camera captureCamera = cameraObject.AddComponent<Camera>();
            ConfigureCamera(captureCamera);

            try
            {
                foreach (Island island in islands)
                {
                    CaptureIsland(island, captureCamera, resolution, outputDirectory, manifest);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        File.WriteAllText(
            Path.Combine(outputDirectory, "manifest.json"),
            JsonUtility.ToJson(manifest, true));
        File.WriteAllText(
            Path.Combine(outputDirectory, "index.md"),
            BuildMarkdownIndex(manifest));

        Debug.Log(
            $"<color=cyan>[Terrain Reference Capture]</color> Wrote {(isPartialFailure ? "failed/partial" : "successful")} " +
            $"reference with {manifest.islands.Count} indexed top/side composites to '{outputDirectory}'.");
        return outputDirectory;
    }

    private static List<Island> ResolveGeneratedIslands(MapManager mapManager)
    {
        IEnumerable<Island> candidates = mapManager.islands != null && mapManager.islands.Count > 0
            ? mapManager.islands
            : mapManager.GetComponentsInChildren<Island>(true);

        return candidates
            .Where(island => island != null && island.gameObject.activeInHierarchy)
            .Distinct()
            .OrderBy(island => island.id)
            .ThenBy(island => island.name, StringComparer.Ordinal)
            .ToList();
    }

    private static void ReplaceOutputDirectory(string outputDirectory)
    {
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, true);
        }

        Directory.CreateDirectory(outputDirectory);
    }

    private static CaptureManifest ReadPreviousManifest(string outputDirectory)
    {
        string manifestPath = Path.Combine(outputDirectory, "manifest.json");
        if (!File.Exists(manifestPath)) return null;

        try
        {
            return JsonUtility.FromJson<CaptureManifest>(File.ReadAllText(manifestPath));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not read previous terrain-reference timing: {exception.Message}");
            return null;
        }
    }

    private static void ConfigureCamera(Camera camera)
    {
        camera.enabled = false;
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f, 1f);
        camera.cullingMask = 1 << CaptureLayer;
        camera.allowHDR = false;
        camera.allowMSAA = true;
        camera.useOcclusionCulling = false;
    }

    private static void CaptureIsland(
        Island island,
        Camera camera,
        int resolution,
        string outputDirectory,
        CaptureManifest manifest)
    {
        Bounds bounds = CalculateRenderableBounds(island);
        List<LayerState> originalLayers = MoveHierarchyToCaptureLayer(island.transform);
        Texture2D top = null;
        Texture2D side = null;
        Texture2D composite = null;
        Texture2D controlTexture = null;
        Texture2D gameplayDebugTexture = null;

        try
        {
            top = RenderTopView(camera, bounds, resolution);
            side = RenderSideView(camera, bounds, resolution);
            composite = BuildComposite(top, side, island.id, manifest.selectionInverted);

            string fileName = $"island-{island.id:D3}_top-left_side-right.png";
            File.WriteAllBytes(Path.Combine(outputDirectory, fileName), composite.EncodeToPNG());

            List<string> crudeOilDepositFiles = CaptureCrudeOilDepositCloseups(
                island,
                camera,
                resolution,
                outputDirectory);

            string controlTextureFile = null;
            if (TryBuildControlTextureReference(island, manifest.selectionInverted, out controlTexture))
            {
                controlTextureFile = $"island-{island.id:D3}_splat-control.png";
                File.WriteAllBytes(
                    Path.Combine(outputDirectory, controlTextureFile),
                    controlTexture.EncodeToPNG());
            }

            string gameplayDebugFile = null;
            if (controlTexture != null
                && TryBuildGameplayDebugReference(island, controlTexture, manifest.selectionInverted, out gameplayDebugTexture))
            {
                gameplayDebugFile = $"island-{island.id:D3}_gameplay-grid.png";
                File.WriteAllBytes(
                    Path.Combine(outputDirectory, gameplayDebugFile),
                    gameplayDebugTexture.EncodeToPNG());
            }

            manifest.islands.Add(new CaptureEntry
            {
                islandNumber = island.id,
                islandName = island.gameObject.name,
                file = fileName,
                controlTextureFile = controlTextureFile,
                gameplayDebugFile = gameplayDebugFile,
                crudeOilDepositFiles = crudeOilDepositFiles,
                boundsCenter = FormatVector(bounds.center),
                boundsSize = FormatVector(bounds.size),
                timing = island.GetComponent<MapGrid>()?.LastGenerationProfile,
            });

            TerrainGenerationProfile timing = manifest.islands[manifest.islands.Count - 1].timing;
            if (timing != null)
            {
                manifest.maximumVisualSamplesPerCell = Mathf.Max(
                    manifest.maximumVisualSamplesPerCell,
                    timing.visualSamplesPerCell);
            }
        }
        finally
        {
            RestoreLayers(originalLayers);
            if (top != null) UnityEngine.Object.DestroyImmediate(top);
            if (side != null) UnityEngine.Object.DestroyImmediate(side);
            if (composite != null) UnityEngine.Object.DestroyImmediate(composite);
            if (controlTexture != null) UnityEngine.Object.DestroyImmediate(controlTexture);
            if (gameplayDebugTexture != null) UnityEngine.Object.DestroyImmediate(gameplayDebugTexture);
        }
    }

    private static bool TryBuildControlTextureReference(
        Island island,
        bool selectionInverted,
        out Texture2D reference)
    {
        reference = null;
        MapGrid mapGrid = island.GetComponent<MapGrid>() ?? island.GetComponentInChildren<MapGrid>(true);
        if (mapGrid == null) return false;

        int visualSamples = mapGrid.generationSettings != null ? mapGrid.generationSettings.visualSamplesPerCell : 1;
        TextureBuilder builder = new TextureBuilder(mapGrid.Grid, mapGrid.TerrainSource, visualSamples, mapGrid.climateProfile);
        reference = builder.BuildDiagnosticSplatMask();
        if (reference == null) return false;

        int bannerHeight = Mathf.Max(58, reference.height / 12);
        DrawFilledRectangle(
            reference,
            0,
            reference.height - bannerHeight,
            reference.width,
            bannerHeight,
            new Color32(0, 0, 0, 230));
        int glyphScale = Mathf.Max(2, reference.width / 250);
        DrawPixelText(
            reference,
            12,
            reference.height - 12 - 7 * glyphScale,
            $"ISLAND {island.id:D3} - SPLAT",
            glyphScale,
            new Color32(255, 255, 255, 255));
        DrawInvertedFlag(reference, selectionInverted, reference.width - 196, reference.height - 12 - 7 * glyphScale, glyphScale);
        int legendY = reference.height - bannerHeight + 8;
        if (mapGrid.currentGridType == GridType.Type.Plateau)
        {
            DrawLegendItem(reference, 12, legendY, "SAND / TOP", new Color32(0, 255, 0, 255), glyphScale);
            DrawLegendItem(reference, 188, legendY, "ROCK / RIM", new Color32(0, 0, 255, 255), glyphScale);
            DrawLegendItem(reference, 372, legendY, "ABYSS", new Color32(0, 0, 0, 255), glyphScale, new Color32(255, 255, 255, 255));
        }
        else
        {
            DrawLegendItem(reference, 12, legendY, "MAINLAND", new Color32(255, 0, 0, 255), glyphScale);
            DrawLegendItem(reference, 154, legendY, "BEACH", new Color32(0, 255, 0, 255), glyphScale);
            DrawLegendItem(reference, 264, legendY, "MOUNTAIN", new Color32(0, 0, 255, 255), glyphScale);
            DrawLegendItem(reference, 414, legendY, "WATER", new Color32(0, 0, 0, 255), glyphScale, new Color32(255, 255, 255, 255));
        }
        reference.Apply(false, false);
        return true;
    }

    private static bool TryBuildGameplayDebugReference(
        Island island,
        Texture2D splatReference,
        bool selectionInverted,
        out Texture2D reference)
    {
        reference = null;
        MapGrid mapGrid = island.GetComponent<MapGrid>() ?? island.GetComponentInChildren<MapGrid>(true);
        GridSystem gridSystem = island.GetComponent<GridSystem>() ?? island.GetComponentInChildren<GridSystem>(true);
        Cell[,] grid = mapGrid != null ? mapGrid.Grid : null;
        if (grid == null || gridSystem == null) return false;

        reference = CopyTexture(splatReference, $"Island {island.id:D3} Gameplay Grid Reference");
        int cellsX = grid.GetLength(0);
        int cellsZ = grid.GetLength(1);
        float pixelsPerCellX = reference.width / (float)cellsX;
        float pixelsPerCellZ = reference.height / (float)cellsZ;
        int gridLineWidth = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(pixelsPerCellX, pixelsPerCellZ) * 0.08f));
        int outlineWidth = Mathf.Max(2, gridLineWidth + 1);

        for (int z = 0; z < cellsZ; z++)
        {
            for (int x = 0; x < cellsX; x++)
            {
                Cell cell = grid[x, z];
                bool buildable = IsValidConstructionCell(gridSystem, cell);
                int x0 = Mathf.RoundToInt(x * pixelsPerCellX);
                int x1 = Mathf.RoundToInt((x + 1) * pixelsPerCellX);
                int y0 = Mathf.RoundToInt(z * pixelsPerCellZ);
                int y1 = Mathf.RoundToInt((z + 1) * pixelsPerCellZ);

                BlendFilledRectangle(
                    reference,
                    x0,
                    y0,
                    Mathf.Max(1, x1 - x0),
                    Mathf.Max(1, y1 - y0),
                    buildable ? new Color32(40, 255, 90, 54) : new Color32(255, 35, 95, 38));

                DrawRectangleOutline(
                    reference,
                    x0,
                    y0,
                    Mathf.Max(1, x1 - x0),
                    Mathf.Max(1, y1 - y0),
                    gridLineWidth,
                    new Color32(255, 255, 255, 42),
                    true);

                if (buildable)
                {
                    Color32 buildableOutline = new Color32(0, 245, 255, 255);
                    if (x == 0 || !IsValidConstructionCell(gridSystem, grid[x - 1, z]))
                        DrawFilledRectangle(reference, x0, y0, outlineWidth, y1 - y0, buildableOutline);
                    if (x == cellsX - 1 || !IsValidConstructionCell(gridSystem, grid[x + 1, z]))
                        DrawFilledRectangle(reference, x1 - outlineWidth, y0, outlineWidth, y1 - y0, buildableOutline);
                    if (z == 0 || !IsValidConstructionCell(gridSystem, grid[x, z - 1]))
                        DrawFilledRectangle(reference, x0, y0, x1 - x0, outlineWidth, buildableOutline);
                    if (z == cellsZ - 1 || !IsValidConstructionCell(gridSystem, grid[x, z + 1]))
                        DrawFilledRectangle(reference, x0, y1 - outlineWidth, x1 - x0, outlineWidth, buildableOutline);
                }

                if (cell != null && cell.isDeposit && cell.depositNodeType != ResourceNodeType.None)
                {
                    int centerX = Mathf.RoundToInt((x + 0.5f) * pixelsPerCellX);
                    int centerY = Mathf.RoundToInt((z + 0.5f) * pixelsPerCellZ);
                    DrawResourceMarker(reference, centerX, centerY, cell.depositNodeType);
                }
            }
        }

        DrawGameplayLegend(reference, island.id, selectionInverted);
        reference.Apply(false, false);
        return true;
    }

    private static bool IsValidConstructionCell(GridSystem gridSystem, Cell cell)
    {
        return gridSystem.IsValidSurfaceConstructionCell(cell)
            || gridSystem.IsValidUnderwaterPlateauCell(cell);
    }

    private static void DrawGameplayLegend(Texture2D texture, int islandNumber, bool selectionInverted)
    {
        int glyphScale = Mathf.Max(2, texture.width / 250);
        int bannerHeight = Mathf.Max(136, texture.height / 7);
        DrawFilledRectangle(texture, 0, texture.height - bannerHeight, texture.width, bannerHeight, new Color32(0, 0, 0, 235));
        DrawPixelText(texture, 12, texture.height - 12 - 7 * glyphScale, $"ISLAND {islandNumber:D3} - GAMEPLAY GRID", glyphScale, new Color32(255, 255, 255, 255));
        DrawInvertedFlag(texture, selectionInverted, texture.width - 196, texture.height - 12 - 7 * glyphScale, glyphScale);

        int rowOne = texture.height - bannerHeight + 74;
        DrawLegendItem(texture, 12, rowOne, "CAN PLACE", new Color32(40, 255, 90, 255), glyphScale);
        DrawLegendItem(texture, 220, rowOne, "CANNOT", new Color32(255, 35, 95, 255), glyphScale);
        DrawLegendItem(texture, 390, rowOne, "BUILDABLE EDGE", new Color32(0, 245, 255, 255), glyphScale);

        int rowTwo = texture.height - bannerHeight + 42;
        DrawLegendItem(texture, 12, rowTwo, "MI MINE", ResourceColor(ResourceNodeType.Mine), glyphScale);
        DrawLegendItem(texture, 150, rowTwo, "FG FOREST", ResourceColor(ResourceNodeType.ForestGrove), glyphScale);
        DrawLegendItem(texture, 320, rowTwo, "RB RIVER", ResourceColor(ResourceNodeType.RiverBank), glyphScale);
        DrawLegendItem(texture, 485, rowTwo, "LM MOUTH", ResourceColor(ResourceNodeType.LakeMouth), glyphScale);
        DrawLegendItem(texture, 650, rowTwo, "CF FISH", ResourceColor(ResourceNodeType.CoastalFishery), glyphScale);

        int rowThree = texture.height - bannerHeight + 10;
        DrawLegendItem(texture, 12, rowThree, "OS ORE SEABED", ResourceColor(ResourceNodeType.OreSeabed), glyphScale);
        DrawLegendItem(texture, 250, rowThree, "HV VENT", ResourceColor(ResourceNodeType.HydrothermalVent), glyphScale);
        DrawLegendItem(texture, 430, rowThree, "CO CRUDE OIL", ResourceColor(ResourceNodeType.CrudeOil), glyphScale);
    }

    private static void DrawLegendItem(
        Texture2D texture,
        int x,
        int y,
        string label,
        Color32 swatch,
        int glyphScale,
        Color32? border = null)
    {
        int size = 9 * glyphScale;
        DrawFilledRectangle(texture, x, y, size, size, border ?? new Color32(255, 255, 255, 255));
        DrawFilledRectangle(texture, x + glyphScale, y + glyphScale, size - glyphScale * 2, size - glyphScale * 2, swatch);
        DrawPixelText(texture, x + size + 4 * glyphScale, y + glyphScale, label, glyphScale, new Color32(255, 255, 255, 255));
    }

    private static void DrawInvertedFlag(Texture2D texture, bool selectionInverted, int x, int y, int glyphScale)
    {
        if (!selectionInverted) return;

        Color32 orange = new Color32(255, 115, 15, 255);
        int flagHeight = 9 * glyphScale;
        int flagWidth = 52 * glyphScale;
        DrawFilledRectangle(texture, x, y - glyphScale, flagWidth, flagHeight, orange);
        DrawPixelText(texture, x + 2 * glyphScale, y, "INVERTED", glyphScale, new Color32(0, 0, 0, 255));
    }

    private static void DrawResourceMarker(Texture2D texture, int centerX, int centerY, ResourceNodeType type)
    {
        Color32 color = ResourceColor(type);
        int radius = Mathf.Max(4, texture.width / 160);
        DrawFilledRectangle(texture, centerX - radius - 1, centerY - radius - 1, radius * 2 + 3, radius * 2 + 3, new Color32(0, 0, 0, 255));
        DrawFilledRectangle(texture, centerX - radius, centerY - radius, radius * 2 + 1, radius * 2 + 1, color);
        DrawPixelText(texture, centerX - radius + 1, centerY - 3, ResourceCode(type), 1, new Color32(0, 0, 0, 255));
    }

    private static Color32 ResourceColor(ResourceNodeType type)
    {
        switch (type)
        {
            case ResourceNodeType.Mine: return new Color32(255, 215, 0, 255);
            case ResourceNodeType.ForestGrove: return new Color32(120, 255, 50, 255);
            case ResourceNodeType.RiverBank: return new Color32(255, 145, 30, 255);
            case ResourceNodeType.LakeMouth: return new Color32(40, 220, 255, 255);
            case ResourceNodeType.CoastalFishery: return new Color32(60, 255, 210, 255);
            case ResourceNodeType.OreSeabed: return new Color32(180, 100, 255, 255);
            case ResourceNodeType.HydrothermalVent: return new Color32(255, 70, 220, 255);
            case ResourceNodeType.CrudeOil: return new Color32(35, 25, 15, 255);
            default: return new Color32(180, 180, 180, 255);
        }
    }

    private static string ResourceCode(ResourceNodeType type)
    {
        switch (type)
        {
            case ResourceNodeType.Mine: return "MI";
            case ResourceNodeType.ForestGrove: return "FG";
            case ResourceNodeType.RiverBank: return "RB";
            case ResourceNodeType.LakeMouth: return "LM";
            case ResourceNodeType.CoastalFishery: return "CF";
            case ResourceNodeType.OreSeabed: return "OS";
            case ResourceNodeType.HydrothermalVent: return "HV";
            case ResourceNodeType.CrudeOil: return "CO";
            default: return "";
        }
    }

    private static Texture2D CopyTexture(Texture source, string textureName)
    {
        RenderTexture temporary = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Default);
        RenderTexture previous = RenderTexture.active;

        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;

            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                name = textureName,
                hideFlags = HideFlags.HideAndDontSave,
            };
            copy.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
            copy.Apply(false, false);
            return copy;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
        }
    }

    private static Bounds CalculateRenderableBounds(Island island)
    {
        Renderer[] renderers = island.GetComponentsInChildren<Renderer>(false);
        bool foundRenderer = false;
        Bounds bounds = default;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

            if (!foundRenderer)
            {
                bounds = renderer.bounds;
                foundRenderer = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (foundRenderer) return bounds;
        if (island.bounds.size.sqrMagnitude > 0.001f) return island.bounds;

        return new Bounds(island.transform.position, new Vector3(60f, 10f, 60f));
    }

    private static Texture2D RenderTopView(Camera camera, Bounds bounds, int resolution)
    {
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * FramingPadding;
        float distance = Mathf.Max(10f, bounds.size.y + radius * 2f);

        camera.transform.position = bounds.center + Vector3.up * distance;
        camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        camera.orthographicSize = Mathf.Max(1f, radius);
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = distance + bounds.size.y + 10f;
        return Render(camera, resolution, "Terrain Reference Top");
    }

    private static List<string> CaptureCrudeOilDepositCloseups(
        Island island,
        Camera camera,
        int resolution,
        string outputDirectory)
    {
        Transform[] deposits = island.GetComponentsInChildren<Transform>(false)
            .Where(child => child != null
                && child.name.StartsWith("Crude Oil Deposit (", StringComparison.Ordinal))
            .OrderBy(child => child.name, StringComparer.Ordinal)
            .ToArray();
        var files = new List<string>(deposits.Length);
        int closeupResolution = Mathf.Clamp(resolution, 384, 768);

        for (int index = 0; index < deposits.Length; index++)
        {
            Renderer[] renderers = deposits[index].GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) continue;

            Bounds depositBounds = renderers[0].bounds;
            for (int rendererIndex = 1; rendererIndex < renderers.Length; rendererIndex++)
            {
                if (renderers[rendererIndex] != null && renderers[rendererIndex].enabled)
                {
                    depositBounds.Encapsulate(renderers[rendererIndex].bounds);
                }
            }

            // Keep a fixed amount of surrounding tabletop in frame. Besides making
            // captures comparable across seeds, this exposes hard rectangular edges,
            // incorrect scale, and poor sediment blending immediately.
            float contextDiameter = Mathf.Max(
                8f,
                Mathf.Max(depositBounds.size.x, depositBounds.size.z) * 2.5f);
            var closeupBounds = new Bounds(
                depositBounds.center,
                new Vector3(contextDiameter, Mathf.Max(2f, depositBounds.size.y), contextDiameter));

            Texture2D closeup = null;
            try
            {
                closeup = RenderTopView(camera, closeupBounds, closeupResolution);
                string fileName = $"island-{island.id:D3}_crude-oil-{index + 1:D2}_top-closeup.png";
                File.WriteAllBytes(Path.Combine(outputDirectory, fileName), closeup.EncodeToPNG());
                files.Add(fileName);
            }
            finally
            {
                if (closeup != null) UnityEngine.Object.DestroyImmediate(closeup);
            }
        }

        return files;
    }

    private static Texture2D RenderSideView(Camera camera, Bounds bounds, int resolution)
    {
        float radius = Mathf.Max(bounds.extents.x, Mathf.Max(2f, bounds.extents.y)) * FramingPadding;
        float distance = Mathf.Max(10f, bounds.size.z + radius * 2f);

        camera.transform.position = bounds.center + Vector3.back * distance;
        camera.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        camera.orthographicSize = Mathf.Max(1f, radius);
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = distance + bounds.size.z + 10f;
        return Render(camera, resolution, "Terrain Reference Side");
    }

    private static Texture2D Render(Camera camera, int resolution, string textureName)
    {
        var renderTexture = new RenderTexture(
            resolution,
            resolution,
            24,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Default)
        {
            name = textureName,
            antiAliasing = 4,
            hideFlags = HideFlags.HideAndDontSave,
        };

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;

        try
        {
            renderTexture.Create();
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            var result = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = textureName,
                hideFlags = HideFlags.HideAndDontSave,
            };
            result.ReadPixels(new Rect(0f, 0f, resolution, resolution), 0, 0, false);
            result.Apply(false, false);
            return result;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }
    }

    private static Texture2D BuildComposite(Texture2D top, Texture2D side, int islandNumber, bool selectionInverted)
    {
        int viewSize = top.width;
        var composite = new Texture2D(viewSize * 2, viewSize, TextureFormat.RGBA32, false)
        {
            name = $"Island {islandNumber:D3} Terrain Reference",
            hideFlags = HideFlags.HideAndDontSave,
        };

        composite.SetPixels(0, 0, viewSize, viewSize, top.GetPixels());
        composite.SetPixels(viewSize, 0, viewSize, viewSize, side.GetPixels());

        int bannerHeight = Mathf.Max(30, viewSize / 14);
        DrawFilledRectangle(composite, 0, viewSize - bannerHeight, viewSize, bannerHeight, new Color32(0, 0, 0, 230));
        DrawFilledRectangle(composite, viewSize, viewSize - bannerHeight, viewSize, bannerHeight, new Color32(0, 0, 0, 230));
        DrawFilledRectangle(composite, viewSize - 1, 0, 2, viewSize, new Color32(255, 255, 255, 255));

        int glyphScale = Mathf.Max(2, viewSize / 170);
        int textY = viewSize - bannerHeight + (bannerHeight - 7 * glyphScale) / 2;
        DrawPixelText(composite, 12, textY, $"ISLAND {islandNumber:D3} - TOP", glyphScale, new Color32(255, 255, 255, 255));
        DrawPixelText(composite, viewSize + 12, textY, $"ISLAND {islandNumber:D3} - SIDE", glyphScale, new Color32(255, 255, 255, 255));
        DrawInvertedFlag(composite, selectionInverted, viewSize - 196, textY, glyphScale);
        DrawInvertedFlag(composite, selectionInverted, viewSize * 2 - 196, textY, glyphScale);

        composite.Apply(false, false);
        return composite;
    }

    private static void DrawFilledRectangle(Texture2D texture, int x, int y, int width, int height, Color32 color)
    {
        int minX = Mathf.Clamp(x, 0, texture.width);
        int maxX = Mathf.Clamp(x + width, 0, texture.width);
        int minY = Mathf.Clamp(y, 0, texture.height);
        int maxY = Mathf.Clamp(y + height, 0, texture.height);

        for (int py = minY; py < maxY; py++)
        {
            for (int px = minX; px < maxX; px++)
            {
                texture.SetPixel(px, py, color);
            }
        }
    }

    private static void BlendFilledRectangle(Texture2D texture, int x, int y, int width, int height, Color32 overlay)
    {
        int minX = Mathf.Clamp(x, 0, texture.width);
        int maxX = Mathf.Clamp(x + width, 0, texture.width);
        int minY = Mathf.Clamp(y, 0, texture.height);
        int maxY = Mathf.Clamp(y + height, 0, texture.height);
        float alpha = overlay.a / 255f;

        for (int py = minY; py < maxY; py++)
        {
            for (int px = minX; px < maxX; px++)
            {
                Color32 source = texture.GetPixel(px, py);
                texture.SetPixel(
                    px,
                    py,
                    new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Lerp(source.r, overlay.r, alpha)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(source.g, overlay.g, alpha)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(source.b, overlay.b, alpha)),
                        255));
            }
        }
    }

    private static void DrawRectangleOutline(
        Texture2D texture,
        int x,
        int y,
        int width,
        int height,
        int thickness,
        Color32 color,
        bool blend)
    {
        Action<Texture2D, int, int, int, int, Color32> draw = blend
            ? BlendFilledRectangle
            : DrawFilledRectangle;
        draw(texture, x, y, width, thickness, color);
        draw(texture, x, y + height - thickness, width, thickness, color);
        draw(texture, x, y, thickness, height, color);
        draw(texture, x + width - thickness, y, thickness, height, color);
    }

    private static void DrawPixelText(Texture2D texture, int x, int y, string text, int scale, Color32 color)
    {
        int cursorX = x;
        foreach (char character in text.ToUpperInvariant())
        {
            string[] glyph = GetGlyph(character);
            for (int row = 0; row < glyph.Length; row++)
            {
                for (int column = 0; column < glyph[row].Length; column++)
                {
                    if (glyph[row][column] != '1') continue;

                    int pixelX = cursorX + column * scale;
                    int pixelY = y + (6 - row) * scale;
                    DrawFilledRectangle(texture, pixelX, pixelY, scale, scale, color);
                }
            }

            cursorX += 6 * scale;
        }
    }

    private static string[] GetGlyph(char character)
    {
        switch (character)
        {
            case '0': return new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" };
            case '1': return new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" };
            case '2': return new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" };
            case '3': return new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" };
            case '4': return new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" };
            case '5': return new[] { "11111", "10000", "10000", "11110", "00001", "00001", "11110" };
            case '6': return new[] { "01110", "10000", "10000", "11110", "10001", "10001", "01110" };
            case '7': return new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" };
            case '8': return new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" };
            case '9': return new[] { "01110", "10001", "10001", "01111", "00001", "00001", "01110" };
            case 'A': return new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" };
            case 'B': return new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" };
            case 'C': return new[] { "01111", "10000", "10000", "10000", "10000", "10000", "01111" };
            case 'D': return new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" };
            case 'E': return new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" };
            case 'F': return new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" };
            case 'G': return new[] { "01111", "10000", "10000", "10111", "10001", "10001", "01111" };
            case 'H': return new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" };
            case 'I': return new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" };
            case 'J': return new[] { "00111", "00010", "00010", "00010", "10010", "10010", "01100" };
            case 'K': return new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" };
            case 'L': return new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" };
            case 'M': return new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" };
            case 'N': return new[] { "10001", "11001", "11001", "10101", "10011", "10011", "10001" };
            case 'O': return new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" };
            case 'P': return new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" };
            case 'Q': return new[] { "01110", "10001", "10001", "10001", "10101", "10010", "01101" };
            case 'R': return new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" };
            case 'S': return new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" };
            case 'T': return new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" };
            case 'U': return new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" };
            case 'V': return new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" };
            case 'W': return new[] { "10001", "10001", "10001", "10101", "10101", "10101", "01010" };
            case 'X': return new[] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" };
            case 'Y': return new[] { "10001", "10001", "01010", "00100", "00100", "00100", "00100" };
            case 'Z': return new[] { "11111", "00001", "00010", "00100", "01000", "10000", "11111" };
            case '-': return new[] { "00000", "00000", "00000", "11111", "00000", "00000", "00000" };
            default: return new[] { "00000", "00000", "00000", "00000", "00000", "00000", "00000" };
        }
    }

    private static List<LayerState> MoveHierarchyToCaptureLayer(Transform root)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        var states = new List<LayerState>(transforms.Length);
        foreach (Transform child in transforms)
        {
            states.Add(new LayerState(child.gameObject, child.gameObject.layer));
            child.gameObject.layer = CaptureLayer;
        }

        return states;
    }

    private static void RestoreLayers(List<LayerState> states)
    {
        foreach (LayerState state in states)
        {
            if (state.gameObject != null) state.gameObject.layer = state.layer;
        }
    }

    private static string FormatVector(Vector3 value)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "({0:F3}, {1:F3}, {2:F3})",
            value.x,
            value.y,
            value.z);
    }

    private static string BuildMarkdownIndex(CaptureManifest manifest)
    {
        var markdown = new StringBuilder();
        markdown.AppendLine(manifest.isPartialFailure
            ? "# Latest Failed Terrain Generation Reference"
            : "# Latest Successful Terrain Generation Reference");
        markdown.AppendLine();
        markdown.AppendLine(
            manifest.isPartialFailure
                ? "**Future AI:** this is a partial pre-cleanup snapshot. Diagnose the failure using the chunk/phase, timings, and the last island that has an image. Compare performance against `../Latest/manifest.json`; do not treat this failed run as the successful baseline."
                : "**Future AI:** use this directory as the last successful visual/performance baseline. Compare a failure against `../LatestFailed/manifest.json` without replacing this baseline.");
        markdown.AppendLine();
        markdown.AppendLine($"Generated: `{manifest.generatedAtUtc}`  ");
        markdown.AppendLine($"Scene: `{manifest.scene}`  ");
        markdown.AppendLine($"Layout: `{manifest.viewLayout}`");
        markdown.AppendLine($"Generation status: `{manifest.generationStatus}`  ");
        markdown.AppendLine($"Selection inverted: `{manifest.selectionInverted}`  ");
        if (manifest.isPartialFailure)
        {
            markdown.AppendLine($"Failure checkpoint: chunk `{manifest.failureChunk}`, phase `{manifest.failurePhase}`  ");
            markdown.AppendLine("Captured island count can be lower than the configured map because cleanup had not yet run and generation stopped mid-map.  ");
        }
        markdown.AppendLine($"Total generation time: `{manifest.totalGenerationMs} ms`  ");
        if (manifest.hasPreviousGeneration)
        {
            string sign = manifest.generationDeltaMs >= 0L ? "+" : string.Empty;
            markdown.AppendLine(
                $"Previous generation: `{manifest.previousTotalGenerationMs} ms`; " +
                $"deviation: `{sign}{manifest.generationDeltaMs} ms ({sign}{manifest.generationDeltaPercent:F1}%)`  ");
        }
        else
        {
            markdown.AppendLine("Previous generation: `not available (first indexed capture)`  ");
        }
        markdown.AppendLine($"Island count: `{manifest.islandCount}`; maximum visual samples/cell: `{manifest.maximumVisualSamplesPerCell}`");
        markdown.AppendLine();

        markdown.AppendLine("## Per-island generation timings");
        markdown.AppendLine();
        markdown.AppendLine("| Island | Total | Reservations | Sampling | Gameplay/Metrics | Mesh | Mesh Upload | Splat | Texture Upload | Foliage |");
        markdown.AppendLine("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (CaptureEntry entry in manifest.islands)
        {
            TerrainGenerationProfile timing = entry.timing;
            if (timing == null)
            {
                markdown.AppendLine($"| {entry.islandNumber:D3} | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a | n/a |");
                continue;
            }

            markdown.AppendLine(
                $"| {entry.islandNumber:D3} | {timing.totalMs} ms | {timing.featureReservationsMs} ms | " +
                $"{timing.samplingCacheMs} ms | {timing.gameplayGridAndMetricsMs} ms | {timing.meshBuildMs} ms | " +
                $"{timing.meshUploadMs} ms | {timing.textureSplatMs} ms | {timing.textureUploadMs} ms | {timing.foliageMs} ms |");
        }
        markdown.AppendLine();

        foreach (CaptureEntry entry in manifest.islands)
        {
            markdown.AppendLine($"## Island {entry.islandNumber:D3} — {entry.islandName}");
            markdown.AppendLine();
            markdown.AppendLine($"![Island {entry.islandNumber:D3} top and side](./{entry.file})");
            markdown.AppendLine();
            if (!string.IsNullOrEmpty(entry.controlTextureFile))
            {
                markdown.AppendLine($"Raw splat/control texture: ![Island {entry.islandNumber:D3} splat control](./{entry.controlTextureFile})");
                markdown.AppendLine();
            }
            if (!string.IsNullOrEmpty(entry.gameplayDebugFile))
            {
                markdown.AppendLine($"Gameplay placement grid and resource nodes: ![Island {entry.islandNumber:D3} gameplay grid](./{entry.gameplayDebugFile})");
                markdown.AppendLine();
            }
            if (entry.crudeOilDepositFiles != null && entry.crudeOilDepositFiles.Count > 0)
            {
                markdown.AppendLine($"Crude-oil deposit close-ups ({entry.crudeOilDepositFiles.Count}):");
                markdown.AppendLine();
                for (int depositIndex = 0; depositIndex < entry.crudeOilDepositFiles.Count; depositIndex++)
                {
                    markdown.AppendLine(
                        $"![Island {entry.islandNumber:D3} crude-oil deposit {depositIndex + 1:D2}](./{entry.crudeOilDepositFiles[depositIndex]})");
                    markdown.AppendLine();
                }
            }
            markdown.AppendLine($"Bounds center: `{entry.boundsCenter}`; size: `{entry.boundsSize}`");
            markdown.AppendLine();
        }

        return markdown.ToString();
    }

    private readonly struct LayerState
    {
        public LayerState(GameObject gameObject, int layer)
        {
            this.gameObject = gameObject;
            this.layer = layer;
        }

        public readonly GameObject gameObject;
        public readonly int layer;
    }

    [Serializable]
    private sealed class CaptureManifest
    {
        public string generatedAtUtc;
        public string scene;
        public int resolutionPerView;
        public string viewLayout;
        public string generationStatus;
        public bool isPartialFailure;
        public string failureChunk;
        public string failurePhase;
        public long totalGenerationMs;
        public bool hasPreviousGeneration;
        public long previousTotalGenerationMs;
        public long generationDeltaMs;
        public float generationDeltaPercent;
        public int islandCount;
        public int maximumVisualSamplesPerCell;
        public bool selectionInverted;
        public List<CaptureEntry> islands = new List<CaptureEntry>();
    }

    [Serializable]
    private sealed class CaptureEntry
    {
        public int islandNumber;
        public string islandName;
        public string file;
        public string controlTextureFile;
        public string gameplayDebugFile;
        public List<string> crudeOilDepositFiles = new List<string>();
        public string boundsCenter;
        public string boundsSize;
        public TerrainGenerationProfile timing;
    }
}

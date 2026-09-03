using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates procedural sprites (CogWheel, ProgressRing, Slots, Action Icons)
/// and configures sprite import settings for Anno 2070 building GUI panels.
/// Menu: Tools > Moonlight > Generate Building Facility Assets
/// </summary>
public static class BuildingFacilityAssetGenerator
{
    private const string SpriteFolder = "Assets/Prefabs/UI/Sprites";

    [MenuItem("Tools/Moonlight/Generate Building Facility Assets")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(SpriteFolder);

        GenerateCogWheel();
        GenerateProgressRing();
        GenerateSlotBackground();
        GenerateHomeIcon();
        GeneratePickaxeIcon();
        GenerateCycleIcon();

        ConfigureExistingTextures();

        AssetDatabase.Refresh();
        Debug.Log("Building facility UI assets generated and configured successfully!");
    }

    private static void GenerateCogWheel()
    {
        int size = 512;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float outerRadius = size * 0.46f;
        float rootRadius = size * 0.36f;
        float innerRimRadius = size * 0.31f;
        float hubRadius = size * 0.14f;
        float holeRadius = size * 0.06f;
        int numTeeth = 12;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pos = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 diff = pos - center;
                float dist = diff.magnitude;
                float angle = Mathf.Atan2(diff.y, diff.x);
                if (angle < 0) angle += Mathf.PI * 2f;

                // Tooth angle fraction
                float toothAngleStep = (Mathf.PI * 2f) / numTeeth;
                float modAngle = Mathf.Repeat(angle, toothAngleStep);
                float toothFraction = modAngle / toothAngleStep; // 0 to 1

                // Tooth profile: trapezoid
                float toothWidth = 0.5f; // tooth takes 50% of the slot
                bool inTooth = toothFraction >= (1f - toothWidth) * 0.5f && toothFraction <= (1f + toothWidth) * 0.5f;

                float maxR = inTooth ? outerRadius : rootRadius;

                float alpha = 0f;

                if (dist <= maxR && dist >= innerRimRadius)
                {
                    // Gear rim and teeth
                    alpha = Mathf.Clamp01(maxR - dist);
                    if (dist - innerRimRadius < 1f) alpha = Mathf.Min(alpha, Mathf.Clamp01(dist - innerRimRadius));
                }
                else if (dist <= hubRadius && dist >= holeRadius)
                {
                    // Center hub
                    alpha = Mathf.Clamp01(hubRadius - dist);
                    if (dist - holeRadius < 1f) alpha = Mathf.Min(alpha, Mathf.Clamp01(dist - holeRadius));
                }
                else if (dist < innerRimRadius && dist > hubRadius)
                {
                    // Spokes (4 spokes)
                    int numSpokes = 4;
                    float spokeAngleStep = (Mathf.PI * 2f) / numSpokes;
                    float spokeMod = Mathf.Repeat(angle, spokeAngleStep);
                    float spokeDistFromCenter = Mathf.Abs(spokeMod - spokeAngleStep * 0.5f);
                    float spokeWidthAngle = 0.18f;

                    if (spokeDistFromCenter < spokeWidthAngle)
                    {
                        alpha = Mathf.Clamp01((spokeWidthAngle - spokeDistFromCenter) * 20f);
                    }
                }

                colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(colors);
        tex.Apply();

        string path = $"{SpriteFolder}/CogWheel.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        SetTextureAsSprite(path);
    }

    private static void GenerateProgressRing()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float outerR = size * 0.46f;
        float innerR = size * 0.36f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 diff = new Vector2(x + 0.5f, y + 0.5f) - center;
                float dist = diff.magnitude;

                float alpha = 0f;
                if (dist <= outerR && dist >= innerR)
                {
                    float aOuter = Mathf.Clamp01(outerR - dist);
                    float aInner = Mathf.Clamp01(dist - innerR);
                    alpha = Mathf.Min(aOuter, aInner);
                }

                colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(colors);
        tex.Apply();

        string path = $"{SpriteFolder}/ProgressRing.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        SetTextureAsSprite(path);
    }

    private static void GenerateSlotBackground()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];

        float cornerRadius = 14f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x - size * 0.5f) - (size * 0.5f - cornerRadius));
                float dy = Mathf.Max(0, Mathf.Abs(y - size * 0.5f) - (size * 0.5f - cornerRadius));
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.Clamp01(cornerRadius - dist);

                // Subtle border gradient
                float borderDist = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                float shade = Mathf.Lerp(0.85f, 0.65f, (float)y / size);
                if (borderDist < 2) shade = 0.95f;

                colors[y * size + x] = new Color(shade, shade, shade, alpha);
            }
        }

        tex.SetPixels(colors);
        tex.Apply();

        string path = $"{SpriteFolder}/SlotBackground.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        SetTextureAsSprite(path, new Vector4(16, 16, 16, 16));
    }

    private static void GenerateHomeIcon()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = 0f;
                // Roof: triangle
                if (y >= 60 && y <= 110)
                {
                    float roofHalfWidth = (110 - y) * 1.0f;
                    if (Mathf.Abs(x - 64) <= roofHalfWidth) alpha = 1f;
                }
                // Base: rectangle
                if (y >= 18 && y < 60 && x >= 30 && x <= 98)
                {
                    // Door cutout
                    if (!(x >= 52 && x <= 76 && y <= 45))
                    {
                        alpha = 1f;
                    }
                }
                colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(colors);
        tex.Apply();

        string path = $"{SpriteFolder}/HomeIcon.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        SetTextureAsSprite(path);
    }

    private static void GeneratePickaxeIcon()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = 0f;
                // Handle: diagonal line
                float lineDist = Mathf.Abs((x - y) * 0.7071f);
                if (lineDist < 4f && x >= 24 && x <= 96 && y >= 24 && y <= 96)
                {
                    alpha = 1f;
                }
                // Pick head: curved arc at top right
                Vector2 pickCenter = new Vector2(100, 100);
                float pDist = Vector2.Distance(new Vector2(x, y), pickCenter);
                if (pDist >= 35f && pDist <= 46f && x >= 60 && y >= 60)
                {
                    alpha = 1f;
                }

                colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(colors);
        tex.Apply();

        string path = $"{SpriteFolder}/PickaxeIcon.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        SetTextureAsSprite(path);
    }

    private static void GenerateCycleIcon()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];

        Vector2 center = new Vector2(64, 64);
        float rOuter = 46f;
        float rInner = 36f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 diff = new Vector2(x, y) - center;
                float dist = diff.magnitude;
                float alpha = 0f;

                // Two arc segments (left and right)
                float angle = Mathf.Atan2(diff.y, diff.x);
                bool inArc1 = (angle > 0.3f && angle < Mathf.PI - 0.3f);
                bool inArc2 = (angle < -0.3f && angle > -Mathf.PI + 0.3f);

                if (dist >= rInner && dist <= rOuter && (inArc1 || inArc2))
                {
                    alpha = 1f;
                }

                // Arrow heads at tips
                if (Mathf.Abs(x - 24) < 10 && Mathf.Abs(y - 64) < 10 && y <= 64) alpha = 1f;
                if (Mathf.Abs(x - 104) < 10 && Mathf.Abs(y - 64) < 10 && y >= 64) alpha = 1f;

                colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(colors);
        tex.Apply();

        string path = $"{SpriteFolder}/CycleIcon.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        SetTextureAsSprite(path);
    }

    private static void ConfigureExistingTextures()
    {
        string[] paths = new string[]
        {
            "Assets/Imports/Anno2070/Icons/Credits-icon.png",
            "Assets/Imports/Anno2070/Icons/Energy-icon.png",
            "Assets/Imports/Anno2070/Icons/Ecobal-icon.png",
            "Assets/Imports/Anno2070/Icons/Health-icon.png",
            "Assets/Imports/Anno2070/Icons/Diplomacy_icon.png",
            "Assets/Imports/Anno2070/Icons/Upgrade.png",
            "Assets/Imports/Anno2070/WEBP - Item Icons/Ozone-maker-icon.png",
            "Assets/Imports/Anno2070/Energy_and_Ecology/Ozonmaker.png",
            "Assets/Imports/Anno2070/PNG - Item Icons/CrudeOil.png",
            "Assets/Imports/Anno2070/Icons/Needs & Desires/Building Based/Information.png"
        };

        foreach (string p in paths)
        {
            SetTextureAsSprite(p);
        }
    }

    private static void SetTextureAsSprite(string path, Vector4? border = null)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool modified = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                modified = true;
            }
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                modified = true;
            }
            if (border.HasValue)
            {
                importer.spriteBorder = border.Value;
                modified = true;
            }
            if (modified)
            {
                importer.SaveAndReimport();
            }
        }
    }
}

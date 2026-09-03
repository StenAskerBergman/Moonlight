using UnityEngine;

/// <summary>
/// Provides crisp vector-style procedural sprites for the Context Menu's core tools
/// (Demolish Pickaxe, Build Menu House, Pipette Eyedropper) and default shortcuts.
/// </summary>
public static class ContextMenuIcons
{
    private static Sprite pickaxeSprite;
    private static Sprite houseSilhouetteSprite;
    private static Sprite pipetteSprite;
    private static Sprite houseBuildingSprite;
    private static Sprite roadSprite;
    private static Sprite warehouseSprite;
    private static Sprite dirtRoadSprite;
    private static Sprite slotFrameSprite;

    public static Sprite Pickaxe => pickaxeSprite ??= CreatePickaxeSprite();
    public static Sprite HouseSilhouette => houseSilhouetteSprite ??= CreateHouseSilhouetteSprite();
    public static Sprite Pipette => pipetteSprite ??= CreatePipetteSprite();
    public static Sprite HouseBuilding => houseBuildingSprite ??= CreateHouseBuildingSprite();
    public static Sprite Road => roadSprite ??= CreateRoadSprite();
    public static Sprite Warehouse => warehouseSprite ??= CreateWarehouseSprite();
    public static Sprite DirtRoad => dirtRoadSprite ??= CreateDirtRoadSprite();
    public static Sprite SlotFrame => slotFrameSprite ??= CreateSlotFrameSprite();

    private static Sprite CreateSpriteFromTexture(Texture2D tex)
    {
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>
    /// Pickaxe icon (diagonal handle with pickaxe head) in cyan/white.
    /// </summary>
    private static Sprite CreatePickaxeSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0, 0, 0, 0);
        Color cyanWhite = new Color(0.75f, 0.95f, 1f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, clear);
            }
        }

        // Draw diagonal handle: from (24, 24) to (76, 76)
        DrawThickLine(tex, 24, 24, 76, 76, 7, cyanWhite);

        // Draw pickaxe curved head: centered near (82, 82), curving from (40, 95) to (95, 40)
        for (float t = 0f; t <= 1f; t += 0.005f)
        {
            // Quadratic Bezier arc
            float p0x = 34f, p0y = 100f;
            float p1x = 88f, p1y = 88f;
            float p2x = 100f, p2y = 34f;

            float u = 1f - t;
            float px = u * u * p0x + 2f * u * t * p1x + t * t * p2x;
            float py = u * u * p0y + 2f * u * t * p1y + t * t * p2y;

            DrawCircle(tex, Mathf.RoundToInt(px), Mathf.RoundToInt(py), 6, cyanWhite);
        }

        // Head tip highlights
        DrawCircle(tex, 34, 100, 3, cyanWhite);
        DrawCircle(tex, 100, 34, 3, cyanWhite);

        tex.Apply();
        return CreateSpriteFromTexture(tex);
    }

    /// <summary>
    /// Simple solid house silhouette (triangular roof + rectangular base + small door cutout).
    /// </summary>
    private static Sprite CreateHouseSilhouetteSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0, 0, 0, 0);
        Color cyanWhite = new Color(0.75f, 0.95f, 1f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Centered house geometry
                // Roof: triangle from y=62 to y=104, x around center 64
                // Base: rectangle from y=24 to y=62, x between 36 and 92
                // Door: cutout from y=24 to y=46, x between 56 and 72
                bool inRoof = y >= 62 && y <= 104 && Mathf.Abs(x - 64) <= (104 - y) * 1.3f;
                bool inBase = y >= 24 && y <= 64 && x >= 36 && x <= 92;
                bool inDoor = y >= 24 && y <= 48 && x >= 56 && x <= 72;

                if ((inRoof || inBase) && !inDoor)
                {
                    tex.SetPixel(x, y, cyanWhite);
                }
                else
                {
                    tex.SetPixel(x, y, clear);
                }
            }
        }

        tex.Apply();
        return CreateSpriteFromTexture(tex);
    }

    /// <summary>
    /// Pipette / Eyedropper tool icon angled diagonally.
    /// </summary>
    private static Sprite CreatePipetteSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0, 0, 0, 0);
        Color cyanWhite = new Color(0.75f, 0.95f, 1f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, clear);
            }
        }

        // Draw bulb at top right (90, 90)
        DrawCircle(tex, 88, 88, 14, cyanWhite);
        // Draw collar
        DrawThickLine(tex, 82, 70, 70, 82, 6, cyanWhite);
        // Draw tube from (74, 74) to (44, 44)
        DrawThickLine(tex, 74, 74, 44, 44, 10, cyanWhite);
        // Draw tapered tip to (26, 26)
        DrawThickLine(tex, 44, 44, 26, 26, 4, cyanWhite);
        // Droplet at (20, 20)
        DrawCircle(tex, 20, 20, 3, cyanWhite);

        tex.Apply();
        return CreateSpriteFromTexture(tex);
    }

    /// <summary>
    /// 3D stylized green roof house icon.
    /// </summary>
    private static Sprite CreateHouseBuildingSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0, 0, 0, 0);
        Color roofGreen = new Color(0.35f, 0.65f, 0.35f, 1f);
        Color roofShade = new Color(0.24f, 0.50f, 0.24f, 1f);
        Color wallColor = new Color(0.85f, 0.85f, 0.88f, 1f);
        Color wallShade = new Color(0.65f, 0.67f, 0.72f, 1f);
        Color chimney = new Color(0.78f, 0.78f, 0.80f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, clear);
            }
        }

        // Chimney
        for (int y = 80; y <= 104; y++)
            for (int x = 40; x <= 50; x++)
                tex.SetPixel(x, y, chimney);

        // Walls
        for (int y = 24; y <= 66; y++)
        {
            for (int x = 28; x <= 100; x++)
            {
                tex.SetPixel(x, y, x > 64 ? wallShade : wallColor);
            }
        }

        // Roof
        for (int y = 62; y <= 98; y++)
        {
            float halfWidth = (98 - y) * 1.25f + 8f;
            int minX = Mathf.RoundToInt(64 - halfWidth);
            int maxX = Mathf.RoundToInt(64 + halfWidth);
            for (int x = minX; x <= maxX; x++)
            {
                if (x >= 0 && x < size)
                    tex.SetPixel(x, y, x > 64 ? roofShade : roofGreen);
            }
        }

        // Windows & Door
        for (int y = 28; y <= 50; y++)
            for (int x = 40; x <= 52; x++)
                tex.SetPixel(x, y, new Color(0.25f, 0.35f, 0.45f, 1f));

        for (int y = 36; y <= 52; y++)
            for (int x = 74; x <= 90; x++)
                tex.SetPixel(x, y, new Color(0.25f, 0.35f, 0.45f, 1f));

        tex.Apply();
        return CreateSpriteFromTexture(tex);
    }

    /// <summary>
    /// Paved street / stone grid road tile icon.
    /// </summary>
    private static Sprite CreateRoadSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color border = new Color(0.22f, 0.28f, 0.32f, 1f);
        Color stoneLight = new Color(0.65f, 0.68f, 0.70f, 1f);
        Color stoneDark = new Color(0.48f, 0.52f, 0.55f, 1f);
        Color mortar = new Color(0.30f, 0.33f, 0.35f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (x < 12 || x > 115 || y < 12 || y > 115)
                {
                    tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                    continue;
                }

                if (x == 12 || x == 115 || y == 12 || y == 115)
                {
                    tex.SetPixel(x, y, border);
                    continue;
                }

                bool isGrid = (x % 26 <= 2) || (y % 26 <= 2);
                if (isGrid)
                {
                    tex.SetPixel(x, y, mortar);
                }
                else
                {
                    bool alt = ((x / 26) + (y / 26)) % 2 == 0;
                    tex.SetPixel(x, y, alt ? stoneLight : stoneDark);
                }
            }
        }

        tex.Apply();
        return CreateSpriteFromTexture(tex);
    }

    /// <summary>
    /// Warehouse / wooden pallet with stacked brown crates.
    /// </summary>
    private static Sprite CreateWarehouseSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0, 0, 0, 0);
        Color pallet = new Color(0.65f, 0.60f, 0.45f, 1f);
        Color crateA = new Color(0.72f, 0.48f, 0.36f, 1f);
        Color crateB = new Color(0.58f, 0.38f, 0.28f, 1f);
        Color strap = new Color(0.40f, 0.25f, 0.18f, 1f);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, clear);

        // Pallet base
        for (int y = 20; y <= 32; y++)
            for (int x = 20; x <= 108; x++)
                tex.SetPixel(x, y, pallet);

        // Bottom Left Crate
        for (int y = 33; y <= 72; y++)
            for (int x = 24; x <= 62; x++)
                tex.SetPixel(x, y, (x == 24 || x == 62 || y == 33 || y == 72) ? strap : crateA);

        // Bottom Right Crate
        for (int y = 33; y <= 72; y++)
            for (int x = 66; x <= 104; x++)
                tex.SetPixel(x, y, (x == 66 || x == 104 || y == 33 || y == 72) ? strap : crateB);

        // Top Stacked Crate
        for (int y = 73; y <= 106; y++)
            for (int x = 36; x <= 88; x++)
                tex.SetPixel(x, y, (x == 36 || x == 88 || y == 73 || y == 106) ? strap : crateA);

        tex.Apply();
        return CreateSpriteFromTexture(tex);
    }

    /// <summary>
    /// Dirt road / earth path icon.
    /// </summary>
    private static Sprite CreateDirtRoadSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color dirt = new Color(0.48f, 0.40f, 0.30f, 1f);
        Color dirtDark = new Color(0.38f, 0.32f, 0.24f, 1f);
        Color rim = new Color(0.30f, 0.26f, 0.20f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (x < 14 || x > 113 || y < 14 || y > 113)
                {
                    tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                    continue;
                }

                if (x == 14 || x == 113 || y == 14 || y == 113)
                {
                    tex.SetPixel(x, y, rim);
                    continue;
                }

                // Smooth organic pattern
                float n = Mathf.PerlinNoise(x * 0.1f, y * 0.1f);
                tex.SetPixel(x, y, Color.Lerp(dirt, dirtDark, n));
            }
        }

        tex.Apply();
        return CreateSpriteFromTexture(tex);
    }

    /// <summary>
    /// Rounded slot frame with subtle beveled border.
    /// </summary>
    private static Sprite CreateSlotFrameSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0, 0, 0, 0);
        Color fill = new Color(0.12f, 0.16f, 0.20f, 0.85f);
        Color border = new Color(0.30f, 0.40f, 0.48f, 0.80f);

        float radius = 8f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x - 31.5f) - (32f - radius));
                float dy = Mathf.Max(0, Mathf.Abs(y - 31.5f) - (32f - radius));
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > radius)
                {
                    tex.SetPixel(x, y, clear);
                }
                else if (dist > radius - 2f)
                {
                    tex.SetPixel(x, y, border);
                }
                else
                {
                    tex.SetPixel(x, y, fill);
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(10, 10, 10, 10));
    }

    private static void DrawCircle(Texture2D tex, int cx, int cy, int r, Color color)
    {
        int r2 = r * r;
        for (int dy = -r; dy <= r; dy++)
        {
            int py = cy + dy;
            if (py < 0 || py >= tex.height) continue;
            for (int dx = -r; dx <= r; dx++)
            {
                int px = cx + dx;
                if (px < 0 || px >= tex.width) continue;
                if (dx * dx + dy * dy <= r2)
                {
                    tex.SetPixel(px, py, color);
                }
            }
        }
    }

    private static void DrawThickLine(Texture2D tex, int x0, int y0, int x1, int y1, int thickness, Color color)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        int halfThick = thickness / 2;

        while (true)
        {
            DrawCircle(tex, x0, y0, halfThick, color);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }
}

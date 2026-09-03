using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Resource caching service providing instant access to authentic Anno 2070 sprites:
/// vessel portraits, stat icons (upkeep, firepower, health), stance buttons, and abilities.
/// </summary>
public static class ShipUIResourceCache
{
    private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    private static Sprite defaultSlotBg;
    private static Sprite vehicleBadge;
    private static Sprite balanceIcon;
    private static Sprite attackPowerIcon;
    private static Sprite healthIcon;
    private static Sprite shieldIcon;
    private static Sprite shipMoveIcon;
    private static Sprite cycleIcon;
    private static Sprite fleetIcon;

    public static Sprite SlotBackground => ResolveIcon(ref defaultSlotBg, "Assets/Prefabs/UI/Sprites/SlotBackground.png");
    public static Sprite VehicleBadge => ResolveIcon(ref vehicleBadge, "Assets/Imports/Anno2070/Icons/Vehicle-slot-icon.png");
    public static Sprite BalanceIcon => ResolveIcon(ref balanceIcon, "Assets/Imports/Anno2070/Icons/Balance-icon.png");
    public static Sprite AttackPowerIcon => ResolveIcon(ref attackPowerIcon, "Assets/Imports/Anno2070/Icons/Attack-power-icon.png");
    public static Sprite HealthIcon => ResolveIcon(ref healthIcon, "Assets/Imports/Anno2070/Icons/Health-icon.png");
    public static Sprite ShieldIcon => ResolveIcon(ref shieldIcon, "Assets/Imports/Anno2070/Icons/Shield-icon.png");
    public static Sprite ShipMoveIcon => ResolveIcon(ref shipMoveIcon, "Assets/Imports/Anno2070/Icons/Ship-move.png");
    public static Sprite CycleIcon => ResolveIcon(ref cycleIcon, "Assets/Imports/Anno2070/Icons/CycleIcon.png", "Assets/Prefabs/UI/Sprites/CycleIcon.png");
    public static Sprite FleetIcon => ResolveIcon(ref fleetIcon, "Assets/Imports/Anno2070/Icons/Ship-multi.png", "Assets/Imports/Anno2070/Icons/Ship-icon.png");

    public static Sprite GetVesselPortrait(string vesselName)
    {
        if (string.IsNullOrEmpty(vesselName)) return null;

        string clean = vesselName.ToLowerInvariant().Trim();

        string path = null;
        if (clean.Contains("colossus")) path = "Assets/Imports/Anno2070/WEBP - Item Icons/vessels/Colossus-icon.png";
        else if (clean.Contains("viper")) path = "Assets/Imports/Anno2070/WEBP - Item Icons/vessels/Viper-ship-icon.png";
        else if (clean.Contains("hovercraft")) path = "Assets/Imports/Anno2070/WEBP - Item Icons/vessels/Hovercraft-icon.png";
        else if (clean.Contains("commando")) path = "Assets/Imports/Anno2070/WEBP - Item Icons/vessels/Commando-ship-icon.png";
        else if (clean.Contains("shark")) path = "Assets/Imports/Anno2070/WEBP - Item Icons/vessels/Shark-icon.png";
        else if (clean.Contains("raider")) path = "Assets/Imports/Anno2070/WEBP - Item Icons/vessels/Raider-ship-icon.png";
        else if (clean.Contains("atlas")) path = "Assets/Imports/Anno2070/WEBP - Item Icons/vessels/Atlas_Icon.png";
        else if (clean.Contains("freight")) path = "Assets/Imports/Anno2070/WEBP - Item Icons/vessels/Freight-ship-icon.png";
        else if (clean.Contains("cargo")) path = "Assets/Imports/Anno2070/WEBP - Item Icons/vessels/Cargo-liner-icon.png";
        else if (clean.Contains("container")) path = "Assets/Imports/Anno2070/WEBP - Item Icons/vessels/Container-ship-icon.png";
        else if (clean.Contains("oil") || clean.Contains("tanker")) path = "Assets/Imports/Anno2070/WEBP - Item Icons/vessels/Oiltanker_Icon.png";
        else if (clean.Contains("ocean") || clean.Contains("glider") || clean.Contains("t38")) path = "Assets/Imports/Anno2070/WEBP - Item Icons/vessels/sub/Trimaran-ocean-glider-icon.png";
        else if (clean.Contains("sisyphus")) path = "Assets/Imports/Anno2070/WEBP - Item Icons/vessels/sub/Sisyphus_Icon.png";
        else if (clean.Contains("hunter")) path = "Assets/Imports/Anno2070/WEBP - Item Icons/vessels/sub/Deep-sea-hunter.png";
        else if (clean.Contains("orca")) path = "Assets/Imports/Anno2070/WEBP - Item Icons/vessels/sub/Orca_icon.png";
        else if (clean.Contains("erebos")) path = "Assets/Imports/Anno2070/WEBP - Item Icons/vessels/sub/Erebos_icon.png";

        if (path != null)
        {
            Sprite s = LoadSprite(path);
            if (s != null) return s;
        }

        return null;
    }

    public static Sprite LoadSprite(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return null;

        if (spriteCache.TryGetValue(assetPath, out Sprite cached) && cached != null)
        {
            return cached;
        }

#if UNITY_EDITOR
        Sprite loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (loaded != null)
        {
            spriteCache[assetPath] = loaded;
            return loaded;
        }
#endif

        if (File.Exists(assetPath))
        {
            byte[] bytes = File.ReadAllBytes(assetPath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(bytes))
            {
                Sprite s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                s.name = Path.GetFileNameWithoutExtension(assetPath);
                spriteCache[assetPath] = s;
                return s;
            }
        }

        return null;
    }

    private static Sprite ResolveIcon(ref Sprite cachedRef, params string[] paths)
    {
        if (cachedRef != null) return cachedRef;

        foreach (string path in paths)
        {
            if (string.IsNullOrEmpty(path)) continue;
            Sprite s = LoadSprite(path);
            if (s != null)
            {
                cachedRef = s;
                return s;
            }
        }

        return null;
    }
}

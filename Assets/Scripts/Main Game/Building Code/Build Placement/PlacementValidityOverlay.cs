using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thin compatibility entry point for terrain/coastal placement and influence visualization.
/// Delegates GPU mask updates to <see cref="PlacementOverlayMaskUpdater"/> so terrain and
/// water render stepped Anno-style borders, territory dimming, footprint curbs, and road
/// connector highlights in one pass without CompareFunction.Always line meshes.
/// </summary>
[DisallowMultipleComponent]
public class PlacementValidityOverlay : MonoBehaviour
{
    private GridSystem gridSystem;
    private static readonly List<PlacementValidityOverlay> ActiveOverlays = new List<PlacementValidityOverlay>();

    public static void Show(
        GridSystem gridSystem,
        Island island,
        BuildingProperties properties,
        Unit foundingBoat,
        Vector3? previewWorldPos = null,
        PlacementValidationResult validation = null,
        Quaternion? previewRotation = null)
    {
        HideOthers(gridSystem);
        if (gridSystem == null || properties == null) return;

        PlacementValidityOverlay overlay = GetOrCreate(gridSystem);
        PlacementOverlayMaskUpdater updater = PlacementOverlayMaskUpdater.GetOrCreate(gridSystem);
        updater.ApplyPlacement(island, properties, foundingBoat, previewWorldPos, validation, previewRotation);
    }

    public static void ShowStaticZone(GridSystem gridSystem, Island island, InfluenceZone zone)
    {
        HideOthers(gridSystem);
        if (gridSystem == null || zone == null) return;

        PlacementValidityOverlay overlay = GetOrCreate(gridSystem);
        PlacementOverlayMaskUpdater updater = PlacementOverlayMaskUpdater.GetOrCreate(gridSystem);
        updater.ApplyStaticZone(island, zone);
    }

    public static void HideAll()
    {
        for (int i = ActiveOverlays.Count - 1; i >= 0; i--)
        {
            PlacementValidityOverlay overlay = ActiveOverlays[i];
            if (overlay == null)
            {
                ActiveOverlays.RemoveAt(i);
                continue;
            }
            overlay.Hide();
        }
    }

    private static void HideOthers(GridSystem activeGrid)
    {
        for (int i = ActiveOverlays.Count - 1; i >= 0; i--)
        {
            PlacementValidityOverlay overlay = ActiveOverlays[i];
            if (overlay == null)
            {
                ActiveOverlays.RemoveAt(i);
                continue;
            }
            if (overlay.gridSystem != activeGrid)
            {
                overlay.Hide();
            }
        }
    }

    private static PlacementValidityOverlay GetOrCreate(GridSystem gridSystem)
    {
        PlacementValidityOverlay overlay = gridSystem.GetComponent<PlacementValidityOverlay>();
        if (overlay == null)
        {
            overlay = gridSystem.gameObject.AddComponent<PlacementValidityOverlay>();
        }
        overlay.gridSystem = gridSystem;
        return overlay;
    }

    private void OnEnable()
    {
        if (gridSystem == null) gridSystem = GetComponent<GridSystem>();
        if (!ActiveOverlays.Contains(this)) ActiveOverlays.Add(this);
    }

    private void OnDisable()
    {
        ActiveOverlays.Remove(this);
        Hide();
    }

    private void OnDestroy()
    {
        ActiveOverlays.Remove(this);
    }

    public void Hide()
    {
        if (gridSystem == null) gridSystem = GetComponent<GridSystem>();
        PlacementOverlayMaskUpdater updater = gridSystem != null ? gridSystem.GetComponent<PlacementOverlayMaskUpdater>() : null;
        if (updater != null) updater.Clear();
    }
}

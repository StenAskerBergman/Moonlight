using System;
using UnityEngine;

/// <summary>
/// Coordinates mutual exclusion between the different placement/tool modes
/// so only one is active at a time. Each subsystem requests its mode via
/// <see cref="RequestMode"/> and releases it via <see cref="ReleaseMode"/>.
/// Switching away from a mode automatically tells the previous owner to cancel.
/// </summary>
public class ToolModeManager : MonoBehaviour
{
    public enum ToolMode
    {
        None,
        BuildingPlacement,
        RoadPlacement,
        StampCapture,
        StampPlacement
    }

    public static ToolModeManager Instance { get; private set; }

    public ToolMode CurrentMode { get; private set; } = ToolMode.None;

    /// <summary>
    /// Raised whenever the active mode changes.
    /// Listeners receive (previousMode, newMode).
    /// </summary>
    public static event Action<ToolMode, ToolMode> OnToolModeChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Attempts to switch to <paramref name="requested"/>. If another mode is
    /// active it is cancelled first. Returns true if the switch succeeded.
    /// </summary>
    public bool RequestMode(ToolMode requested)
    {
        if (CurrentMode == requested) return true;

        ToolMode previous = CurrentMode;

        // Ask the current owner to tear down.
        CancelCurrentMode();

        CurrentMode = requested;
        OnToolModeChanged?.Invoke(previous, requested);
        return true;
    }

    /// <summary>
    /// The owner of <paramref name="mode"/> calls this when it finishes or is
    /// cancelled. If <paramref name="mode"/> is not the current mode, the call
    /// is ignored.
    /// </summary>
    public void ReleaseMode(ToolMode mode)
    {
        if (CurrentMode != mode) return;

        ToolMode previous = CurrentMode;
        CurrentMode = ToolMode.None;
        OnToolModeChanged?.Invoke(previous, ToolMode.None);
    }

    /// <summary>
    /// Returns true when no tool is active or the active tool is
    /// <paramref name="mode"/> itself.
    /// </summary>
    public bool IsModeAvailable(ToolMode mode)
    {
        return CurrentMode == ToolMode.None || CurrentMode == mode;
    }

    /// <summary>
    /// Force-cancels whatever mode is active and returns to None.
    /// </summary>
    public void CancelAll()
    {
        if (CurrentMode == ToolMode.None) return;
        CancelCurrentMode();

        ToolMode previous = CurrentMode;
        CurrentMode = ToolMode.None;
        OnToolModeChanged?.Invoke(previous, ToolMode.None);
    }

    // --------------- private helpers ---------------

    private void CancelCurrentMode()
    {
        switch (CurrentMode)
        {
            case ToolMode.BuildingPlacement:
                if (BuildingChecker.instance != null)
                    BuildingChecker.instance.CancelBuilding();
                break;

            case ToolMode.RoadPlacement:
                var roadCtrl = FindObjectOfType<RoadPlacementController>();
                if (roadCtrl != null && roadCtrl.RoadModeActive)
                    roadCtrl.ExitRoadMode();
                break;

            case ToolMode.StampCapture:
                var capture = FindObjectOfType<StampCaptureTool>();
                if (capture != null)
                    capture.CancelCapture();
                break;

            case ToolMode.StampPlacement:
                var placement = FindObjectOfType<StampPlacementController>();
                if (placement != null)
                    placement.CancelPlacement();
                break;

            case ToolMode.None:
            default:
                break;
        }
    }
}

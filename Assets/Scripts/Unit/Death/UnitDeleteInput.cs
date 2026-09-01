using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Delete kills the selected units, playing their death sequence rather than removing
/// them instantly - a boat sinks, a land unit topples.
///
/// Self-bootstraps like DemolitionManager so no scene needs editing, and deliberately
/// stays out of demolition mode's way: Delete there would be ambiguous between "kill the
/// selected unit" and "remove the marked buildings".
/// </summary>
[DisallowMultipleComponent]
public sealed class UnitDeleteInput : MonoBehaviour
{
    public static UnitDeleteInput Instance { get; private set; }

    [Tooltip("Key that kills the current unit selection.")]
    [SerializeField] private KeyCode deleteKey = KeyCode.Delete;

    [Tooltip("OFF makes Delete a no-op, for builds where losing a unit to a stray keypress is unacceptable.")]
    [SerializeField] private bool enableDeleteKey = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject host = new GameObject(nameof(UnitDeleteInput));
        host.AddComponent<UnitDeleteInput>();
        DontDestroyOnLoad(host);
    }

    private void Update()
    {
        if (!enableDeleteKey || !Input.GetKeyDown(deleteKey)) return;
        if (DemolitionManager.Instance != null && DemolitionManager.Instance.IsActive) return;

        KillSelection();
    }

    /// <summary>Kills every selected unit. Returns how many death sequences started.</summary>
    public int KillSelection()
    {
        if (UnitSelections.Instance == null || UnitSelections.Instance.unitsSelected == null) return 0;

        // Copy first: the death sequence deselects as it goes, mutating this list.
        List<Unit> doomed = new List<Unit>(UnitSelections.Instance.unitsSelected);

        int killed = 0;
        foreach (Unit unit in doomed)
        {
            if (unit != null && Kill(unit)) killed++;
        }

        return killed;
    }

    /// <summary>
    /// Kills one unit. Routes through <see cref="UnitHealth"/> when the unit has it, so
    /// anything listening for a death hears about this one too; otherwise the death
    /// sequence is started directly.
    /// </summary>
    public static bool Kill(Unit unit)
    {
        if (unit == null) return false;

        // Added on demand - no unit prefab carries these yet.
        UnitDeath death = unit.GetComponent<UnitDeath>() ?? unit.gameObject.AddComponent<UnitDeath>();

        UnitHealth health = unit.GetComponent<UnitHealth>();
        if (health != null)
        {
            if (health.IsDead) return false;
            health.Kill();
            return true;
        }

        death.BeginDeath();
        return true;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows/hides the build and dive action buttons for the currently selected unit
/// based on whether each action is currently possible (BuildInteraction.CanBuild(),
/// DiveInteraction.CanDive()). Polls on selection change and every frame, since both
/// conditions (range, resources, terrain) can change without a new selection event.
/// </summary>
public class UnitActionBarUI : MonoBehaviour
{
    [SerializeField] private Button buildButton;
    [SerializeField] private Button diveButton;
    [SerializeField] private Button deliverButton;

    private BuildInteraction buildInteraction;
    private DiveInteraction diveInteraction;
    private DeliverInteraction deliverInteraction;

    private bool subscribedToSelection;
    private Unit trackedUnit;

    private void Awake()
    {
        ResolveButtonReferences();
    }

    private void ResolveButtonReferences()
    {
        if (buildButton == null)
        {
            Transform t = transform.Find("TMP Build Harbor Button");
            if (t != null) buildButton = t.GetComponent<Button>();
        }

        if (diveButton == null)
        {
            Transform t = transform.Find("TMP Dive Button");
            if (t != null) diveButton = t.GetComponent<Button>();
        }

        if (deliverButton == null)
        {
            Transform t = transform.Find("TMP Deliver Cargo Button");
            if (t != null) deliverButton = t.GetComponent<Button>();
        }
    }

    private void OnEnable()
    {
        ResolveButtonReferences();
        EnsureSubscribedToSelection();

        if (diveButton != null) diveButton.onClick.AddListener(OnDiveButtonClicked);
        if (deliverButton != null) deliverButton.onClick.AddListener(OnDeliverButtonClicked);

        RefreshSelectedComponents();
    }

    /// <summary>
    /// UnitSelections assigns its Instance in Awake, which can run after this component's
    /// OnEnable. The subscription was attempted once and silently skipped when that
    /// happened, so selection changes never reached this bar and the build button stayed
    /// hidden forever. Retried from Update until it takes.
    /// </summary>
    private void EnsureSubscribedToSelection()
    {
        if (subscribedToSelection || UnitSelections.Instance == null) return;

        UnitSelections.Instance.selectionChanged.AddListener(OnSelectionChanged);
        subscribedToSelection = true;
    }

    private void OnDisable()
    {
        if (subscribedToSelection && UnitSelections.Instance != null)
        {
            UnitSelections.Instance.selectionChanged.RemoveListener(OnSelectionChanged);
        }
        subscribedToSelection = false;

        if (diveButton != null) diveButton.onClick.RemoveListener(OnDiveButtonClicked);
        if (deliverButton != null) deliverButton.onClick.RemoveListener(OnDeliverButtonClicked);
    }

    /// <summary>
    /// Persistent Button.onClick target for the TMP Build Harbor Button.
    /// Kept public so the scene wiring is visible and inspectable in Unity.
    /// </summary>
    public void BuildHarbor()
    {
        buildInteraction?.Build(null);
    }

    private void OnDiveButtonClicked()
    {
        if (diveInteraction == null) return;
        
        if (diveInteraction.IsSubmerged)
        {
            diveInteraction.Surface();
        }
        else
        {
            diveInteraction.Dive();
        }
    }

    private void OnSelectionChanged(System.Collections.Generic.List<Unit> selectedUnits)
    {
        RefreshSelectedComponents();
    }

    private void RefreshSelectedComponents()
    {
        Unit selectedUnit = null;
        if (UnitSelections.Instance != null && UnitSelections.Instance.unitsSelected != null && UnitSelections.Instance.unitsSelected.Count > 0)
        {
            selectedUnit = UnitSelections.Instance.FocusedUnit;
        }

        trackedUnit = selectedUnit;

        if (selectedUnit != null)
        {
            buildInteraction = selectedUnit.GetComponent<BuildInteraction>();
            if (buildInteraction == null && InfluenceManager.IsBoatUnit(selectedUnit))
            {
                buildInteraction = selectedUnit.gameObject.AddComponent<BuildInteraction>();
            }

            diveInteraction = selectedUnit.GetComponent<DiveInteraction>();

            // Added on demand like BuildInteraction above - no vessel prefab carries it.
            deliverInteraction = selectedUnit.GetComponent<DeliverInteraction>();
            if (deliverInteraction == null && InfluenceManager.IsBoatUnit(selectedUnit))
            {
                deliverInteraction = selectedUnit.gameObject.AddComponent<DeliverInteraction>();
            }
        }
        else
        {
            buildInteraction = null;
            diveInteraction = null;
            deliverInteraction = null;
        }

        UpdateButtonVisibility();
    }

    private void Update()
    {
        EnsureSubscribedToSelection();

        // Re-resolve on a focus change even if the selection event was missed, so the
        // bar can never be left pointing at a unit that is no longer selected.
        Unit focused = UnitSelections.Instance != null ? UnitSelections.Instance.FocusedUnit : null;
        if (focused != trackedUnit)
        {
            RefreshSelectedComponents();
            return;
        }

        UpdateButtonVisibility();
    }

    /// <summary>Persistent Button.onClick target for the deliver-cargo button.</summary>
    public void DeliverCargo()
    {
        deliverInteraction?.DeliverAll();
    }

    private void OnDeliverButtonClicked() => DeliverCargo();

    private void UpdateButtonVisibility()
    {
        if (deliverButton != null)
        {
            deliverButton.gameObject.SetActive(deliverInteraction != null && deliverInteraction.CanDeliver());
        }

        if (buildButton != null)
        {
            bool canBuild = buildInteraction != null && buildInteraction.CanBuild();
            buildButton.gameObject.SetActive(canBuild);
        }

        if (diveButton != null)
        {
            bool canDive = diveInteraction != null && diveInteraction.CanDive();
            bool canSurface = diveInteraction != null && diveInteraction.CanSurface();
            
            diveButton.gameObject.SetActive(canDive || canSurface);

            // Update text to say "Surface" if submerged
            string label = (diveInteraction != null && diveInteraction.IsSubmerged) ? "Surface" : "Dive";
            TMP_Text tmpText = diveButton.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                tmpText.text = label;
            }
            else
            {
                Text btnText = diveButton.GetComponentInChildren<Text>();
                if (btnText != null)
                {
                    btnText.text = label;
                }
            }
        }
    }
}

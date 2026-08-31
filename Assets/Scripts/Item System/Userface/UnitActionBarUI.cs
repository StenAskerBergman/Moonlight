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

    private BuildInteraction buildInteraction;
    private DiveInteraction diveInteraction;

    private void OnEnable()
    {
        if (UnitSelections.Instance != null)
        {
            UnitSelections.Instance.selectionChanged.AddListener(OnSelectionChanged);
        }

        if (buildButton != null) buildButton.onClick.AddListener(OnBuildButtonClicked);
        if (diveButton != null) diveButton.onClick.AddListener(OnDiveButtonClicked);

        RefreshSelectedComponents();
    }

    private void OnDisable()
    {
        if (UnitSelections.Instance != null)
        {
            UnitSelections.Instance.selectionChanged.RemoveListener(OnSelectionChanged);
        }

        if (buildButton != null) buildButton.onClick.RemoveListener(OnBuildButtonClicked);
        if (diveButton != null) diveButton.onClick.RemoveListener(OnDiveButtonClicked);
    }

    private void OnBuildButtonClicked()
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
        if (UnitSelections.Instance != null && UnitSelections.Instance.unitsSelected.Count > 0)
        {
            selectedUnit = UnitSelections.Instance.unitsSelected[0];
        }

        buildInteraction = selectedUnit != null ? selectedUnit.GetComponent<BuildInteraction>() : null;
        diveInteraction = selectedUnit != null ? selectedUnit.GetComponent<DiveInteraction>() : null;

        UpdateButtonVisibility();
    }

    private void Update()
    {
        UpdateButtonVisibility();
    }

    private void UpdateButtonVisibility()
    {
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
            Text btnText = diveButton.GetComponentInChildren<Text>();
            if (btnText != null && diveInteraction != null)
            {
                btnText.text = diveInteraction.IsSubmerged ? "Surface" : "Dive";
            }
        }
    }
}

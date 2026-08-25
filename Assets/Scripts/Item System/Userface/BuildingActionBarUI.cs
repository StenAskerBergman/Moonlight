using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD panel shown for the currently selected building (BuildingSelections). Exposes
/// an "Order Truck" button wired to TransportManager.RequestTruck(), disabled while
/// that building's manual-order cooldown is active. Mirrors UnitActionBarUI's
/// selection-listener + per-frame poll pattern, since cooldown state changes
/// without a new selection event.
/// </summary>
public class BuildingActionBarUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button orderTruckButton;
    [SerializeField] private TMP_Text orderTruckLabel; // shows "Order Truck" or the remaining cooldown

    private Building selectedBuilding;

    private void OnEnable()
    {
        if (BuildingSelections.Instance != null)
        {
            BuildingSelections.Instance.selectionChanged.AddListener(OnSelectionChanged);
        }

        if (orderTruckButton != null)
        {
            orderTruckButton.onClick.AddListener(OnOrderTruckClicked);
        }

        RefreshSelectedBuilding();
    }

    private void OnDisable()
    {
        if (BuildingSelections.Instance != null)
        {
            BuildingSelections.Instance.selectionChanged.RemoveListener(OnSelectionChanged);
        }

        if (orderTruckButton != null)
        {
            orderTruckButton.onClick.RemoveListener(OnOrderTruckClicked);
        }
    }

    private void OnSelectionChanged(Building building)
    {
        RefreshSelectedBuilding();
    }

    private void RefreshSelectedBuilding()
    {
        selectedBuilding = BuildingSelections.Instance != null ? BuildingSelections.Instance.SelectedBuilding : null;
        UpdatePanel();
    }

    private void Update()
    {
        // Cooldown ticks down without a selection-changed event, so poll like
        // UnitActionBarUI does for CanBuild()/CanDive().
        UpdatePanel();
    }

    private void UpdatePanel()
    {
        bool hasBuilding = selectedBuilding != null;

        if (panelRoot != null) panelRoot.SetActive(hasBuilding);
        if (!hasBuilding || orderTruckButton == null) return;

        float cooldown = TransportManager.Instance != null
            ? TransportManager.Instance.GetCooldownRemaining(selectedBuilding)
            : 0f;

        orderTruckButton.interactable = cooldown <= 0f;

        if (orderTruckLabel != null)
        {
            orderTruckLabel.text = cooldown > 0f ? $"Order Truck ({cooldown:F0}s)" : "Order Truck";
        }
    }

    private void OnOrderTruckClicked()
    {
        if (selectedBuilding == null || TransportManager.Instance == null) return;
        TransportManager.Instance.RequestTruck(selectedBuilding);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingActive : MonoBehaviour
{
    private Building building;
    private BuildingProductionController productionController;

    private int originalProductionRate;
    private bool hasCachedProductionRate;

    [SerializeField] private float destroyedDisableDelay = 2f;

    private void Awake()
    {
        building = GetComponent<Building>();
        productionController = GetComponent<BuildingProductionController>();
        CacheOriginalProductionRate();
    }

    private void OnEnable()
    {
        Building.OnBuildingStateChanged += HandleBuildingStateChanged;
    }

    private void OnDisable()
    {
        Building.OnBuildingStateChanged -= HandleBuildingStateChanged;
    }

    private void HandleBuildingStateChanged(Building changedBuilding, BuildingEnums.BuildingState newState)
    {
        if (changedBuilding != building) return;

        switch (newState)
        {
            case BuildingEnums.BuildingState.Active:
                CacheOriginalProductionRate();
                productionController?.SetProductionRate(originalProductionRate);
                break;

            case BuildingEnums.BuildingState.Inactive:
            case BuildingEnums.BuildingState.Paused:
                CacheOriginalProductionRate();
                productionController?.SetProductionRate(0);
                break;

            case BuildingEnums.BuildingState.Destroyed:
                StartCoroutine(DisableAfterDelay());
                break;
        }
    }

    // TODO: BuildingProductionController's production data is only ever set via its
    // (currently unwired) BuildingProductionData asset - once that wiring exists this
    // cache will correctly capture the designer-configured rate the first time it's read.
    private void CacheOriginalProductionRate()
    {
        if (hasCachedProductionRate || productionController == null) return;

        originalProductionRate = productionController.GetProductionRate();
        hasCachedProductionRate = true;
    }

    private IEnumerator DisableAfterDelay()
    {
        yield return new WaitForSeconds(destroyedDisableDelay);
        gameObject.SetActive(false);
    }

    public void Activate()
    {
        building?.SetState(BuildingEnums.BuildingState.Active);
    }

    public void Deactivate()
    {
        building?.SetState(BuildingEnums.BuildingState.Inactive);
    }
}

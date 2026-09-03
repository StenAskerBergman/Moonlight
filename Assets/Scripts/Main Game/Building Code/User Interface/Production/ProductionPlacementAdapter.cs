using UnityEngine;

/// <summary>
/// The only bridge between the production graph and Moonlight's placement pipeline.
/// The renderer deals exclusively in BuildingData and delegates placement here.
/// </summary>
public sealed class ProductionPlacementAdapter : MonoBehaviour
{
    [SerializeField] private BuildingSelector buildingSelector;

    private void Awake()
    {
        if (buildingSelector == null)
        {
            buildingSelector = FindObjectOfType<BuildingSelector>();
        }
    }

    public bool CanPlace(BuildingData buildingData)
    {
        return buildingData != null && ResolvePrefab(buildingData) != null;
    }

    public void BeginPlacement(BuildingData buildingData)
    {
        if (buildingData == null) return;

        if (buildingSelector == null)
        {
            buildingSelector = FindObjectOfType<BuildingSelector>();
        }

        if (buildingSelector == null)
        {
            Debug.LogWarning($"Cannot place '{buildingData.buildingName}': no BuildingSelector is active.", this);
            return;
        }

        GameObject prefab = ResolvePrefab(buildingData);
        if (prefab == null)
        {
            Debug.LogWarning(
                $"Cannot place '{buildingData.buildingName}': BuildingPrefabRegistry has no prefab for " +
                $"'{buildingData.Id}'. Register the building prefab rather than adding placement logic to the production UI.",
                this);
            return;
        }

        buildingSelector.CancelPreview();
        buildingSelector.SpawnPreview(prefab);
    }

    private static GameObject ResolvePrefab(BuildingData buildingData)
    {
        BuildingPrefabRegistry registry = BuildingPrefabRegistry.Instance;
        return registry != null ? registry.GetPrefab(buildingData.Id.ToString()) : null;
    }
}

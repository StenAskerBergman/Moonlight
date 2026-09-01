using UnityEngine;

/// <summary>
/// Gives a building a stand-in model while its real art does not exist.
///
/// Attach it to a building prefab, or let <see cref="Ensure"/> add it at spawn time.
/// By default it only builds when the building has no geometry of its own, so dropping
/// real art into the prefab automatically retires the placeholder - no code change needed.
/// </summary>
[DisallowMultipleComponent]
public class BuildingPlaceholderModel : MonoBehaviour
{
    [Header("Library")]
    [Tooltip("Leave empty to use the shared library from Resources, or the built-in defaults.")]
    [SerializeField] private PlaceholderModelLibrary library;

    [Header("Behaviour")]
    [Tooltip("Build the placeholder even when the building already has real geometry.")]
    [SerializeField] private bool alwaysUsePlaceholder;

    [Tooltip("Overrides the profile picked from the library. Leave the key empty; it is ignored.")]
    [SerializeField] private bool overrideProfile;
    [SerializeField] private PlaceholderModelProfile profileOverride = new PlaceholderModelProfile();

    [Header("Footprint")]
    [Tooltip("Used when the building has no BuildingData / BuildingProperties size to read.")]
    [SerializeField] private Vector2 fallbackFootprint = new Vector2(2f, 2f);
    [SerializeField] private PlaceholderModelFactory.FootprintAlignment alignment =
        PlaceholderModelFactory.FootprintAlignment.Centered;

    [Tooltip("Add a box collider sized to the footprint. Turn off if the prefab has its own.")]
    [SerializeField] private bool addCollider = true;

    private GameObject _placeholderInstance;

    /// <summary>The generated placeholder, or null while none is built.</summary>
    public GameObject PlaceholderInstance => _placeholderInstance;

    private void Awake()
    {
        Build();
    }

    /// <summary>
    /// Adds the component to a spawned building if it does not have one, then builds.
    /// Safe to call on anything - buildings that already have art keep it.
    /// </summary>
    /// <param name="withCollider">
    /// Pass false for blueprints - a collider on the preview would intercept the placement raycast.
    /// </param>
    public static BuildingPlaceholderModel Ensure(GameObject buildingInstance, bool withCollider = true)
    {
        if (buildingInstance == null) return null;

        BuildingPlaceholderModel placeholder = buildingInstance.GetComponent<BuildingPlaceholderModel>();
        if (placeholder != null)
        {
            placeholder.addCollider = placeholder.addCollider && withCollider;

            // Awake has already run for a prefab-authored component; only build if it has not.
            if (placeholder._placeholderInstance == null) placeholder.Build();
            return placeholder;
        }

        if (!PlaceholderModelFactory.NeedsPlaceholder(buildingInstance)) return null;

        // AddComponent runs Awake immediately, which already builds - rebuild only if the
        // collider preference differs from what Awake used.
        placeholder = buildingInstance.AddComponent<BuildingPlaceholderModel>();
        if (placeholder._placeholderInstance == null || !withCollider)
        {
            placeholder.addCollider = withCollider;
            placeholder.Build();
        }
        return placeholder;
    }

    /// <summary>Builds (or rebuilds) the stand-in geometry.</summary>
    [ContextMenu("Rebuild Placeholder")]
    public void Build()
    {
        if (!alwaysUsePlaceholder && !PlaceholderModelFactory.NeedsPlaceholder(gameObject))
        {
            // Real art is present - nothing to stand in for.
            return;
        }

        PlaceholderModelLibrary activeLibrary = library != null ? library : PlaceholderModelLibrary.RuntimeDefault;
        BuildingData data = ResolveBuildingData();

        PlaceholderModelProfile profile = overrideProfile
            ? profileOverride
            : activeLibrary.Resolve(data, gameObject.name);

        _placeholderInstance = PlaceholderModelFactory.Build(
            transform,
            profile,
            ResolveFootprint(data),
            alignment,
            addCollider);
    }

    /// <summary>Removes the stand-in, e.g. once real art is swapped in at runtime.</summary>
    [ContextMenu("Clear Placeholder")]
    public void Clear()
    {
        PlaceholderModelFactory.Clear(transform);
        _placeholderInstance = null;
    }

    private BuildingData ResolveBuildingData()
    {
        BuildingProperties properties = GetComponent<BuildingProperties>();
        if (properties != null && properties.buildingData != null) return properties.buildingData;

        BuildingPreview preview = GetComponent<BuildingPreview>();
        if (preview != null && preview.buildingData != null) return preview.buildingData;

        return null;
    }

    private Vector2 ResolveFootprint(BuildingData data)
    {
        // buildingSize is expressed in grid cells, and MapGrid lays cells out at 1 world unit.
        if (data != null && data.buildingSize.x > 0f && data.buildingSize.z > 0f)
        {
            return new Vector2(data.buildingSize.x, data.buildingSize.z);
        }

        BuildingProperties properties = GetComponent<BuildingProperties>();
        if (properties != null && properties.buildingSize.x > 0f && properties.buildingSize.z > 0f)
        {
            return new Vector2(properties.buildingSize.x, properties.buildingSize.z);
        }

        return fallbackFootprint;
    }
}

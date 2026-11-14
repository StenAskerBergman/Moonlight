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

    [Tooltip("Float the building's name and role over it in play mode. Blueprints switch this off.")]
    [SerializeField] private bool showLabel = true;

    private GameObject _placeholderInstance;
    private bool _builtWithCollider;
    private bool _builtWithLabel;

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
    /// <param name="withLabel">
    /// Pass false for blueprints - the preview re-tints every MeshRenderer valid/invalid, which
    /// would turn the label text into a solid block.
    /// </param>
    public static BuildingPlaceholderModel Ensure(GameObject buildingInstance, bool withCollider = true,
                                                  bool withLabel = true)
    {
        if (buildingInstance == null) return null;

        BuildingPlaceholderModel placeholder = buildingInstance.GetComponent<BuildingPlaceholderModel>();
        if (placeholder == null)
        {
            if (!PlaceholderModelFactory.NeedsPlaceholder(buildingInstance)) return null;

            // AddComponent runs Awake immediately, which already builds with the defaults.
            placeholder = buildingInstance.AddComponent<BuildingPlaceholderModel>();
        }

        // A prefab-authored component keeps its own "off" choices; the caller can only narrow them.
        placeholder.addCollider = placeholder.addCollider && withCollider;
        placeholder.showLabel = placeholder.showLabel && withLabel;

        // Awake has usually built already. Rebuild only when it has not, or when it was built
        // with parts this caller does not want (a collider or label on a blueprint).
        if (placeholder._placeholderInstance == null
            || placeholder._builtWithCollider != placeholder.addCollider
            || placeholder._builtWithLabel != placeholder.WantsLabel())
        {
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

        string displayName = ResolveDisplayName(data);
        PlaceholderModelProfile profile = overrideProfile
            ? profileOverride
            : activeLibrary.Resolve(data, displayName);

        bool withLabel = WantsLabel() && activeLibrary.showLabels && profile.showLabel;

        _placeholderInstance = PlaceholderModelFactory.Build(
            transform,
            profile,
            ResolveFootprint(data),
            alignment,
            addCollider,
            withLabel ? displayName : null);

        _builtWithCollider = addCollider;
        _builtWithLabel = WantsLabel();
    }

    /// <summary>Labels exist only in play mode, so edit-time builds never count as missing one.</summary>
    private bool WantsLabel() => showLabel && Application.isPlaying;

    /// <summary>The player-facing name: the data asset's, else the object name without "(Clone)".</summary>
    private string ResolveDisplayName(BuildingData data)
    {
        if (data != null && !string.IsNullOrWhiteSpace(data.buildingName)) return data.buildingName;

        BuildingProperties properties = GetComponent<BuildingProperties>();
        if (properties != null && !string.IsNullOrWhiteSpace(properties.buildingName)) return properties.buildingName;

        return gameObject.name.Replace("(Clone)", string.Empty).Trim();
    }

    /// <summary>Removes the stand-in, e.g. once real art is swapped in at runtime.</summary>
    [ContextMenu("Clear Placeholder")]
    public void Clear()
    {
        PlaceholderModelFactory.Clear(transform);
        _placeholderInstance = null;
        _builtWithCollider = false;
        _builtWithLabel = false;
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

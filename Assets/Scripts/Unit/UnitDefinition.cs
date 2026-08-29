using UnityEngine;

/// <summary>
/// Data-driven definition of a unit. Ties together the prefab, movement profile,
/// UI type, and name category into one indivisible package that the factory applies
/// at creation time.
///
/// OnValidate guarantees that:
///   - The prefab is a project asset (not a scene object)
///   - The movement profile is assigned
///   - The profile is compatible with the prefab's components
///
/// Convention follows ItemData ("Data/Item/Item Data"),
/// BuildingData ("Data/Building/Building Data"), etc.
/// </summary>
[CreateAssetMenu(fileName = "New Unit Definition", menuName = "Data/Unit/Unit Definition")]
public class UnitDefinition : ScriptableObject, IIdentifiable
{
    [Header("Identity")]
    [Tooltip("Namespaced identifier (e.g. 'core:patrol_boat', 'core:cargo_truck', 'modname:custom_submarine').")]
    [SerializeField] private string identifier = "core:unit";

    public Identifier Id => !string.IsNullOrEmpty(identifier)
        ? new Identifier(identifier)
        : new Identifier($"core:{name.ToLowerInvariant().Replace(' ', '_')}");

    [Tooltip("Human-readable category name (e.g. 'Patrol Boat', 'Cargo Ship').")]
    public string displayCategory;

    [Header("Prefab")]
    [Tooltip("Must be a project asset, not a scene object.")]
    public GameObject prefab;

    [Header("Classification")]
    [Tooltip("UI / selection routing (Character, House, Drone).")]
    public UnitType unitType;

    [Tooltip("Movement configuration — determines NavMesh agent type, area mask, speed, etc.")]
    public MovementProfile movementProfile;

    [Tooltip("Name pool to draw random display names from.")]
    public NameType nameType;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (prefab == null) return; // Nothing to validate yet

        // Reject scene objects — they have no asset path.
        // This is the exact defect in UnitManager.unitPrefab today: it points at
        // Boat (Unit Inventory) (1) in the scene, not at a project-level prefab.
        string path = UnityEditor.AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError($"UnitDefinition '{name}': prefab '{prefab.name}' is a scene object, " +
                           $"not a project asset. Assign a prefab from the Project window.");
        }

        if (movementProfile == null)
        {
            Debug.LogWarning($"UnitDefinition '{name}': movementProfile is not assigned.");
            return;
        }

        if (!movementProfile.Validate(prefab, out string error))
        {
            Debug.LogError($"UnitDefinition '{name}': {error}");
        }
    }
#endif
}

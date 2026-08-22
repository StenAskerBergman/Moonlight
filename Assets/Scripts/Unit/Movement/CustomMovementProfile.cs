using UnityEngine;

/// <summary>
/// Movement profile for units that do not use NavMesh (e.g. Aircraft).
/// Apply() is a stub until the custom movement system is implemented.
///
/// The unit is correctly classified by domain from creation — the factory
/// sets MoveType.Aircraft, the validator checks for no NavMeshAgent, and
/// custom flight logic will attach in a later slice.
/// </summary>
[CreateAssetMenu(fileName = "New Custom Profile", menuName = "Data/Unit/Movement/Custom Profile")]
public class CustomMovementProfile : MovementProfile
{
    [Header("Movement Domain")]
    [SerializeField] private MoveType domain = MoveType.Aircraft;

    [Header("Flight Configuration (placeholder)")]
    [Tooltip("Target altitude band for this unit type.")]
    [SerializeField] private float altitudeBand = 50f;

    [Tooltip("Cruise speed when flight is implemented.")]
    [SerializeField] private float cruiseSpeed = 10f;

    public override MoveType Domain => domain;

    public override void Apply(GameObject unit)
    {
        // Aircraft movement is not yet implemented. The unit is correctly
        // classified by domain; custom flight logic will be added in a later slice.
        Debug.Log($"<color=yellow>CustomMovementProfile.Apply:</color> '{unit.name}' classified " +
                  $"as {domain}. Custom movement not yet implemented — unit will be stationary.");
    }

    public override bool Validate(GameObject prefab, out string error)
    {
        if (prefab == null)
        {
            error = "Prefab is null.";
            return false;
        }

        var agent = prefab.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            error = $"Prefab '{prefab.name}' has a NavMeshAgent, but CustomMovementProfile " +
                    $"'{name}' does not use NavMesh. Remove the NavMeshAgent or use a " +
                    $"NavMeshMovementProfile instead.";
            return false;
        }

        error = null;
        return true;
    }
}

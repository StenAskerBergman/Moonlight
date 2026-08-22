using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Movement profile for NavMesh-based units (ships, humanoids, submarines).
/// Carries the full NavMeshAgent configuration and applies it at creation time,
/// guaranteeing the agent type matches the unit's domain.
///
/// Pre-authored assets:
///   Ship Profile     — agentTypeID -1372625422, areaMask 656, TravelMedium Water
///   Humanoid Profile — agentTypeID 0, TravelMedium Ground
///   Submarine Profile — agentTypeID -334000983
///
///   Landcraft Profile (Truck) — ground vehicle, domain MoveType.Landcraft
///     agentTypeID: -1234567890 placeholder // TODO: replace with the actual baked
///                  agent type ID once a Landcraft agent type exists in
///                  Navigation > Agents settings.
///     areaMask: Walkable (1) — a dedicated road NavMesh area can be added to the
///               mask once road surfaces are baked with their own area.
///     TravelMedium: Ground layer
///   No asset exists yet for this profile — create one via
///   Data/Unit/Movement/NavMesh Profile with domain set to Landcraft in the
///   Inspector; Apply() already handles any domain generically, so no code beyond
///   the asset itself is required.
/// </summary>
[CreateAssetMenu(fileName = "New NavMesh Profile", menuName = "Data/Unit/Movement/NavMesh Profile")]
public class NavMeshMovementProfile : MovementProfile
{
    [Header("Movement Domain")]
    [SerializeField] private MoveType domain = MoveType.Watercraft;

    [Header("NavMesh Agent Configuration")]
    [Tooltip("Must match a defined agent type in Navigation > Agents settings.")]
    [SerializeField] private int agentTypeID;

    [Tooltip("Bitmask of walkable NavMesh areas. -1 = Everything.")]
    [SerializeField] private int areaMask = -1;

    [SerializeField] private float baseOffset = -3f;
    [SerializeField] private float speed = 3.5f;
    [SerializeField] private float angularSpeed = 120f;
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float stoppingDistance = 0f;

    [Header("Physics Raycast")]
    [Tooltip("LayerMask for movement-order raycasts (UnitMovement.TravelMedium).")]
    [SerializeField] private LayerMask travelMedium;

    public int AgentTypeID => agentTypeID;
    public override MoveType Domain => domain;

    public override void Apply(GameObject unit)
    {
        // --- NavMeshAgent ---
        var agent = unit.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError($"NavMeshMovementProfile.Apply: '{unit.name}' has no NavMeshAgent. " +
                           $"Profile '{name}' requires one.");
            return;
        }

        agent.agentTypeID = agentTypeID;
        agent.areaMask    = areaMask;
        agent.baseOffset  = baseOffset;
        agent.speed       = speed;
        agent.angularSpeed = angularSpeed;
        agent.acceleration = acceleration;
        agent.stoppingDistance = stoppingDistance;

        // --- UnitMovement ---
        var movement = unit.GetComponent<UnitMovement>();
        if (movement != null)
        {
            movement.TravelMedium = travelMedium;
        }
    }

    public override bool Validate(GameObject prefab, out string error)
    {
        if (prefab == null)
        {
            error = "Prefab is null.";
            return false;
        }

        var agent = prefab.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            error = $"Prefab '{prefab.name}' has no NavMeshAgent, but NavMeshMovementProfile " +
                    $"'{name}' requires one.";
            return false;
        }

        // agentTypeID on the prefab may differ if the profile hasn't been applied yet.
        // This is correctable rather than fatal - Apply() overwrites it at creation - so
        // warn here directly. Returning true with `error` set would be unreachable:
        // UnitDefinition.OnValidate only surfaces the message when Validate returns false.
        if (agent.agentTypeID != agentTypeID && agent.agentTypeID != 0)
        {
            Debug.LogWarning($"NavMeshMovementProfile '{name}': prefab '{prefab.name}' has " +
                             $"agentTypeID {agent.agentTypeID}, profile expects {agentTypeID}. " +
                             $"Apply() corrects this at creation; consider updating the prefab.");
        }

        error = null;
        return true;
    }
}

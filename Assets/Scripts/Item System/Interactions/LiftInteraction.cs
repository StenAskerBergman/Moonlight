using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Aircraft take-off, climb and landing orders. The mirror of DiveInteraction.
///
/// The one thing that genuinely differs from a dive: an aircraft parked on the
/// apron is on a band that follows the terrain, while every band above it is a
/// flat proxy. A climb therefore has a fixed ceiling per step, and "climb" walks
/// the bands in order rather than jumping straight to the top.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class LiftInteraction : MonoBehaviour, ILiftable
{
    [System.Serializable]
    public class AltitudeBand
    {
        public string label = "Low";
        public float height = 25f;
        public int area = NavAreas.LowAltitude;
    }

    [Tooltip("Ground level, where the aircraft sits. Must match the Air - Apron band.")]
    [SerializeField] private float apronHeight = 0f;

    [Tooltip("Altitude bands, lowest first. Must match the Air - * bands.")]
    [SerializeField]
    private AltitudeBand[] bands =
    {
        new AltitudeBand { label = "Low",  height = 25f, area = NavAreas.LowAltitude },
        new AltitudeBand { label = "Mid",  height = 50f, area = NavAreas.MidAltitude },
        new AltitudeBand { label = "High", height = 75f, area = NavAreas.HighAltitude },
    };

    [Tooltip("Seconds before this airframe can change altitude again.")]
    [SerializeField] private float cooldown = 4f;

    [Tooltip("How far from the target band the aircraft may be and still reach it.")]
    [SerializeField] private float sampleTolerance = 8f;

    /// <summary>Raised when an order is refused, with the reason. Hook UI to this.</summary>
    public event System.Action<string> OnLiftRefused;

    private NavMeshAgent agent;
    private NavLinkTraversal traversal;
    private float readyAt;

    /// <summary>Index of the band the aircraft is on, or -1 when it is on the apron.</summary>
    public int CurrentBandIndex
    {
        get
        {
            int area = NavLayerTransit.CurrentArea(agent);
            for (int i = 0; i < bands.Length; i++)
            {
                if (bands[i].area == area) return i;
            }
            return -1;
        }
    }

    /// <summary>True when the aircraft is airborne rather than on the apron.</summary>
    public bool IsAirborne { get { return CurrentBandIndex >= 0; } }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        traversal = GetComponent<NavLinkTraversal>();
    }

    /// <summary>Climb one band. From the apron this is the take-off.</summary>
    public bool LiftOff()
    {
        if (!Ready("take off")) return false;

        int next = CurrentBandIndex + 1;
        if (next >= bands.Length)
        {
            OnLiftRefused?.Invoke("Already at maximum altitude.");
            return false;
        }

        AltitudeBand target = bands[next];

        if (!NavLayerTransit.IsAreaAllowed(agent, target.area))
        {
            OnLiftRefused?.Invoke($"This airframe cannot reach {target.label} altitude.");
            return false;
        }

        if (!NavLayerTransit.MoveToLayer(agent, target.height, target.area, sampleTolerance))
        {
            OnLiftRefused?.Invoke($"No climb route to {target.label} altitude from here.");
            return false;
        }

        readyAt = Time.time + cooldown;
        return true;
    }

    /// <summary>Descend one band, or onto the apron from the lowest one.</summary>
    public bool Land()
    {
        if (!Ready("land")) return false;

        int current = CurrentBandIndex;
        if (current < 0)
        {
            OnLiftRefused?.Invoke("Already on the ground.");
            return false;
        }

        float height = current == 0 ? apronHeight : bands[current - 1].height;
        int area = current == 0 ? NavAreas.Walkable : bands[current - 1].area;

        if (!NavLayerTransit.MoveToLayer(agent, height, area, sampleTolerance))
        {
            OnLiftRefused?.Invoke(current == 0
                ? "Nowhere to land here."
                : "No descent route from here.");
            return false;
        }

        readyAt = Time.time + cooldown;
        return true;
    }

    private bool Ready(string verb)
    {
        if (traversal != null && traversal.IsTraversing)
        {
            OnLiftRefused?.Invoke($"Cannot {verb} mid-transit.");
            return false;
        }

        if (Time.time < readyAt)
        {
            OnLiftRefused?.Invoke($"Cannot {verb} for another {readyAt - Time.time:F1}s.");
            return false;
        }

        return true;
    }
}

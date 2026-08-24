using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drives an agent across a NavMeshLink by hand instead of letting the agent snap
/// across it, so a dive or a take-off takes time and can be seen.
///
/// Unity's automatic link traversal moves the agent across in a frame or two,
/// which reads as a submarine blinking to the seabed. Turning
/// autoTraverseOffMeshLink off and calling CompleteOffMeshLink ourselves is the
/// supported way to own that motion, and it is also the only place where "this
/// unit is currently diving" becomes a fact other systems can observe - which is
/// what the events are for.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NavLinkTraversal : MonoBehaviour
{
    /// <summary>What a link crossing means, derived from the areas at each end.</summary>
    public enum TransitionKind
    {
        Other,
        Dive,
        Surface,
        Climb,
        Descend,
    }

    [Tooltip("Seconds to dive or surface.")]
    [SerializeField] private float diveDuration = 2.5f;

    [Tooltip("Seconds to take off, climb or descend.")]
    [SerializeField] private float climbDuration = 2f;

    [Tooltip("Seconds for anything else, e.g. a Jump link.")]
    [SerializeField] private float defaultDuration = 0.75f;

    [Tooltip("Pitch the hull nose-down while descending and nose-up while climbing. " +
             "0 keeps it level.")]
    [SerializeField] private float pitchDegrees = 20f;

    [Tooltip("Eases the traversal instead of moving at a constant rate.")]
    [SerializeField] private AnimationCurve profile = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    /// <summary>Raised when a link crossing starts.</summary>
    public event System.Action<TransitionKind> OnTraversalStarted;

    /// <summary>Raised when a link crossing finishes.</summary>
    public event System.Action<TransitionKind> OnTraversalFinished;

    /// <summary>True while the unit is on a link. Movement orders must not interrupt this.</summary>
    public bool IsTraversing { get; private set; }

    /// <summary>What is currently being crossed, or Other when idle.</summary>
    public TransitionKind CurrentTransition { get; private set; } = TransitionKind.Other;

    private NavMeshAgent agent;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        // We move the unit across links ourselves; see the class comment.
        agent.autoTraverseOffMeshLink = false;
    }

    private void Update()
    {
        if (IsTraversing) return;
        if (!agent.isOnNavMesh || !agent.isOnOffMeshLink) return;

        StartCoroutine(Traverse(agent.currentOffMeshLinkData));
    }

    private IEnumerator Traverse(OffMeshLinkData link)
    {
        IsTraversing = true;

        Vector3 start = agent.transform.position;
        Vector3 end = link.endPos + Vector3.up * agent.baseOffset;

        TransitionKind kind = Classify(link.startPos, link.endPos);
        CurrentTransition = kind;
        OnTraversalStarted?.Invoke(kind);

        float duration = DurationFor(kind);
        Quaternion startRotation = transform.rotation;
        Quaternion pitched = PitchFor(start, end, startRotation);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = profile.Evaluate(Mathf.Clamp01(t / duration));
            agent.transform.position = Vector3.Lerp(start, end, k);

            // Pitch in over the first quarter and out over the last, so the hull is
            // level again by the time it arrives on the far layer.
            float lean = Mathf.Min(Mathf.Clamp01(t / (duration * 0.25f)),
                                   Mathf.Clamp01((duration - t) / (duration * 0.25f)));
            transform.rotation = Quaternion.Slerp(startRotation, pitched, lean);

            yield return null;
        }

        agent.transform.position = end;
        transform.rotation = startRotation;
        agent.CompleteOffMeshLink();

        IsTraversing = false;
        CurrentTransition = TransitionKind.Other;
        OnTraversalFinished?.Invoke(kind);
    }

    /// <summary>
    /// Works out what the crossing is from the areas at each end.
    ///
    /// A NavMeshLink is not backed by an OffMeshLink component, so
    /// OffMeshLinkData.offMeshLink is null and the link's own area cannot be read
    /// from it. The endpoints, however, sit on the two layers being joined, and
    /// those carry the areas that give the crossing its meaning.
    /// </summary>
    private TransitionKind Classify(Vector3 startPos, Vector3 endPos)
    {
        int from = AreaAt(startPos);
        int to = AreaAt(endPos);

        if (from == NavAreas.DeepSea || to == NavAreas.DeepSea)
        {
            return to == NavAreas.DeepSea ? TransitionKind.Dive : TransitionKind.Surface;
        }

        if (IsAltitude(from) || IsAltitude(to))
        {
            return endPos.y > startPos.y ? TransitionKind.Climb : TransitionKind.Descend;
        }

        return TransitionKind.Other;
    }

    private int AreaAt(Vector3 position)
    {
        NavMeshHit hit;
        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask,
        };

        if (!NavMesh.SamplePosition(position, out hit, 2f, filter)) return -1;

        // NavMeshHit.mask is a one-bit area mask; turn it back into an index.
        for (int area = 0; area < 32; area++)
        {
            if ((hit.mask & (1 << area)) != 0) return area;
        }

        return -1;
    }

    private static bool IsAltitude(int area)
    {
        return area == NavAreas.LowAltitude
            || area == NavAreas.MidAltitude
            || area == NavAreas.HighAltitude;
    }

    private Quaternion PitchFor(Vector3 start, Vector3 end, Quaternion current)
    {
        if (Mathf.Approximately(pitchDegrees, 0f)) return current;

        // Descending pitches the nose down, climbing pitches it up.
        float sign = end.y < start.y ? 1f : -1f;
        return current * Quaternion.Euler(sign * pitchDegrees, 0f, 0f);
    }

    private float DurationFor(TransitionKind kind)
    {
        switch (kind)
        {
            case TransitionKind.Dive:
            case TransitionKind.Surface:
                return diveDuration;

            case TransitionKind.Climb:
            case TransitionKind.Descend:
                return climbDuration;

            default:
                return defaultDuration;
        }
    }
}

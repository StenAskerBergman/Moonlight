using UnityEngine;

/// <summary>
/// Carries the lobby's choices across the scene load into the match.
///
/// This is a static rather than a DontDestroyOnLoad singleton: there is nothing
/// to instantiate, nothing to wire into the Lobby scene, and no duplicate-instance
/// case to defend against. The one hazard of a static - state surviving into the
/// next play session in the editor - is handled by the SubsystemRegistration
/// reset below, the same guard GameEventBus uses.
///
/// Pending being null is a supported, load-bearing state: it means "no lobby ran",
/// and the Match scene falls back to the values serialized in its own Inspector.
/// That is what keeps pressing Play directly in Match.unity a working workflow.
/// </summary>
public static class GameSession
{
    /// <summary>Config handed over by the lobby, consumed by MatchBootstrapper.
    /// Null when the Match scene was entered directly.</summary>
    public static MatchConfig Pending { get; private set; }

    /// <summary>The config the running match was actually started with. Null when
    /// the match is running on its Inspector defaults.</summary>
    public static MatchConfig Active { get; private set; }

    public static bool HasPending => Pending != null;

    /// <summary>Called by the lobby just before routing into the match.</summary>
    public static void SetPending(MatchConfig config)
    {
        Pending = config != null ? config.Copy() : null;
    }

    /// <summary>
    /// Called by MatchBootstrapper. Consuming is a move, not a read: Pending is
    /// cleared so a later direct entry into the Match scene does not silently
    /// re-apply a config from a lobby visit two matches ago.
    /// </summary>
    public static MatchConfig ConsumePending()
    {
        Active = Pending;
        Pending = null;
        return Active;
    }

    public static void Clear()
    {
        Pending = null;
        Active = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Pending = null;
        Active = null;
    }
}

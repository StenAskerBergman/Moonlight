using UnityEngine;

/// <summary>
/// Applies the lobby's MatchConfig to the Match scene before anything reads it.
///
/// Lives on a GameObject in Match.unity. The negative execution order is what
/// makes this correct: MapManager generates in Start() and PlayerFactionController
/// activates in Start(), both of which run after every Awake() - but
/// PlayerFactionController also builds its dictionary in Awake(), and Awake order
/// between two components is otherwise undefined.
///
/// When GameSession has nothing pending - pressing Play directly in Match.unity -
/// this does nothing at all and the scene runs on its Inspector values. That
/// fallback is intentional; it is what keeps direct-scene iteration usable.
/// </summary>
[DefaultExecutionOrder(-100)]
public class MatchBootstrapper : MonoBehaviour
{
    [Tooltip("Leave empty to find it in the scene.")]
    [SerializeField] private MapManager mapManager;

    [Tooltip("Leave empty to find it in the scene.")]
    [SerializeField] private PlayerFactionController factionController;

    [SerializeField] private bool showLogs = true;

    private void Awake()
    {
        MatchConfig config = GameSession.ConsumePending();

        if (config == null)
        {
            Log("no pending MatchConfig - running on the scene's Inspector values.");
            return;
        }

        Log($"applying {config}");

        Resolve();

        if (mapManager != null)
        {
            mapManager.ApplyConfig(config);
        }
        else
        {
            Debug.LogError("MatchBootstrapper: no MapManager in the scene - the map " +
                           "settings from the lobby cannot be applied.");
        }

        if (factionController != null)
        {
            factionController.SetStartingFactions(config.startingFactions);
        }
        else
        {
            Debug.LogError("MatchBootstrapper: no PlayerFactionController in the scene - " +
                           "the starting factions from the lobby cannot be applied.");
        }
    }

    /// <summary>
    /// Fills in whatever was left unwired in the Inspector. includeInactive is on
    /// because a manager parked under a disabled group is still the one we mean.
    /// </summary>
    private void Resolve()
    {
        if (mapManager == null)
        {
            mapManager = FindObjectOfType<MapManager>(true);
        }

        if (factionController == null)
        {
            factionController = FindObjectOfType<PlayerFactionController>(true);
        }
    }

    private void Log(object message)
    {
        if (showLogs)
        {
            Debug.Log($"<color=lightblue>MatchBootstrapper:</color> {message}");
        }
    }
}

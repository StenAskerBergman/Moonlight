using UnityEngine;

/// <summary>
/// A stand-in for the real lobby, so the config spine can be tested before any
/// lobby UI exists.
///
/// Drop this in the Lobby scene, set the values in the Inspector, press Play:
/// it hands the config to GameSession and routes into the match exactly the way
/// LobbyController eventually will. If island count and starting factions change
/// in the match, Phases 1 and 2 work.
///
/// Delete this once the lobby UI exists.
/// </summary>
public class DebugMatchLauncher : MonoBehaviour
{
    [SerializeField] private MatchConfig config = new MatchConfig();

    [Tooltip("Launch on Start. Turn off to trigger it manually from the context menu.")]
    [SerializeField] private bool launchOnStart = true;

    private void Start()
    {
        if (launchOnStart)
        {
            Launch();
        }
    }

    [ContextMenu("Launch Match")]
    public void Launch()
    {
        Debug.Log($"<color=lightblue>DebugMatchLauncher:</color> launching with {config}");
        GameSession.SetPending(config);
        SceneRouter.ToMatch();
    }
}

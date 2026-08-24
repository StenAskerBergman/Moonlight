using UnityEngine.SceneManagement;

/// <summary>
/// The single place that changes scenes. Every menu button routes through here
/// so the flow is described in one file rather than scattered across UnityEvent
/// wiring in four different scenes.
/// </summary>
public static class SceneRouter
{
    public static void ToMainMenu() => SceneManager.LoadScene(SceneNames.MainMenu);

    public static void ToLobby() => SceneManager.LoadScene(SceneNames.Lobby);

    public static void ToOptions() => SceneManager.LoadScene(SceneNames.Options);

    /// <summary>
    /// Enters the match described by <see cref="GameSession.Pending"/>.
    ///
    /// This currently loads the Match scene directly, which stalls the main thread
    /// for the duration of the load. Once the Loading scene has a controller, this
    /// body becomes LoadScene(SceneNames.Loading) and that controller does the
    /// async load - callers do not change.
    /// </summary>
    public static void ToMatch() => SceneManager.LoadScene(SceneNames.Match);
}

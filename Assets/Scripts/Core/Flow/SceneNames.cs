/// <summary>
/// The scene names registered in Build Settings, in one place.
///
/// Scenes are loaded by name rather than by build index. The previous
/// LoadScene(activeScene.buildIndex + 1) pattern silently loads the wrong scene
/// the moment anything in the Build Settings list is reordered or inserted, and
/// it fails outright when the active scene is last in the list.
/// </summary>
public static class SceneNames
{
    public const string MainMenu = "MainMenu";
    public const string Lobby    = "Lobby";
    public const string Options  = "Options";
    public const string Loading  = "Loading";
    public const string Match    = "Match";
}

using UnityEngine;

/// <summary>
/// Button target for the main menu scene. Hook these to the Buttons' OnClick.
///
/// Method names are the UnityEvent wiring contract - renaming one silently
/// breaks the button that calls it, because the reference is stored as a string.
/// </summary>
public class MainMenu : MonoBehaviour
{
    /// <summary>Play goes to the lobby, not straight into a match - the lobby is
    /// what fills in the MatchConfig the match then reads.</summary>
    public void PlayGame()
    {
        SceneRouter.ToLobby();
    }

    public void OpenOptions()
    {
        SceneRouter.ToOptions();
    }

    public void QuitGame()
    {
        Debug.Log("QUITS");
        Application.Quit();
    }
}

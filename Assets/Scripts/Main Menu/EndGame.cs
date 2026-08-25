using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a transition animation and then leaves the current scene.
///
/// This used to load activeScene.buildIndex + 1, which now resolves to whatever
/// happens to sit after Match in Build Settings. The destination is an explicit
/// scene name instead, defaulting to the main menu - the destination this is
/// actually used for from inside a match.
/// </summary>
public class EndGame : MonoBehaviour
{
    public Animator transition;

    public float transistionTime = 1f;

    [Tooltip("Scene to load once the transition has played. See SceneNames.")]
    [SerializeField] private string targetScene = SceneNames.MainMenu;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.End))
        {
            Debug.Log("End key was pressed.");
            LoadNextLevel();
        }
    }

    public void LoadNextLevel()
    {
        StartCoroutine(LoadLevel(targetScene));
    }

    IEnumerator LoadLevel(string sceneName)
    {
        // Play Animation
        if (transition != null)
        {
            transition.SetTrigger("Start");
        }

        // Wait
        yield return new WaitForSeconds(transistionTime);

        // Load Scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Debug.Log("QUITS");
        Application.Quit();
    }
}

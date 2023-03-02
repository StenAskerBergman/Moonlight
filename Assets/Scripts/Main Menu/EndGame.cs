using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndGame : MonoBehaviour
{
    public Animator transition;

    public float transistionTime = 1f;

    void Update()
    {
        //SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        if (Input.GetKeyDown(KeyCode.End))
        {
            Debug.Log("End key was pressed.");
            LoadNextLevel();
        }

    }

    public void LoadNextLevel()
    {
        StartCoroutine(LoadLevel(SceneManager.GetActiveScene().buildIndex + 1));
    }

    IEnumerator LoadLevel(int levelIndex)
    {
        // Play Animation
        transition.SetTrigger("Start");

        // Wait
        yield return new WaitForSeconds(transistionTime);

        // Load Scene
        SceneManager.LoadScene(levelIndex);

    }

    public void QuitGame()
    {
        Debug.Log("QUITS");
        Application.Quit();

    }
}

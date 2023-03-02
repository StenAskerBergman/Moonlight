using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StateData
{
    public string Name;
    public int Population;
    public string Nation;
    public Dictionary<string, int> Resources;
}

public class GameManager2 : MonoBehaviour
{
    public ControlPoint[] controlPoints;
    public int team1Points;
    public int team2Points;
    public int pointsToWin;

    private void Update()
    {
        // Check Point Management
        CheckPointStatus();
        CheckWinCondition();
        
        // Player State Management
        switch (currentState)
        {
            case GameState.Menu:
                // Show menu screen and handle country selection
                // nation selection screen code goes here
                // Once Done Call this Function 
                StartGame();
                break;

            case GameState.Playing:
                // Handle gameplay
                break;

            case GameState.Observer:
                // Show observer view of the game
                break;

            case GameState.End:
                // Show end screen and allow restart
                break;

        }
    }
    public enum GameState
    {
        Menu,
        Playing,
        Observer,
        End
    }

    private void CheckPointStatus()
    {
        team1Points = 0;
        team2Points = 0;

        foreach (ControlPoint point in controlPoints)
        {
            if (point.teamOwned == 0)
            {
                // Check if a player from team 1 is within capture range
                // If yes, capture the point for team 1
            }
            else if (point.teamOwned == 1)
            {
                team1Points++;
            }
            else if (point.teamOwned == 2)
            {
                team2Points++;
            }
        }
    }
    private void CheckWinCondition()
    {
        if (team1Points >= pointsToWin)
        {
            // Team 1 wins
        }
        else if (team2Points >= pointsToWin)
        {
            // Team 2 wins
        }
    }
    public GameState currentState;

    private void Start()
    {
        currentState = GameState.Menu;
    }
    
    public bool IsInGame()
    {
        return currentState == GameState.Playing;
    }
    private void StartGame()
    {
        string selectionedNation = string.Empty;
        currentState = GameState.Playing;
        //Debug.Log("Game Starting as "+ selectionedNation);
    }

}
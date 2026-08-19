using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class NormalGame : MonoBehaviour
{
    // Session Time
    public int time = 0;

    // Game Time
    private float timeStart = 0;
    private float timeEnd = 0;

      
    public Text textBox;

    public int Score;
    public bool VictoryConditions;

    void Start()
    {
        textBox.text = timeStart.ToString();
    }

    void Update()
    {
        // Count Down
        //timeStart -= Time.deltaTime;

        // Count Up
        timeStart += Time.deltaTime;

        // Round Up Number
        textBox.text = Mathf.Round(timeStart).ToString();
        time = (int)timeStart;

        // Ending 
        if (VictoryConditions) {}
        if (time > timeEnd) {} 
    }
}

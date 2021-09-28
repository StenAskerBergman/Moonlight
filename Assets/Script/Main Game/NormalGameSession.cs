using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NormalGameSession : MonoBehaviour
{
    // Game Time
    public float timeStart = 0;
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

    }
}

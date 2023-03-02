using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugLoggerScript : MonoBehaviour
{
    public bool DebugOnAwake = false;
    public bool DebugOnEnable = false;
    public bool DebugOnDisable = false;
    public bool DebugOnStart = false;

    void Start()
    {
        if (DebugOnStart == true)
        {
            Debug.Log("DebugLoggerScript: Print On Start");
        }
    }
    
    private void Awake()
    {
         if (DebugOnAwake == true)
         {
            Debug.Log("DebugLoggerScript: Print On Awake");
         }
    }
    private void OnDisable()
    {
         if (DebugOnDisable == true)
         {
            Debug.Log("DebugLoggerScript: Print On Disable");
         }    
    }

    private void OnEnable()
    {
        if (DebugOnEnable == true)
        {
            Debug.Log("DebugLoggerScript: Print On Enable");
        }
    }


}

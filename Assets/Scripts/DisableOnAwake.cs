using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableOnAwake : MonoBehaviour
{   
    [Tooltip("Reminder This Auto Selects itself!")]
    [SerializeField] private GameObject thisObject;

    private void Awake()
    {
        thisObject = this.gameObject;
        thisObject.SetActive(true);
    }
}

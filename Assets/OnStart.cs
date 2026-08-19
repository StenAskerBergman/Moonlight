using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OnStart : MonoBehaviour
{
    [SerializeField] private bool setActive = true;
    [SerializeField] private GameObject target;
    void Start()
    {
        if (target == null) return;
        this.target.SetActive(setActive);
    }

}
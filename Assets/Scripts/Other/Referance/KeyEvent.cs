using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyEvent : MonoBehaviour {

    public static int keycount;



    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Error 1");
            GameVariables.keycount += 2;
            Destroy(gameObject);
        }
    }
}

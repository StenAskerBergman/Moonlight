using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerEvent : MonoBehaviour
{
    
	void OnTriggerEnter (Collider other)
    {
        if (other.CompareTag("Player") && GameVariables.keycount>0)
        {
            Debug.Log("Unlocked");
            Destroy(gameObject);
        }
	}
}

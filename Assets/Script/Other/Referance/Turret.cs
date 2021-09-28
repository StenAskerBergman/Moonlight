using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Turret : MonoBehaviour{

	[SerializeField]

	float speed, maxAngle;

	[SerializeField]
	bool isAiming;
	public GameObject player;
	public GameObject VisionField;

	private void Update()
	{

		//If we are aiming at the player we look at it
		if(isAiming)
		{
			transform.LookAt(player.transform);
			Debug.Log ("Target Found" + player.transform);
		}
		else
		{
			transform.rotation = Quaternion.Euler(0, Mathf.Sin(Time.time * speed) * maxAngle, 0);
			Debug.Log ("No Targets Found");
		}
	}

	private void OnTriggerEnter(Collider other)
	{
		if (other.tag == "Player")
		{
			Debug.Log("Bang");
			isAiming = true;
			player = other.gameObject;
			//Turn on shoot script using get component?
		}
	}

	private void OnTriggerExit(Collider other)
	{
		if (other.tag == "Player")
		{

			Debug.Log("No more Targets");
			isAiming = false;
			player = null;
			//Turn off shoot script using get component?
		}
	}
		
	
}


// thanks Fred#1947 - 2018
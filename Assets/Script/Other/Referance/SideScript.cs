using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SideScript : MonoBehaviour {

	public int x;
	public int y;
	public int z;

	public bool FellOver;

	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
	//	transform.rotation = Quaternion.Euler(x, y, z);

		if (x > 89 || x < -89 || y > 89 || y < -89) {
		
			bool FellOver = true;

		}
	}
}

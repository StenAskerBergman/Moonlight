using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Description
// The Player Bank Management 
public enum ResourceType {
    Wood,
    Stone,
    Iron,
    Gold,
    Money,
    Food,
    Water,
    Electricity,
    Population
}

public class Bank : MonoBehaviour
{
	public static int money = 100;
	public static int BM = 100;
	public int wood;
	public int food;
    
    // Start is called before the first frame update
    void Awake()
    {
        //Debug.Log("Bank: BM " + BM);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

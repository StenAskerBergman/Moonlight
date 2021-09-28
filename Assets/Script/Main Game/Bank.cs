using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bank : MonoBehaviour
{
	public static int money = 100;
	public static int stone = 100;
	public int wood;
	public int food;
    // Start is called before the first frame update
    void Awake()
    {
        Debug.Log("Bank: Stone " + stone);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

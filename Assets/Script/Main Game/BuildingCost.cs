using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class BuildingCost : MonoBehaviour
{   
    //håller koll på hur många frames som passerat
	public int costStone = 20;
	
	private int counter = 0;
	private int previous = 0;
	private int division;
	
	public int GetValue(){
		return costStone;
	}
	
	private void DeliveryInterval()
	{
		//Här ändrar du tiden. Inom parantesen på raden under ändrar du siffran som inte är 5 till intervallen du vill ge pengar på
		division = counter/(50*1); // counter/(Frames * Time Interval)
		
		if(division != previous)
		{
			//Debug.Log("resources delivered");
			DeliverChunk();
		}
		
		previous = division;
		
		counter++;
		
	}
	
	private void DeliverChunk()
	{
		Bank.stone = Bank.stone + 20;
	}

    void FixedUpdate()
    {
		DeliveryInterval();
	
    }
}

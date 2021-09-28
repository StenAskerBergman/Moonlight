using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class GetResourceData : MonoBehaviour
{
	//public TextMesh label;
	private string strStone;
    private void Awake()
    {
        strStone = Bank.stone.ToString();
        GetComponent<UnityEngine.UI.Text>().text = "Stone : " + strStone;
    }
    
    void Update()
    {
		strStone = Bank.stone.ToString();
		GetComponent<UnityEngine.UI.Text>().text = "Stone : " + strStone; 
    
    }
}

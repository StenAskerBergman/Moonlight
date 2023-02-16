using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class GetResourceData : MonoBehaviour
{
	//public TextMesh label;
  private int Player = 0;

	private string strBM;
    private void Awake()
    {
        strBM = Bank.BM.ToString();
        GetComponent<UnityEngine.UI.Text>().text = strBM;
    }
    
    void Update()
    {
		strBM = Bank.BM.ToString();
		GetComponent<UnityEngine.UI.Text>().text = "" + strBM + ""; 
    
    }
}

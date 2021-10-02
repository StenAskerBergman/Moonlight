using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActivationScript : MonoBehaviour
{

    public GameObject objectToDisable1;
    //public GameObject objectToDisable2;
    public static bool disabled = false;

    /*
     * Allt de här scriptet gör är sätta på ui när man spelar
     * så slipper vi see den i editor vyn
    */
    private void Awake()
    {
      objectToDisable1.SetActive(true);
      //objectToDisable2.SetActive(true);

    }


}
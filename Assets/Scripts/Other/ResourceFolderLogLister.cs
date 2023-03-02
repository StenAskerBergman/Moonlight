using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResourceFolderLogLister : MonoBehaviour
{
    public bool PrintLog = false;

    // Start is called before the first frame update
    void Start()
    {

        if (PrintLog == true){
            Object[] assets = Resources.LoadAll("");
            
            foreach (Object asset in assets) 
            {
                Debug.Log(asset.name);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        

    }
}

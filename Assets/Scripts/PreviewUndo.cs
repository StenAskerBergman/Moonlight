using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PreviewUndo : MonoBehaviour
{

    public void UndoPreview(GameObject buildingPreview) 
    {
        if (buildingPreview != null)
        {
            Debug.Log($"Destroying: {buildingPreview.name} - Is asset? {buildingPreview.scene.rootCount == 0}");
            Destroy(buildingPreview);
        }
    }
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

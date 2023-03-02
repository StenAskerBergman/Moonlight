using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SelectionSystem : MonoBehaviour
{
	public GameObject selected = null;
	RaycastHit hit;
	public static bool placing;
    public Material selectedIndicatorColor;
    public Material originalMaterial;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition); // Ray
        
		if (placing == false  && Input.GetMouseButton(0) && Physics.Raycast(ray, out hit) && hit.transform.gameObject.tag != "Ground")
		{
            
            if (selected != null)
            {
                selected.GetComponent<MeshRenderer>().material = originalMaterial;
            }
            
				selected = hit.transform.gameObject;
                
                originalMaterial = selected.GetComponent<MeshRenderer>().material;

                selected.GetComponent<MeshRenderer>().material = selectedIndicatorColor;

                Debug.Log(selected);

        }
    }
}

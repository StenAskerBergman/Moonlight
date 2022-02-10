using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class UnitMovement : MonoBehaviour
{
    // Responsibilities: Unit Moving

    public Camera cam;
    public NavMeshAgent agent;
    public LayerMask ground;

    void Start()
    {
        cam = Camera.main;
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            //Debug.Log("UnitMovement: Moused Button Down");
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, ground))
            {
                //Debug.Log("UnitMovement: Moused Button Clicked");
                agent.SetDestination(hit.point);

                //Debug.Log("UnitMovement: " + hit.point);
            }

            //Vector3 UnitPosition = transform.position;
        }
    }
}

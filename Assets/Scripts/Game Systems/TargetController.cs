using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class TargetController : MonoBehaviour
{
    public Camera cam;
    public NavMeshAgent agent;

    void Update()
    {
        if (Input.GetMouseButton(1) || Input.GetMouseButton(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {

                // Move our Agent
                agent.SetDestination(hit.point);

            }

        }

    }
}

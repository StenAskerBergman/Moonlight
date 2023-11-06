using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingRotator : MonoBehaviour
{
    [SerializeField] private int rotationAngleAmount = 90;
    [SerializeField] public static bool rotationMode = true; // Determines Rotation Mode
    private float placementRotation; // Building Placement Rotation

    private void Update()
    {
        RotateBuilding();
    }

    private void RotateBuilding()
    {
        
        // Rotational System -- Flat Based: Quadral 90° - Newer
        float scrollDirection = Input.GetAxis("Mouse ScrollWheel") * 10.0f; // Scaling factor

        if (scrollDirection > 0)
        {
            placementRotation += Mathf.FloorToInt(scrollDirection) * rotationAngleAmount;
        }
        else if (scrollDirection < 0)
        {
            placementRotation -= Mathf.FloorToInt(Mathf.Abs(scrollDirection)) * rotationAngleAmount;
        }

        transform.rotation = Quaternion.AngleAxis(placementRotation, Vector3.down);
    }
}


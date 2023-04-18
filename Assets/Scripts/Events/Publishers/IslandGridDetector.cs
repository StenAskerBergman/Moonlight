using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class IslandGridDetector : MonoBehaviour
{
    // Declare a delegate and an event for GridSystem detection
    public delegate void GridSystemDetectedEventHandler(GridSystem gridSystem);
    public event GridSystemDetectedEventHandler OnGridSystemDetected;

    [SerializeField] private LayerMask islandLayer;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float maxRaycastDistance = 100f;

    private void Update()
    {
        DetectGridSystemWithMouse();
    }

    private void DetectGridSystemWithMouse()
    {
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            Ray mray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit mhit;

            if (Physics.Raycast(mray, out mhit, maxRaycastDistance, islandLayer))
            {
                GridSystem gridSystem = mhit.collider.GetComponentInParent<GridSystem>();

                if (gridSystem != null)
                {
                    // Debug.Log($"GridSystem detected on island: {gridSystem.name}");
                    // Invoke the OnGridSystemDetected event with the detected GridSystem
                    OnGridSystemDetected?.Invoke(gridSystem);
                }
            }
        }
    }
}

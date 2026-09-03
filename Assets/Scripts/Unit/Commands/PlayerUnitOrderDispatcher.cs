using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

/// <summary>
/// Centralized dispatcher for player unit movement and interaction commands.
/// Captures Right-Click and Shift + Right-Click, projects targets onto the NavMesh,
/// and issues MoveCommands to selected units' UnitCommandExecutors.
/// Replaces individual input polling inside UnitMovement.
/// </summary>
public class PlayerUnitOrderDispatcher : MonoBehaviour
{
    private static PlayerUnitOrderDispatcher _instance;
    public static PlayerUnitOrderDispatcher Instance => _instance;

    private Camera mainCamera;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            HandleRightClickOrder();
        }
    }

    private void HandleRightClickOrder()
    {
        // Ignore clicks over UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (UnitSelections.Instance == null || UnitSelections.Instance.unitsSelected == null)
        {
            return;
        }

        var selectedUnits = UnitSelections.Instance.unitsSelected;
        if (selectedUnits.Count == 0) return;

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Determine travel medium from the first selected unit
        LayerMask travelMedium = LayerMask.GetMask("Water", "Default", "Ground");
        var firstUnit = selectedUnits[0];
        if (firstUnit != null)
        {
            var movement = firstUnit.GetComponent<UnitMovement>();
            if (movement != null && movement.TravelMedium.value != 0)
            {
                travelMedium = movement.TravelMedium;
            }
        }

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, travelMedium, QueryTriggerInteraction.Ignore))
        {
            bool queue = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            foreach (var unit in selectedUnits)
            {
                if (unit == null) continue;

                var agent = unit.GetComponent<NavMeshAgent>();
                if (agent == null) continue;

                var filter = new NavMeshQueryFilter
                {
                    agentTypeID = agent.agentTypeID,
                    areaMask = agent.areaMask
                };

                NavMeshHit navHit;
                if (NavMesh.SamplePosition(hit.point, out navHit, 1.0f, filter) ||
                    NavMesh.SamplePosition(hit.point, out navHit, 500f, filter))
                {
                    Vector3 validPoint = navHit.position;

                    var executor = unit.GetComponent<UnitCommandExecutor>();
                    if (executor == null)
                    {
                        executor = unit.gameObject.AddComponent<UnitCommandExecutor>();
                    }

                    executor.IssueCommand(new MoveCommand(validPoint), queue: queue, isPlayerOrder: true);
                }
            }
        }
    }
}

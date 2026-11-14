using UnityEngine;
using UnityEngine.EventSystems;
// using static UnityEngine.Rendering.DebugUI.Table;
// using UnityEngine.UIElements.Experimental;

public class UnitDrag : MonoBehaviour
{
    // WATTADO:
    // this script draws the visual selection square and how big it will be
    // it also draws the invisable selection box and adds the selected units
    // to the selection system, that is all this script does

    private Camera myCam;
    public bool isHolding;
    private bool pointerStartedOverUI;

    //Graphical
    [SerializeField]
    RectTransform boxVisual;

    //Logical
    Rect selectionBox;

    Vector2 startPosition;
    Vector2 endPosition;

    void Start()
    {
        myCam = Camera.main;
        startPosition = Vector2.zero;
        endPosition = Vector2.zero;
        DrawVisual();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            pointerStartedOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (pointerStartedOverUI || isHolding)
            {
                CancelCurrentGesture();
                return;
            }

            startPosition = Input.mousePosition;
            endPosition = startPosition;
            selectionBox = new Rect();
        }

        // A world-selection gesture must never begin after a pointer-down on UI.
        // Keep the suppression latched until release; item dragging starts only
        // after Unity's drag threshold, which is too late to guard mouse-down.
        if (pointerStartedOverUI || isHolding)
        {
            if (Input.GetMouseButtonUp(0))
            {
                pointerStartedOverUI = false;
                isHolding = false;
                CancelCurrentGesture();
            }
            return;
        }

        // When dragging
        if (Input.GetMouseButton(0))
        {
            endPosition = Input.mousePosition;
            DrawVisual();
            DrawSelection();
        }

        // When released click
        if (Input.GetMouseButtonUp(0))
        {
            try
            {
                endPosition = Input.mousePosition;
                DrawSelection();
                SelectUnits();
            }
            finally
            {
                CancelCurrentGesture();
            }
        }
    }

    public void SuppressCurrentGesture()
    {
        pointerStartedOverUI = true;
        CancelCurrentGesture();
    }

    private void CancelCurrentGesture()
    {
        startPosition = Vector2.zero;
        endPosition = Vector2.zero;
        selectionBox = new Rect();
        DrawVisual();
    }

    void DrawVisual()
    {
        if (boxVisual == null) return;

        Vector2 boxStart = startPosition;
        Vector2 boxEnd = endPosition;

        Vector2 boxSize = new Vector2(Mathf.Abs(boxStart.x - boxEnd.x), Mathf.Abs(boxStart.y - boxEnd.y));
        bool isDragging = boxSize.sqrMagnitude > 16f;

        if (boxVisual.gameObject.activeSelf != isDragging)
        {
            boxVisual.gameObject.SetActive(isDragging);
        }

        if (!isDragging) return;

        var img = boxVisual.GetComponent<UnityEngine.UI.Image>();
        if (img != null && !img.enabled)
        {
            img.enabled = true;
        }

        Vector2 boxCenter = (boxStart + boxEnd) / 2f;
        boxVisual.position = boxCenter;
        boxVisual.sizeDelta = boxSize;
    }

    void DrawSelection()
    {
        selectionBox = Rect.MinMaxRect(
            Mathf.Min(startPosition.x, endPosition.x),
            Mathf.Min(startPosition.y, endPosition.y),
            Mathf.Max(startPosition.x, endPosition.x),
            Mathf.Max(startPosition.y, endPosition.y)
        );
    }

    void SelectUnits()
    {
        if (UnitSelections.Instance == null) return;
        if (myCam == null) myCam = Camera.main;
        if (myCam == null) return;

        // Require dragging past a small threshold (6 pixels) so a single click is handled by UnitClick
        if (Vector2.Distance(startPosition, endPosition) < 6f)
        {
            return;
        }

        // Loop through all the registered units
        var unitList = UnitSelections.Instance.unitList;

        // Reverse index loop so destroyed entries can be dropped while iterating.
        for (int i = unitList.Count - 1; i >= 0; i--)
        {
            Unit unit = unitList[i];

            // Destroyed Units compare equal to null; drop them rather than
            // dereferencing .transform, which would throw and abort the sweep.
            if (unit == null)
            {
                unitList.RemoveAt(i);
                continue;
            }

            Vector3 screenPos = myCam.WorldToScreenPoint(unit.transform.position);
            if (screenPos.z > 0 && selectionBox.Contains(screenPos))
            {
                UnitSelections.Instance.DragSelect(unit);
            }
        }
    }
}

using UnityEngine;
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
        if (!isHolding)
        {     
            //When Clicked
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = myCam.ScreenPointToRay(Input.mousePosition);

                startPosition = Input.mousePosition;
                selectionBox = new Rect();
            }

            //when dragging
            if (Input.GetMouseButton(0))
            {
                endPosition = Input.mousePosition;
                DrawVisual();
                DrawSelection();
            }

            //When released click
            if (Input.GetMouseButtonUp(0))
            {
                // The box is presentation state and must always be cleared. If anything
                // inside SelectUnits() throws, skipping these three lines leaves the
                // selection rectangle frozen on screen for the rest of the session.
                try
                {
                    SelectUnits();
                }
                finally
                {
                    startPosition = Vector2.zero;
                    endPosition = Vector2.zero;
                    DrawVisual();
                }
            }
        }
        else if(isHolding)
        {
            // IsHolding, aka don't do anything
            return;
        }
    }

    void DrawVisual()
    {
        Vector2 boxStart = startPosition;
        Vector2 boxEnd = endPosition;

        Vector2 boxCenter = (boxStart + boxEnd) / 2;
        boxVisual.position = boxCenter;

        Vector2 boxSize = new Vector2(Mathf.Abs(boxStart.x - boxEnd.x), Mathf.Abs(boxStart.y - boxEnd.y));
        boxVisual.sizeDelta = boxSize;

    }

    void DrawSelection()
    {
        if(Input.mousePosition.x < startPosition.x)  
        {
            selectionBox.xMin = Input.mousePosition.x;
            selectionBox.xMax = startPosition.x;
        }
        else
        {
            selectionBox.xMin = startPosition.x;
            selectionBox.xMax = Input.mousePosition.x;
        }

        if(Input.mousePosition.y < startPosition.y)
        {
            selectionBox.yMin = Input.mousePosition.y;
            selectionBox.yMax = startPosition.y;
        }
        else 
        {
            selectionBox.yMin = startPosition.y;
            selectionBox.yMax = Input.mousePosition.y;
        }
    }

    void SelectUnits()
    {
        //loop thru all the units
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

            if (selectionBox.Contains(myCam.WorldToScreenPoint(unit.transform.position)))
            {
                UnitSelections.Instance.DragSelect(unit);
            }
        }
    }
}

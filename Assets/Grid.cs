using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CodeMonkey.Utils;
public class Grid {

    // Declare class variables
    int width;
    int height;
    float cellSize;
    Vector3 originPosition;
    int[,] gridArray; // Multi Dimensional Array
    TextMesh[,] debugTextArray;

    public Grid(int width, int height, float cellSize, Vector3 originPosition) {
        // Assign passed values to class variables
        this.width = width;
        this.height = height;
        this.cellSize = cellSize;
        this.originPosition = originPosition;

        gridArray = new int[width, height];
        debugTextArray = new TextMesh[width, height];

        // Log the width and height
        //Debug.Log(width + " " + height);
        
        // Loop through all cells in the grid
        for(int x = 0; x < gridArray.GetLength(0); x++){
            for (int y = 0; y < gridArray.GetLength(1); y++){
                debugTextArray[x, y] = UtilsClass.CreateWorldText(gridArray[x, y].ToString(), null, GetWorldPosition(x, y) + new Vector3(cellSize, cellSize) * .5f, 20, Color.white, TextAnchor.MiddleCenter);
                // Draw a line in the world from the current cell to the next cell in the x direction
                Debug.DrawLine(GetWorldPosition(x, y), GetWorldPosition(x, y + 1), Color.white, 100f); // horizontal line
                // Draw a line in the world from the current cell to the next cell in the y direction
                Debug.DrawLine(GetWorldPosition(x, y), GetWorldPosition(x + 1, y), Color.white, 100f); // Vertical line
            }
        }
        // Draw the bottom and right border lines of the grid
        Debug.DrawLine(GetWorldPosition(0, height), GetWorldPosition(width, height), Color.white, 100f);
        Debug.DrawLine(GetWorldPosition(width, 0), GetWorldPosition(width, height), Color.white, 100f);
        
    }

    Vector3 GetWorldPosition(int x, int y) {
        return new Vector3(x, y) * cellSize + originPosition;
    }
    
    // Returns the world position of the cell at (x, y)
    private void GetXY(Vector3 WorldPosition, out int x, out int y){
        x = Mathf.FloorToInt((WorldPosition - originPosition).x / cellSize);
        y = Mathf.FloorToInt((WorldPosition - originPosition).y / cellSize);
    }

    
    // Only set the value if (x, y) is within the bounds of the grid
    public void SetValue(int x, int y, int value){
        // Only set the value if (x, y) is within the bounds of the grid
        if(x >= 0 && y >= 0 && x < width && y < height){
            gridArray[x, y] = value;
            debugTextArray[x,y].text = gridArray[x, y].ToString();
        }
    }
    
    public void SetValue(Vector3 worldPosition, int value) {
      int x, y;  
        GetXY(worldPosition, out x, out y);
        SetValue(x, y, value);
        Debug.Log("Value Set");
    }

    public int GetValue(int x, int y){
        // Only set the value if (x, y) is within the bounds of the grid
        if(x >= 0 && y >= 0 && x < width && y < height){
            return gridArray[x, y];
        } else {
            return 0;
        }
    }

    public int GetValue(Vector3 worldPosition) {
      int x, y;  
        GetXY(worldPosition, out x, out y);
        return GetValue(x, y);
    }

}

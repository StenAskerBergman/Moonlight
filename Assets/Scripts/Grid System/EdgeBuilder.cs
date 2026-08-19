using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Cell;

public class EdgeBuilder : MonoBehaviour
{
    private Cell[,] grid;

    public EdgeBuilder(Cell[,] grid)
    {
        this.grid = grid;
    }

}

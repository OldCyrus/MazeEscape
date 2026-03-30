using UnityEngine;
using System.Collections.Generic;

namespace MazegeneratorPro
{
    public class MazeData
    {
        public bool[,] horizontalWalls; // between rows
        public bool[,] verticalWalls;   // between columns
        public int rows;
        public int cols;

        // Each RectInt stores the top-left cell (x=col, y=row) and size of each carved room
        public List<RectInt> rooms = new List<RectInt>();
    }
}
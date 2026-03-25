using UnityEngine;

namespace MazegeneratorPro
{
    public class MazeData
    {
        public bool[,] horizontalWalls; // between rows
        public bool[,] verticalWalls;   // between columns
        public int rows;
        public int cols;
    }
}


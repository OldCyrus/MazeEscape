using System.Collections.Generic;
using UnityEngine;

namespace MazegeneratorPro
{
    public class MazeGenerator : MazeGeneratorBase
    {
        private System.Random rand;
        private bool[,] visited;
        private MazeData maze;

        public MazeGenerator(int rows, int cols, int seed) : base(rows, cols, seed) { }

        public override MazeData GenerateMaze()
        {
            rand = seed == 0 ? new System.Random() : new System.Random(seed);
            visited = new bool[rows, cols];

            maze = new MazeData
            {
                rows = rows,
                cols = cols,
                horizontalWalls = new bool[rows + 1, cols],
                verticalWalls = new bool[rows, cols + 1]
            };

            // Initialize all walls
            for (int r = 0; r < rows + 1; r++)
                for (int c = 0; c < cols; c++)
                    maze.horizontalWalls[r, c] = true;

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols + 1; c++)
                    maze.verticalWalls[r, c] = true;

            // Start DFS
            DFS(0, 0);

            return maze;
        }

        private void DFS(int r, int c)
        {
            visited[r, c] = true;
            List<Vector2Int> dirs = new List<Vector2Int>
        {
            new Vector2Int(0,1),
            new Vector2Int(1,0),
            new Vector2Int(0,-1),
            new Vector2Int(-1,0)
        };

            Shuffle(dirs);

            foreach (var d in dirs)
            {
                int nr = r + d.x;
                int nc = c + d.y;

                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols && !visited[nr, nc])
                {
                    if (d.x == 0 && d.y == 1) // right
                        maze.verticalWalls[r, c + 1] = false;
                    else if (d.x == 0 && d.y == -1) // left
                        maze.verticalWalls[r, c] = false;
                    else if (d.x == 1 && d.y == 0) // down
                        maze.horizontalWalls[r + 1, c] = false;
                    else if (d.x == -1 && d.y == 0) // up
                        maze.horizontalWalls[r, c] = false;

                    DFS(nr, nc);
                }
            }
        }

        private void Shuffle(List<Vector2Int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rand.Next(i + 1);
                var temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }
    }
}

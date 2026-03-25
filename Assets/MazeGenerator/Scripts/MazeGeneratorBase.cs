using UnityEngine;

namespace MazegeneratorPro
{
    public abstract class MazeGeneratorBase
    {
        protected int rows, cols;
        protected int seed;

        public MazeGeneratorBase(int rows, int cols, int seed)
        {
            this.rows = rows;
            this.cols = cols;
            this.seed = seed;
        }

        public abstract MazeData GenerateMaze();
    }
}

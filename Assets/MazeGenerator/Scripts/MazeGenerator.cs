using System.Collections.Generic;
using UnityEngine;

namespace MazegeneratorPro
{
    public class MazeGenerator : MazeGeneratorBase
    {
        private System.Random rand;
        private bool[,] visited;
        private bool[,] inRoom;   // cells that belong to a room – DFS will skip these
        private MazeData maze;

        // ---------------------------------------------------------------
        //  Room tuning
        // ---------------------------------------------------------------
        private const int MIN_ROOM_WIDTH  = 3;   // cells wide
        private const int MAX_ROOM_WIDTH  = 5;
        private const int MIN_ROOM_HEIGHT = 3;   // cells tall
        private const int MAX_ROOM_HEIGHT = 5;
        private const int ROOM_ATTEMPTS   = 20;  // raise for more rooms (not all attempts succeed)

        public MazeGenerator(int rows, int cols, int seed) : base(rows, cols, seed) { }

        public override MazeData GenerateMaze()
        {
            rand    = seed == 0 ? new System.Random() : new System.Random(seed);
            visited = new bool[rows, cols];
            inRoom  = new bool[rows, cols];

            maze = new MazeData
            {
                rows = rows,
                cols = cols,
                horizontalWalls = new bool[rows + 1, cols],
                verticalWalls   = new bool[rows, cols + 1]
            };

            // All walls start solid
            for (int r = 0; r <= rows; r++)
                for (int c = 0; c < cols; c++)
                    maze.horizontalWalls[r, c] = true;

            for (int r = 0; r < rows; r++)
                for (int c = 0; c <= cols; c++)
                    maze.verticalWalls[r, c] = true;

            // 1. Reserve room footprints BEFORE running DFS so corridors never enter them
            PlaceRooms();

            // 2. Mark every room cell as already-visited so DFS treats them like walls
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (inRoom[r, c])
                        visited[r, c] = true;

            // 3. Carve corridors – DFS will route AROUND room footprints
            DFS(0, 0);

            // 4. Open up room interiors and connect each room to the corridor network
            foreach (var room in maze.rooms)
                FinaliseRoom(room);

            return maze;
        }

        // ---------------------------------------------------------------
        //  Room placement
        // ---------------------------------------------------------------

        private void PlaceRooms()
        {
            // Keep rooms away from the outermost ring of cells so that entrance/exit
            // walls (all three ExitPlacement modes sit on the outer edge) are safe.
            const int margin = 1;

            for (int attempt = 0; attempt < ROOM_ATTEMPTS; attempt++)
            {
                int rw = rand.Next(MIN_ROOM_WIDTH,  MAX_ROOM_WIDTH  + 1);
                int rh = rand.Next(MIN_ROOM_HEIGHT, MAX_ROOM_HEIGHT + 1);

                int maxStartCol = cols - rw - margin;
                int maxStartRow = rows - rh - margin;

                if (maxStartCol < margin || maxStartRow < margin)
                    continue;

                int startCol = rand.Next(margin, maxStartCol + 1);
                int startRow = rand.Next(margin, maxStartRow + 1);

                var candidate = new RectInt(startCol, startRow, rw, rh);

                // Require a 1-cell gap between rooms so corridors can pass between them
                if (OverlapsExistingRoom(candidate))
                    continue;

                // Stamp room cells
                for (int r = candidate.y; r < candidate.y + candidate.height; r++)
                    for (int c = candidate.x; c < candidate.x + candidate.width; c++)
                        inRoom[r, c] = true;

                maze.rooms.Add(candidate);
            }
        }

        private bool OverlapsExistingRoom(RectInt candidate)
        {
            foreach (var room in maze.rooms)
            {
                var padded = new RectInt(room.x - 1, room.y - 1,
                                        room.width + 2, room.height + 2);
                if (padded.Overlaps(candidate))
                    return true;
            }
            return false;
        }

        // ---------------------------------------------------------------
        //  Room finalisation (called after DFS)
        // ---------------------------------------------------------------

        /// <summary>
        /// 1. Removes every wall inside the room so it is a completely open space.
        /// 2. Connects the room to the corridor network through exactly one doorway.
        ///    A doorway is a border wall that sits between a room cell and a
        ///    non-room cell that DFS has already visited (i.e. a real corridor cell).
        /// </summary>
        private void FinaliseRoom(RectInt room)
        {
            int c0 = room.x;
            int r0 = room.y;
            int c1 = c0 + room.width  - 1;
            int r1 = r0 + room.height - 1;

            // --- Remove all interior walls ---

            // Interior horizontal walls (between rows inside the room)
            for (int r = r0 + 1; r <= r1; r++)
                for (int c = c0; c <= c1; c++)
                    maze.horizontalWalls[r, c] = false;

            // Interior vertical walls
            for (int r = r0; r <= r1; r++)
                for (int c = c0 + 1; c <= c1; c++)
                    maze.verticalWalls[r, c] = false;

            // --- Find all candidate doorway positions ---
            // Each entry is (wallRow, wallCol, isHorizontal)
            var doorways = new List<(int wr, int wc, bool horiz)>();

            // Top border: horizontal wall at [r0, c] — corridor cell is at [r0-1, c]
            for (int c = c0; c <= c1; c++)
                if (r0 > 0 && !inRoom[r0 - 1, c] && visited[r0 - 1, c])
                    doorways.Add((r0, c, true));

            // Bottom border: horizontal wall at [r1+1, c] — corridor cell is at [r1+1, c]
            for (int c = c0; c <= c1; c++)
                if (r1 + 1 < rows && !inRoom[r1 + 1, c] && visited[r1 + 1, c])
                    doorways.Add((r1 + 1, c, true));

            // Left border: vertical wall at [r, c0] — corridor cell is at [r, c0-1]
            for (int r = r0; r <= r1; r++)
                if (c0 > 0 && !inRoom[r, c0 - 1] && visited[r, c0 - 1])
                    doorways.Add((r, c0, false));

            // Right border: vertical wall at [r, c1+1] — corridor cell is at [r, c1+1]
            for (int r = r0; r <= r1; r++)
                if (c1 + 1 < cols && !inRoom[r, c1 + 1] && visited[r, c1 + 1])
                    doorways.Add((r, c1 + 1, false));

            if (doorways.Count == 0)
            {
                // No corridor reached the room border – force open any non-outer-edge wall
                ForceDoorway(room);
                return;
            }

            // Pick one at random and open it
            var pick = doorways[rand.Next(doorways.Count)];
            if (pick.horiz)
                maze.horizontalWalls[pick.wr, pick.wc] = false;
            else
                maze.verticalWalls[pick.wr, pick.wc] = false;
        }

        /// <summary>
        /// Fallback: open any non-outer-boundary wall on the room's perimeter.
        /// </summary>
        private void ForceDoorway(RectInt room)
        {
            int c0 = room.x, r0 = room.y;
            int c1 = c0 + room.width - 1;
            int r1 = r0 + room.height - 1;

            if (r0 > 0)        { maze.horizontalWalls[r0,     c0 + rand.Next(room.width)]  = false; return; }
            if (r1 + 1 < rows) { maze.horizontalWalls[r1 + 1, c0 + rand.Next(room.width)]  = false; return; }
            if (c0 > 0)        { maze.verticalWalls[r0 + rand.Next(room.height), c0]        = false; return; }
            if (c1 + 1 < cols) { maze.verticalWalls[r0 + rand.Next(room.height), c1 + 1]   = false; }
        }

        // ---------------------------------------------------------------
        //  DFS corridor carver (unchanged logic, respects visited[])
        // ---------------------------------------------------------------

        private void DFS(int r, int c)
        {
            visited[r, c] = true;

            var dirs = new List<Vector2Int>
            {
                new Vector2Int( 0,  1),
                new Vector2Int( 1,  0),
                new Vector2Int( 0, -1),
                new Vector2Int(-1,  0)
            };
            Shuffle(dirs);

            foreach (var d in dirs)
            {
                int nr = r + d.x;
                int nc = c + d.y;

                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols && !visited[nr, nc])
                {
                    if      (d.x == 0 && d.y ==  1) maze.verticalWalls[r, c + 1]   = false;
                    else if (d.x == 0 && d.y == -1) maze.verticalWalls[r, c]       = false;
                    else if (d.x == 1 && d.y ==  0) maze.horizontalWalls[r + 1, c] = false;
                    else if (d.x ==-1 && d.y ==  0) maze.horizontalWalls[r, c]     = false;

                    DFS(nr, nc);
                }
            }
        }

        // ---------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rand.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
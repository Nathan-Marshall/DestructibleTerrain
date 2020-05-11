using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class CaveGeneration : MonoBehaviour
{
    public int width = 150;
    public int height = 400;

    public int maxHillHeight = 12;
    public float hillPerlinSampleDistance = 0.05f;
    public int minimumCaveDepth = 4;

    public float population = 0.57f;
    public int smoothIterations = 7;
    public string seed;

    private System.Random rand;
    private bool[,] grid;
    private int[] sampleHeights;

    void Start() {
        Generate();
    }

    public void Generate() {
        InitializeSeed();

        sampleHeights = new int[width];
        float sampleStart = (float)rand.NextDouble() * 50000;
        for (int c = 0; c < width; c++) {
            sampleHeights[c] = height - maxHillHeight + Mathf.Clamp((int)(Mathf.PerlinNoise(sampleStart + c * hillPerlinSampleDistance, 0) * (maxHillHeight + 1)), 0, maxHillHeight);
        }

        // Create grid with random tiles based on the population probability
        grid = new bool[width, height];
        for (int c = 0; c < width; c++) {
            for (int r = 0; r < height; r++) {
                if (IsGuaranteedTrue(c, r)) {
                    grid[c, r] = true;
                }
                else if (IsGuaranteedFalse(c, r)) {
                    grid[c, r] = false;
                }
                else {
                    grid[c, r] = rand.NextDouble() < population;
                }
            }
        }

        for (int iteration = 0; iteration < smoothIterations; iteration++) {
            bool[,] newGrid = new bool[width, height];
            for (int c = 0; c < width; c++) {
                for (int r = 0; r < height; r++) {
                    int neighbours = CountNeighbours(c, r);
                    if (IsGuaranteedTrue(c, r)) {
                        newGrid[c, r] = true;
                    }
                    else if (IsGuaranteedFalse(c, r)) {
                        newGrid[c, r] = false;
                    }
                    else if (neighbours > 4) {
                        newGrid[c, r] = true;
                    }
                    else if (neighbours < 4) {
                        newGrid[c, r] = false;
                    }
                    else {
                        newGrid[c, r] = grid[c, r];
                    }
                }
            }
            grid = newGrid;
        }

        // Final pass to remove lone pixels. We can modify the grid directly and don't need to check guaranteed pixels for this pass.
        for (int c = 0; c < width; c++) {
            for (int r = 0; r < height; r++) {
                int neighbours = CountNeighbours(c, r);
                if (neighbours == 8) {
                    grid[c, r] = true;
                }
                else if (neighbours == 0) {
                    grid[c, r] = false;
                }
            }
        }

        GeneratePixelRenderMesh();
    }

    // Initialize random seed
    private void InitializeSeed() {
        if (seed == "") {
            rand = new System.Random();
        }
        else {
            int seedInt = 0;
            foreach (char c in seed) {
                seedInt += c;
            }
            rand = new System.Random(seedInt);
        }
    }

    private bool InGridBounds(int c, int r) {
        return c >= 0 && c < width && r >= 0 && r < height;
    }

    private bool InTerrainBounds(int c, int r) {
        return c >= 0 && c < width && r >= 0 && r < sampleHeights[c];
    }

    private bool GetCell(int c, int r) {
        return InGridBounds(c, r) && grid[c, r];
    }

    private int CountNeighbours(int centerCol, int centerRow) {
        int neighbours = 0;
        for (int c = centerCol - 1; c <= centerCol + 1; c++) {
            for (int r = centerRow - 1; r <= centerRow + 1; r++) {
                if ((c != centerCol || r != centerRow) && GetCell(c, r)) {
                    neighbours++;
                }
            }
        }
        return neighbours;
    }

    private bool IsGuaranteedTrue(int c, int r) {
        return (c == 0 || c == width - 1 || r == 0 || r >= sampleHeights[c] - minimumCaveDepth) && r < sampleHeights[c];
    }

    private bool IsGuaranteedFalse(int c, int r) {
        return r >= sampleHeights[c];
    }

    private void GeneratePixelRenderMesh() {
        Vector3[] vertices = new Vector3[(width + 1) * (height + 1)];
        for (int c = 0; c <= width; c++) {
            for (int r = 0; r <= height; r++) {
                vertices[c + r * (width + 1)] = new Vector3(c - width * 0.5f, r - height, 0);
            }
        }

        List<int> triangles = new List<int>();
        for (int c = 0; c < width; c++) {
            for (int r = 0; r < height; r++) {
                if (grid[c, r]) {
                    int bottomLeft = c + r * (width + 1);
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + (width + 1);
                    int topRight = topLeft + 1;

                    triangles.Add(topLeft);
                    triangles.Add(topRight);
                    triangles.Add(bottomLeft);
                    triangles.Add(bottomLeft);
                    triangles.Add(topRight);
                    triangles.Add(bottomRight);
                }
            }
        }

        MeshFilter mf = GetComponent<MeshFilter>();
        mf.sharedMesh = new Mesh() {
            vertices = vertices,
            triangles = triangles.ToArray()
        };
    }

    private struct IntPoint {
        public int x;
        public int y;

        public IntPoint(int x, int y) {
            this.x = x;
            this.y = y;
        }
    }

    private class PartialPolygon
    {
        // These two lists act as a sort of double-ended queue of all the points added to the polygon so far
        private List<IntPoint> bottomPoints;
        private List<IntPoint> topPoints;

        private int column;
        private int bottomRow;
        private int topRow;

        // Return an IEnumerable of all points
        public IEnumerable<IntPoint> Points {
            get { return bottomPoints.AsEnumerable().Reverse().Concat(topPoints); }
        }

        public PartialPolygon(ColumnContourBoundaries firstCol) {
            bottomRow = firstCol.Boundaries[0];
            topRow = firstCol.Boundaries[firstCol.Boundaries.Count - 1] - 1;

            column = firstCol.Column;
            bottomPoints.Add(new IntPoint(firstCol.Column, bottomRow));
            for (int r = bottomRow + 1; r <= topRow; r++) {
                topPoints.Add(new IntPoint(firstCol.Column, r));
            }
        }

        public void MergeColumn(ColumnContourBoundaries ccb) {
            for (int r = bottomRow - 1; r <= topRow + 1; r++) {
                if ()
            }
        }
    }

    private class ColumnContourBoundaries
    {
        public int Column { get; private set; }
        public List<int> Boundaries { get; private set; }
        public bool IsFirstSolid { get; private set; }

        // All boundaries except the last are starts
        public IEnumerable<int> Starts {
            get { return Boundaries.Where(i => i < Boundaries.Count - 1); }
        }

        // All starts of solid segments
        public IEnumerable<int> SolidStarts {
            get { return Starts.Where(i => IsSolid(i)); }
        }

        // All starts of hole segments
        public IEnumerable<int> HoleStarts {
            get { return Starts.Where(i => !IsSolid(i)); }
        }

        // Just before all boundaries except the first
        public IEnumerable<int> Ends {
            get { return Boundaries.Where(i => i > 0).Select(b => b - 1); }
        }

        // All ends of solid segments
        public IEnumerable<int> SolidEnds {
            get { return Ends.Where(i => IsSolid(i)); }
        }

        // All ends of hole segments
        public IEnumerable<int> HoleEnds {
            get { return Ends.Where(i => !IsSolid(i)); }
        }

        public bool IsSolid(int index) {
            // XOR operator: returns true if either even indices are solid or this index is odd, but not both
            return IsFirstSolid ^ index % 2 == 1; 
        }

        public ColumnContourBoundaries(int col, IEnumerable<int> boundaries, bool isFirstSolid) {
            Column = col;
            Boundaries = boundaries.ToList();
            IsFirstSolid = isFirstSolid;
        }
    }

    private void DetectHoleContours() {
        PartialPolygon terrainPolygon;

        for (int c = 1; c < width - 1; c++) {
            List<int> boundaries = new List<int>() { 0 };
            for (int r = 1; r < sampleHeights[c] - minimumCaveDepth; r++) {
                if (grid[c, r] != grid[c, r - 1]) {
                    boundaries.Add(r);
                }
            }
            boundaries.Add(sampleHeights[c]);
            boundaries.Add(height);
            terrainPolygon.MergeColumn(new ColumnContourBoundaries(c, boundaries, true));
        }
    }
}

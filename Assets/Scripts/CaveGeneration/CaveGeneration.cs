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


}

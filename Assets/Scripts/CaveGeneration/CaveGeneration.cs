﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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
    private int[] surfaceHeights;
    private GridIntersection[,] intersectionGrid;

    void Start() {
        Generate();
    }

    public void Generate() {
        InitializeSeed();
        GenerateSurfaceHeights();
        GeneratePixelGrid();
        GenerateIntersectionGrid();
        DetectContours(out List<List<Vector2>> solidContours, out List<List<Vector2>> holeContours);

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

    private void GenerateSurfaceHeights() {
        surfaceHeights = new int[width];
        float sampleStart = (float)rand.NextDouble() * 50000;
        for (int c = 0; c < width; c++) {
            surfaceHeights[c] = height - maxHillHeight + Mathf.Clamp((int)(Mathf.PerlinNoise(sampleStart + c * hillPerlinSampleDistance, 0) * (maxHillHeight + 1)), 0, maxHillHeight);
        }
    }

    private void GeneratePixelGrid() {
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
                    else if (neighbours < 4 || !SolidWithCardinalNeighbour(c, r)) {
                        newGrid[c, r] = false;
                    }
                    else {
                        newGrid[c, r] = grid[c, r];
                    }
                }
            }
            grid = newGrid;
        }

        // Additional passes to remove solid cells that don't have a neighbour in any of the 4 cardinal directions
        int removedCount;
        do {
            removedCount = 0;

            bool[,] newGrid = new bool[width, height];
            for (int c = 0; c < width; c++) {
                for (int r = 0; r < height; r++) {
                    newGrid[c, r] = SolidWithCardinalNeighbour(c, r);
                    if (grid[c, r] != newGrid[c, r]) {
                        removedCount++;
                    }
                }
            }
            grid = newGrid;

        } while (removedCount > 0);

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
    }

    private bool InGridBounds(int c, int r) {
        return c >= 0 && c < width && r >= 0 && r < height;
    }

    private bool IntersectionInBounds(int c, int r) {
        return c >= 0 && c <= width && r >= 0 && r <= height;
    }

    private bool GetCell(int c, int r) {
        return InGridBounds(c, r) && grid[c, r];
    }

    private GridIntersection GetIntersection(int c, int r) {
        return IntersectionInBounds(c, r) ? intersectionGrid[c, r] : null;
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
        return (c == 0 || c == width - 1 || r == 0 || r >= surfaceHeights[c] - minimumCaveDepth) && r < surfaceHeights[c];
    }

    private bool IsGuaranteedFalse(int c, int r) {
        return r >= surfaceHeights[c];
    }

    private bool SolidWithCardinalNeighbour(int c, int r) {
        return GetCell(c, r) && (GetCell(c - 1, r) || GetCell(c + 1, r) || GetCell(c, r - 1) || GetCell(c, r + 1));
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

        public Vector2 ToVec() {
            return new Vector2(x, y);
        }
    }

    private struct WalkInfo {
        public GridIntersection destination;
        public Vector2 cornerPosition;
        public int turnDirection; // -1: CCW, 0: straight, +1: CW

        public WalkInfo(GridIntersection destination, Vector2 cornerPosition, int turnDirection) {
            this.destination = destination;
            this.cornerPosition = cornerPosition;
            this.turnDirection = turnDirection;
        }
    };

    // Contains information about how to walk along edges to generate contours, and which corners are shifted according
    // to the rules for simplified voronoi
    private class GridIntersection {
        public IntPoint pos;

        // Maps the source node to a tuple containing the destination node, whether the corner is shifted,
        // and the corner position
        public Dictionary<GridIntersection, WalkInfo> contourWalkMap;
    };

    private void InitGridIntersection(int col, int row) {
        GridIntersection intersection = intersectionGrid[col, row];

        intersection.pos = new IntPoint(col, row);
        var p = intersection.pos.ToVec();
        var map = intersection.contourWalkMap = new Dictionary<GridIntersection, WalkInfo>();

        // Get the 4 adjacent intersections
        GridIntersection left = GetIntersection(col - 1, row);
        GridIntersection bottom = GetIntersection(col, row - 1);
        GridIntersection right = GetIntersection(col + 1, row);
        GridIntersection top = GetIntersection(col, row + 1);

        // Get the 4 surrounding nodes that meet at this intersection
        bool lb = GetCell(col - 1, row - 1);
        bool rb = GetCell(col, row - 1);
        bool lt = GetCell(col - 1, row);
        bool rt = GetCell(col, row);

        // "/" solid diagonal
        if (lb && rt && !rb && !lt) {
            map[left] = new WalkInfo(top, p + new Vector2(-0.25f, +0.25f), -1);
            map[right] = new WalkInfo(bottom, p + new Vector2(+0.25f, -0.25f), -1);
        }
        // "\" solid diagonal
        else if (rb && lt && !lb && !rt) {
            map[top] = new WalkInfo(right, p + new Vector2(+0.25f, +0.25f), -1);
            map[bottom] = new WalkInfo(left, p + new Vector2(-0.25f, -0.25f), -1);
        }
        // Bottom left is disconnected from the rest
        else if (lb != rb && lb != lt && lb != rt) {
            Vector2 cornerPoint = p + new Vector2(-0.25f, -0.25f);
            if (lb) {
                // Bottom left is solid
                map[left] = new WalkInfo(bottom, cornerPoint, 1);
            }
            else {
                // The rest are solid
                map[bottom] = new WalkInfo(left, cornerPoint, -1);
            }
        }
        // Bottom right is disconnected from the rest
        else if (rb != lb && rb != rt && rb != lt) {
            Vector2 cornerPoint = p + new Vector2(+0.25f, -0.25f);
            if (rb) {
                // Bottom right is solid
                map[bottom] = new WalkInfo(right, cornerPoint, 1);
            }
            else {
                // The rest are solid
                map[right] = new WalkInfo(bottom, cornerPoint, -1);
            }
        }
        // Top left is disconnected from the rest
        else if (lt != rt && lt != lb && lt != rb) {
            Vector2 cornerPoint = p + new Vector2(-0.25f, +0.25f);
            if (lt) {
                // Top left is solid
                map[top] = new WalkInfo(left, cornerPoint, 1);
            }
            else {
                // The rest are solid
                map[left] = new WalkInfo(top, cornerPoint, -1);
            }
        }
        // Top right is disconnected from the rest
        else if (rt != lt && rt != rb && rt != lb) {
            Vector2 cornerPoint = p + new Vector2(+0.25f, +0.25f);
            if (rt) {
                // Top right is solid
                map[right] = new WalkInfo(top, cornerPoint, 1);
            }
            else {
                // The rest are solid
                map[top] = new WalkInfo(right, cornerPoint, -1);
            }
        }
        // The bottom cells are solid but not the top
        else if (lb && rb && !lt && !rt) {
            map[left] = new WalkInfo(right, p, 0);
        }
        // The top cells are solid but not the bottom
        else if (lt && rt && !lb && !rb) {
            map[right] = new WalkInfo(left, p, 0);
        }
        // The left cells are solid but not the right
        else if (lb && lt && !rb && !rt) {
            map[top] = new WalkInfo(bottom, p, 0);
        }
        // The right cells are solid but not the left
        else if (rb && rt && !lb && !lt) {
            map[bottom] = new WalkInfo(top, p, 0);
        }
    }

    private void GenerateIntersectionGrid() {
        intersectionGrid = new GridIntersection[width + 1, height + 1];

        for (int c = 0; c <= width; c++) {
            for (int r = 0; r <= height; r++) {
                intersectionGrid[c, r] = new GridIntersection();
            }
        }

        for (int c = 0; c <= width; c++) {
            for (int r = 0; r <= height; r++) {
                InitGridIntersection(c, r);
            }
        }
    }

    private void DetectContours(out List<List<Vector2>> solidContours, out List<List<Vector2>> holeContours) {
        solidContours = new List<List<Vector2>>();
        holeContours = new List<List<Vector2>>();
        
        // Begin checking each intersection for remaining contours to walk
        for (int c = 0; c <= width; c++) {
            for (int r = 0; r <= height; r++) {
                GridIntersection start = intersectionGrid[c, r];

                // Exhaust all contours that include this intersection
                while (start.contourWalkMap.Count > 0) {
                    // Create a new polygon
                    List<Vector2> poly = new List<Vector2>();

                    // Set the source node to be any node CW along a contour
                    GridIntersection source = start.contourWalkMap.Keys.First();
                    GridIntersection through = start;

                    int turnCount = 0;
                    do {
                        // Walk to the next neighbor and erase the connection behind us
                        WalkInfo info = through.contourWalkMap[source];
                        through.contourWalkMap.Remove(source);

                        // Add the new corner
                        poly.Add(info.cornerPosition);

                        // Subtracts 1 when turning left, and adds 1 when turning right (0 if straight)
                        turnCount += info.turnDirection;

                        // Update the source and through intersections
                        source = through;
                        through = info.destination;
                    } while (through != start);

                    // If the turn count is 4, then this is a CW contour, which means it is solid
                    if (turnCount == 4) {
                        solidContours.Add(poly);
                    }
                    // If the turn count is -4, then this is a CCW contour, which means it is a hole
                    else if (turnCount == -4) {
                        holeContours.Add(poly);
                    }
                    else {
                        throw new System.Exception("Number of turns in contour was not 4 or -4");
                    }
                }
            }
        }
    }
}
using DestructibleTerrain;
using DestructibleTerrain.Destructible;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor.PackageManager.UI;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class CaveGeneration : MonoBehaviour
{
    public int width = 150;
    public int height = 400;

    public int maxHillHeight = 12;
    public float perlinXScale = 20.0f;
    public float surfaceSampleDistance = 0.2f;
    public int minimumCaveDepth = 4;
    public int minContourPoints = 30;
    public float curveSmoothingFactor = 1.0f;
    public int curveNumSamples = 4;

    public float population = 0.54f;
    public int smoothIterations = 20;
    public string seed;

    private System.Random rand;
    private bool[,] grid;
    private int[] surfaceHeights;
    private GridIntersection[,] intersectionGrid;

    void Start() {
        Generate();
    }

    public void Generate() {
        // Pixel-based cave generation
        InitializeSeed();
        Vector2 sampleStart = new Vector2((float)rand.NextDouble() * 50000, (float)rand.NextDouble() * 50000);
        GenerateSurfaceHeights(sampleStart);
        GeneratePixelGrid();

        //CreatePixelRenderMesh();

        // Detect contours
        ComputeIntersectionGrid();
        DetectContours(out List<List<Vector2>> solidContours, out List<List<Vector2>> holeContours);

        holeContours = PruneHoles(solidContours, holeContours).Select(SmoothPolygon).ToList();
        List<Vector2> realOuterContour = ComputeOuterContour(sampleStart);

        // Construct main terrain object
        DTPolygon mainPolygon = new DTPolygon(realOuterContour, holeContours);
        DestructibleObject mainDTObj = new GameObject("Main Terrain Object").AddComponent<DO_Polygon_Clip_Collide>();
        mainDTObj.ApplyPolygonList(new List<DTPolygon> { mainPolygon });
        mainDTObj.GetComponent<Rigidbody2D>().isKinematic = true;
        mainDTObj.transform.position = new Vector3(-width * 0.5f, -height, 0);

        // Construct additional terrain objects
        for (int i = 1; i < solidContours.Count; i++) {
            DTPolygon poly = new DTPolygon(SmoothPolygon(solidContours[i]));
            DestructibleObject dtObj = new GameObject("Terrain Object").AddComponent<DO_Polygon_Clip_Collide>();
            dtObj.ApplyPolygonList(new List<DTPolygon> { poly });
            dtObj.transform.position = new Vector3(-width * 0.5f, -height, 0);
        }
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

    private void GenerateSurfaceHeights(Vector2 sampleStart) {
        surfaceHeights = new int[width];
        for (int c = 0; c < width; c++) {
            surfaceHeights[c] = height - Mathf.Clamp((int)(Mathf.PerlinNoise(sampleStart.x + c / perlinXScale, sampleStart.y) * (maxHillHeight + 1)), 0, maxHillHeight);
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

    private void CreatePixelRenderMesh() {
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

    private void ComputeIntersectionGrid() {
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

    private List<List<Vector2>> PruneHoles(List<List<Vector2>> solidContours, List<List<Vector2>> holeContours) {
        // Remove any holes that are inside solid contours other than the outer contour
        List<List<Vector2>> keptHoles = new List<List<Vector2>>();

        for (int i = 0; i < holeContours.Count; i++) {
            List<Vector2> hole = holeContours[i];
            bool keep = true;

            for (int j = 1; j < solidContours.Count; j++) {
                if (hole.Count < minContourPoints || DTUtility.QuickPolyInPoly(hole, solidContours[j])) {
                    keep = false;
                    break;
                }
            }

            if (keep) {
                keptHoles.Add(hole);
            }
        }

        return keptHoles;
    }

    private List<Vector2> ComputeOuterContour(Vector2 sampleStart) {
        // Ignore the outer contour detected by the contour detection algorithm. Instead, resample from the perlin
        // noise at a higher frequency and then add the bottom left and right corners.
        List<Vector2> realOuterContour = new List<Vector2>();
        for (float x = 0; x < width; x += surfaceSampleDistance) {
            float y = height - Mathf.PerlinNoise(sampleStart.x + x / perlinXScale, sampleStart.y) * maxHillHeight;
            realOuterContour.Add(new Vector2(x, y));
        }

        // Top right corner
        float lastY = height - Mathf.PerlinNoise(sampleStart.x + width / perlinXScale, sampleStart.y) * maxHillHeight;
        realOuterContour.Add(new Vector2(width, lastY));

        // Bottom right corner
        realOuterContour.Add(new Vector2(width, 0));

        // Bottom left corner
        realOuterContour.Add(new Vector2(0, 0));

        return realOuterContour;
    }

    class CubicCurve
    {
        public readonly Vector2 start, cp0, cp1, end;

        public CubicCurve(Vector2 prev, Vector2 start, Vector2 end, Vector2 next, float curveSmoothingFactor) {
            this.start = start;
            cp0 = start + (end - prev) * 0.25f * curveSmoothingFactor;
            cp1 = end - (next - start) * 0.25f * curveSmoothingFactor;
            this.end = end;
        }

        public Vector2 GetPoint(float t) {
            float u = 1 - t;
            Vector2 a = 1 * u * u * u * start;
            Vector2 b = 3 * u * u * t * cp0;
            Vector2 c = 3 * u * t * t * cp1;
            Vector2 d = 1 * t * t * t * end;
            return a + b + c + d; 
        }

        public Vector2[] Approximate(int numPoints, bool includeLast) {
            Vector2[] points = new Vector2[numPoints];
            for (int i = 0; i < numPoints; i++) {
                float t = (float)i / (numPoints - (includeLast ? 1 : 0));
                points[i] = GetPoint(t);
            }
            return points;
        }
    }

    public List<Vector2> SmoothPolygon(List<Vector2> original) {
        List<Vector2> smoothed = new List<Vector2>();

        for (int i = 0; i < original.Count; i++) {
            Vector2 prev = original.GetCircular(i - 1);
            Vector2 start = original.GetCircular(i);
            Vector2 end = original.GetCircular(i + 1);
            Vector2 next = original.GetCircular(i + 2);
            smoothed.AddRange(new CubicCurve(prev, start, end, next, curveSmoothingFactor).Approximate(
                curveNumSamples, false));
        }

        return smoothed;
    }
}
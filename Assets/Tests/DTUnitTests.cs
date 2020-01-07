using DestructibleTerrain;
using DestructibleTerrain.Clipping;
using DestructibleTerrain.Destructible;
using DestructibleTerrain.ExplosionExecution;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.PerformanceTesting;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

using DO_Poly_Poly_TNet = DestructibleTerrain.Destructible.DestructibleObjectPolygonClippingPolygonCollider_TriangleNetTriangulator;
using DO_Poly_Tri_TNet = DestructibleTerrain.Destructible.DestructibleObjectPolygonClippingTriangulatedCollider_TriangleNetTriangulator;
using DO_Tri_Tri_TNet = DestructibleTerrain.Destructible.DestructibleObjectTriangulatedClippingTriangulatedCollider_TriangleNetTriangulator;
using DO_Poly_CHM_TNet = DestructibleTerrain.Destructible.DestructibleObjectPolygonClippingCustomHMCollider_TriangleNetTriangulator;
using DO_Poly_PPHM_TNet = DestructibleTerrain.Destructible.DestructibleObjectPolygonClippingPolyPartitionHMCollider_TriangleNetTriangulator;

using DO_Poly_Poly_PPEC = DestructibleTerrain.Destructible.DestructibleObjectPolygonClippingPolygonCollider_PolyPartitionEarClippingTriangulator;
using DO_Poly_Tri_PPEC = DestructibleTerrain.Destructible.DestructibleObjectPolygonClippingTriangulatedCollider_PolyPartitionEarClippingTriangulator;
using DO_Tri_Tri_PPEC = DestructibleTerrain.Destructible.DestructibleObjectTriangulatedClippingTriangulatedCollider_PolyPartitionEarClippingTriangulator;
using DO_Poly_CHM_PPEC = DestructibleTerrain.Destructible.DestructibleObjectPolygonClippingCustomHMCollider_PolyPartitionEarClippingTriangulator;
using DO_Poly_PPHM_PPEC = DestructibleTerrain.Destructible.DestructibleObjectPolygonClippingPolyPartitionHMCollider_PolyPartitionEarClippingTriangulator;

public static class DTUnitTests
{
    private static readonly IExplosionExecutor IterEE = IterativeExplosionExecutor.Instance;
    private static readonly IExplosionExecutor BulkEE = BulkExplosionExecutor.Instance;
    private static readonly IExplosionExecutor TrueEE = TrueBulkExplosionExecutor.Instance;

    private static readonly IPolygonSubtractor ClipperSub = ClipperSubtractor.Instance;
    private static readonly IPolygonSubtractor ORourkeSub = ORourkeSubtractor.Instance;

    public static GameObject CreateFloor(float length = 100, float thickness = 5) {
        GameObject go = new GameObject();

        BoxCollider2D boxCollider = go.AddComponent<BoxCollider2D>();
        boxCollider.size = new Vector2(1, 1);
        boxCollider.offset = new Vector2(0, -0.5f);

        Mesh mesh = new Mesh() {
            vertices = new Vector3[] {
                new Vector3(-0.5f, -1),
                new Vector3(-0.5f, 0),
                new Vector3(+0.5f, 0),
                new Vector3(+0.5f, -1)
            },
            triangles = new int[] { 0, 1, 2, 2, 3, 0 }
        };

        MeshFilter meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        go.AddComponent<MeshRenderer>();

        go.transform.localScale = new Vector3(length, thickness, 1);

        return go;
    }

    // Destroys all GameObjects.
    // This should be called at the end of each test.
    public static void CleanUp () {
        GameObject[] gos = UnityEngine.Object.FindObjectsOfType<GameObject>();
        foreach (GameObject go in gos) {
            UnityEngine.Object.Destroy(go);
        }
    }


    private static Vector2 V(float x, float y) {
        return new Vector2(x, y);
    }

    private static DTPolygon P(params Vector2[] contour) {
        return new DTPolygon(new List<Vector2>(contour));
    }

    private static DTPolygon Translate(DTPolygon poly, Vector2 t) {
        return new DTPolygon(
            poly.Contour.Select(v => { return v + t; }).ToList(),
            poly.Holes.Select(hole => {
                return hole.Select(v => { return v + t; }).ToList();
            }).ToList());
    }

    private static List<T> L<T>(params T[] elements) {
        return new List<T>(elements);
    }


    public static class Utility
    {
        [Test]
        public static void SimplifyContour () {
            var inContour = L(V(0, 0), V(1, 0), V(2, 0), V(3, 0), V(3, 1), V(2, 1), V(2, 2), V(2, 3), V(3, 3), V(2, 3), V(2, 2), V(2, 1), V(1, 1), V(0, 1), V(0, 1), V(0, 0));
            var expectedContour = L(V(3, 0), V(3, 1), V(0, 1), V(0, 0));
            var outContour = DTUtility.SimplifyContour(inContour);
            Assert.AreEqual(outContour.Count, expectedContour.Count);
            for (int i = 0; i < expectedContour.Count; ++i) {
                Assert.AreEqual(outContour[i], expectedContour[i]);
            }
        }
    }

    public static class Subtractors
    {
        private static readonly float sqrt2 = Mathf.Sqrt(2);

        private static readonly DTPolygon square = P(V(-1, -1), V(1, -1), V(1, 1), V(-1, 1)); // start with bottom left vertex
        private static readonly DTPolygon smallSquare = P(V(-0.5f, -0.5f), V(0.5f, -0.5f), V(0.5f, 0.5f), V(-0.5f, 0.5f)); // half width and height
        private static readonly DTPolygon wideRect = P(V(-2, -1), V(2, -1), V(2, 1), V(-2, 1)); // double width
        private static readonly DTPolygon narrowRect = P(V(-0.5f, -1), V(0.5f, -1), V(0.5f, 1), V(-0.5f, 1)); // half width
        private static readonly DTPolygon tallRect = P(V(-1, -2), V(1, -2), V(1, 2), V(-1, 2)); // double height
        private static readonly DTPolygon shortRect = P(V(-1, -0.5f), V(1, -0.5f), V(1, 0.5f), V(-1, 0.5f)); // half height
        private static readonly DTPolygon diamond = P(V(0, -1), V(1, 0), V(0, 1), V(-1, 0)); // start with bottom vertex
        private static readonly DTPolygon smallDiamond = P(V(0, -0.5f), V(0.5f, 0), V(0, 0.5f), V(-0.5f, 0)); // half width and height
        private static readonly DTPolygon octagon = P(V(-1, -1), V(0, -sqrt2), V(1, -1), V(sqrt2, 0), V(1, 1), V(0, sqrt2), V(-1, 1), V(-sqrt2, 0));

        // Return true if the subtraction result of two polygons is equal to any of the expected outcomes
        private static void VerifySub(IPolygonSubtractor sub, DTPolygon subj, DTPolygon clip, params List<DTPolygon>[] expectedOutcomes) {
            foreach (var expectedOutcome in expectedOutcomes) {
                if (DTUtility.ContainSameValues(expectedOutcome, sub.Subtract(subj, clip))) {
                    return;
                }
            }
            Assert.True(false);
        }

        // Return true if the subtraction result of two polygroups is equal to any of the expected outcomes
        private static void VerifySubPolygroups(IPolygonSubtractor sub, List<DTPolygon> subj, List<DTPolygon> clip, params List<List<DTPolygon>>[] expectedOutcomes) {
            foreach (var expectedOutcome in expectedOutcomes) {
                if (DTUtility.PolygroupsEqual(expectedOutcome, sub.SubtractPolygroup(subj, clip))) {
                    return;
                }
            }
            Assert.True(false);
        }

        public static void ConvexBasic(IPolygonSubtractor sub) {
            // Basic corner clips (produces an 'L' shape)
            DTPolygon clip0 = Translate(square, V(-1, -1)); // bottom left
            DTPolygon clip1 = Translate(square, V( 1, -1)); // bottom right
            DTPolygon clip2 = Translate(square, V( 1,  1)); // top right
            DTPolygon clip3 = Translate(square, V(-1,  1)); // top left
            // Basic edge clips (produces a 'C' shape)
            DTPolygon clip4 = Translate(narrowRect, V(0, -1)); // bottom
            DTPolygon clip5 = Translate(shortRect, V(1, 0)); // right
            DTPolygon clip6 = Translate(narrowRect, V(0, 1)); // top
            DTPolygon clip7 = Translate(shortRect, V(-1, 0)); // left
            // Diamond clip (produces 4 triangles)
            DTPolygon clip8 = P(V(0, -1.5f), V(1.5f, 0), V(0, 1.5f), V(-1.5f, 0)); // start with bottom vertex

            // Basic corner clips (produces an 'L' shape)
            List<DTPolygon> expected0 = L(P(V( 0,  0), V(0, -1), V(1, -1), V(1, 1), V(-1, 1), V(-1, 0)));
            List<DTPolygon> expected1 = L(P(V(-1, -1), V(0, -1), V(0,  0), V(1, 0), V( 1, 1), V(-1, 1)));
            List<DTPolygon> expected2 = L(P(V(-1, -1), V(1, -1), V(1,  0), V(0, 0), V( 0, 1), V(-1, 1)));
            List<DTPolygon> expected3 = L(P(V(-1, -1), V(1, -1), V(1,  1), V(0, 1), V( 0, 0), V(-1, 0)));
            // Basic edge clips (produces a 'C' shape)
            List<DTPolygon> expected4 = L(P(V(-1, -1), V(-0.5f, -1), V(-0.5f, 0), V(0.5f, 0), V(0.5f, -1), V(1, -1), V(1, 1), V(-1, 1)));
            List<DTPolygon> expected5 = L(P(V(-1, -1), V(1, -1), V(1, -0.5f), V(0, -0.5f), V(0, 0.5f), V(1, 0.5f), V(1, 1), V(-1, 1)));
            List<DTPolygon> expected6 = L(P(V(-1, -1), V(1, -1), V(1, 1), V(0.5f, 1), V(0.5f, 0), V(-0.5f, 0), V(-0.5f, 1), V(-1, 1)));
            List<DTPolygon> expected7 = L(P(V(-1, -1), V(1, -1), V(1, 1), V(-1, 1), V(-1, 0.5f), V(0, 0.5f), V(0, -0.5f), V(-1, -0.5f)));
            // Diamond clip (produces 4 triangles)
            List<DTPolygon> expected8 = L(
                P(V(   -1, -0.5f), V(-1, -1), V(-0.5f,    -1)),
                P(V( 0.5f,    -1), V( 1, -1), V(    1, -0.5f)),
                P(V(    1,  0.5f), V( 1,  1), V( 0.5f,     1)),
                P(V(-0.5f,     1), V(-1,  1), V(   -1,  0.5f))
            );

            // Complete removal
            List<DTPolygon> empty = new List<DTPolygon>();

            VerifySub(sub, square, clip0, expected0);
            VerifySub(sub, square, clip1, expected1);
            VerifySub(sub, square, clip2, expected2);
            VerifySub(sub, square, clip3, expected3);
            VerifySub(sub, square, clip4, expected4);
            VerifySub(sub, square, clip5, expected5);
            VerifySub(sub, square, clip6, expected6);
            VerifySub(sub, square, clip7, expected7);
            VerifySub(sub, square, clip8, expected8);

            // Complete removal
            VerifySub(sub, smallSquare, square, empty);
        }

        public static void ConvexDegenerateIntersection(IPolygonSubtractor sub) {
            // Shared corners (produces an 'L' shape)
            DTPolygon clip0 = Translate(smallSquare, V(-0.5f, -0.5f)); // bottom left
            DTPolygon clip1 = Translate(smallSquare, V(0.5f, -0.5f)); // bottom right
            DTPolygon clip2 = Translate(smallSquare, V(0.5f, 0.5f)); // top right
            DTPolygon clip3 = Translate(smallSquare, V(-0.5f, 0.5f)); // top left
            // Q edge inside P edge (produces a 'C' shape)
            DTPolygon clip4 = Translate(smallSquare, V(0, -0.5f)); // bottom
            DTPolygon clip5 = Translate(smallSquare, V(0.5f, 0)); // right
            DTPolygon clip6 = Translate(smallSquare, V(0, 0.5f)); // top
            DTPolygon clip7 = Translate(smallSquare, V(-0.5f, 0)); // left
            // Half same
            DTPolygon clip8 = Translate(shortRect, V(0, -0.5f)); // bottom half
            DTPolygon clip9 = Translate(narrowRect, V(0.5f, 0)); // right half
            DTPolygon clip10 = Translate(shortRect, V(0, 0.5f)); // top half
            DTPolygon clip11 = Translate(narrowRect, V(-0.5f, 0)); // left half
            // Diamond on corners
            DTPolygon clip12 = Translate(diamond, V(-1, -1)); // bottom left
            DTPolygon clip13 = Translate(diamond, V(1, -1)); // bottom right
            DTPolygon clip14 = Translate(diamond, V(1, 1)); // top right
            DTPolygon clip15 = Translate(diamond, V(-1, 1)); // top left
            // Diamond on edges
            DTPolygon clip16 = Translate(diamond, V(0, -1)); // bottom
            DTPolygon clip17 = Translate(diamond, V(1, 0)); // right
            DTPolygon clip18 = Translate(diamond, V(0, 1)); // top
            DTPolygon clip19 = Translate(diamond, V(-1, 0)); // left

            // Basic corner clips (produces an 'L' shape)
            List<DTPolygon> expected0 = L(P(V(0, 0), V(0, -1), V(1, -1), V(1, 1), V(-1, 1), V(-1, 0)));
            List<DTPolygon> expected1 = L(P(V(-1, -1), V(0, -1), V(0, 0), V(1, 0), V(1, 1), V(-1, 1)));
            List<DTPolygon> expected2 = L(P(V(-1, -1), V(1, -1), V(1, 0), V(0, 0), V(0, 1), V(-1, 1)));
            List<DTPolygon> expected3 = L(P(V(-1, -1), V(1, -1), V(1, 1), V(0, 1), V(0, 0), V(-1, 0)));
            // Basic edge clips (produces a 'C' shape)
            List<DTPolygon> expected4 = L(P(V(-1, -1), V(-0.5f, -1), V(-0.5f, 0), V(0.5f, 0), V(0.5f, -1), V(1, -1), V(1, 1), V(-1, 1)));
            List<DTPolygon> expected5 = L(P(V(-1, -1), V(1, -1), V(1, -0.5f), V(0, -0.5f), V(0, 0.5f), V(1, 0.5f), V(1, 1), V(-1, 1)));
            List<DTPolygon> expected6 = L(P(V(-1, -1), V(1, -1), V(1, 1), V(0.5f, 1), V(0.5f, 0), V(-0.5f, 0), V(-0.5f, 1), V(-1, 1)));
            List<DTPolygon> expected7 = L(P(V(-1, -1), V(1, -1), V(1, 1), V(-1, 1), V(-1, 0.5f), V(0, 0.5f), V(0, -0.5f), V(-1, -0.5f)));
            // Half same
            List<DTPolygon> expected8 = L(P(V(-1, 0), V(1, 0), V(1, 1), V(-1, 1)));
            List<DTPolygon> expected9 = L(P(V(-1, -1), V(0, -1), V(0, 1), V(-1, 1)));
            List<DTPolygon> expected10 = L(P(V(-1, -1), V(1, -1), V(1, 0), V(-1, 0)));
            List<DTPolygon> expected11 = L(P(V(0, -1), V(1, -1), V(1, 1), V(0, 1)));
            // Diamond on corners
            List<DTPolygon> expected12 = L(P(V(0, -1), V(1, -1), V(1, 1), V(-1, 1), V(-1, 0)));
            List<DTPolygon> expected13 = L(P(V(-1, -1), V(0, -1), V(1, 0), V(1, 1), V(-1, 1)));
            List<DTPolygon> expected14 = L(P(V(-1, -1), V(1, -1), V(1, 0), V(0, 1), V(-1, 1)));
            List<DTPolygon> expected15 = L(P(V(-1, -1), V(1, -1), V(1, 1), V(0, 1), V(-1, 0)));
            // Diamond on edges
            List<DTPolygon> expected16 = L(P(V(-1, -1), V(0, 0), V(1, -1), V(1, 1), V(-1, 1)));
            List<DTPolygon> expected17 = L(P(V(-1, -1), V(1, -1), V(0, 0), V(1, 1), V(-1, 1)));
            List<DTPolygon> expected18 = L(P(V(-1, -1), V(1, -1), V(1, 1), V(0, 0), V(-1, 1)));
            List<DTPolygon> expected19 = L(P(V(-1, -1), V(1, -1), V(1, 1), V(-1, 1), V(0, 0)));

            // Complete removal
            List<DTPolygon> empty = new List<DTPolygon>();

            // Complete removal
            VerifySub(sub, square, square, empty);

            VerifySub(sub, square, clip0, expected0);
            VerifySub(sub, square, clip1, expected1);
            VerifySub(sub, square, clip2, expected2);
            VerifySub(sub, square, clip3, expected3);
            VerifySub(sub, square, clip4, expected4);
            VerifySub(sub, square, clip5, expected5);
            VerifySub(sub, square, clip6, expected6);
            VerifySub(sub, square, clip7, expected7);
            VerifySub(sub, square, clip8, expected8);
            VerifySub(sub, square, clip9, expected9);
            VerifySub(sub, square, clip10, expected10);
            VerifySub(sub, square, clip11, expected11);
            VerifySub(sub, square, clip12, expected12);
            VerifySub(sub, square, clip13, expected13);
            VerifySub(sub, square, clip14, expected14);
            VerifySub(sub, square, clip15, expected15);
            VerifySub(sub, square, clip16, expected16);
            VerifySub(sub, square, clip17, expected17);
            VerifySub(sub, square, clip18, expected18);
            VerifySub(sub, square, clip19, expected19);
        }

        public static void ConvexDegenerateNoIntersection(IPolygonSubtractor sub) {
            // Half-overlapping edges
            DTPolygon clip0 = Translate(diamond, V(0.5f, -1.5f)); // bottom right edge shared, Q shifted down-left
            DTPolygon clip1 = Translate(diamond, V(1.5f, -0.5f)); // bottom right edge shared, Q shifted up-right
            DTPolygon clip2 = Translate(diamond, V(1.5f, 0.5f)); // top right edge shared, Q shifted down-right
            DTPolygon clip3 = Translate(diamond, V(0.5f, 1.5f)); // top right edge shared, Q shifted up-left
            DTPolygon clip4 = Translate(diamond, V(-0.5f, 1.5f)); // top left edge shared, Q shifted up-right
            DTPolygon clip5 = Translate(diamond, V(-1.5f, 0.5f)); // top left edge shared, Q shifted down-left
            DTPolygon clip6 = Translate(diamond, V(-1.5f, -0.5f)); // bottom left edge shared, Q shifted up-left
            DTPolygon clip7 = Translate(diamond, V(-0.5f, -1.5f)); // bottom left edge shared, Q shifted down-right
            // P edge completely inside Q edge
            DTPolygon clip8  = Translate(diamond, V(0.75f, -0.75f)); // bottom right edge inside Q edge
            DTPolygon clip9  = Translate(diamond, V(0.75f, 0.75f)); // top right edge inside Q edge
            DTPolygon clip10 = Translate(diamond, V(-0.75f, 0.75f)); // top left edge inside Q edge
            DTPolygon clip11 = Translate(diamond, V(-0.75f, -0.75f)); // bottom left edge inside Q edge
            // P edge completely contains Q edge
            DTPolygon clip12 = Translate(smallDiamond, V(0.75f, -0.75f)); // bottom right edge contains Q edge
            DTPolygon clip13 = Translate(smallDiamond, V(0.75f, 0.75f)); // top right edge contains Q edge
            DTPolygon clip14 = Translate(smallDiamond, V(-0.75f, 0.75f)); // top left edge contains Q edge
            DTPolygon clip15 = Translate(smallDiamond, V(-0.75f, -0.75f)); // bottom left edge contains Q edge
            // P edge and Q edge are the same
            DTPolygon clip16 = Translate(diamond, V(1, -1)); // bottom right edge same
            DTPolygon clip17 = Translate(diamond, V(1, 1)); // top right edge same
            DTPolygon clip18 = Translate(diamond, V(-1, 1)); // top left edge same
            DTPolygon clip19 = Translate(diamond, V(-1, -1)); // bottom left edge same
            // Single shared vertex (octagon)
            DTPolygon clip20 = Translate(octagon, V(-2, -2)); // octagon, bottom left vertex same
            DTPolygon clip21 = Translate(octagon, V( 2, -2)); // octagon, bottom right vertex same
            DTPolygon clip22 = Translate(octagon, V( 2,  2)); // octagon, top left vertex same
            DTPolygon clip23 = Translate(octagon, V(-2,  2)); // octagon, top right vertex same
            // Single shared vertex (diamond)
            DTPolygon clip24 = Translate(diamond, V(-1, -2)); // diamond top, square bottom left vertex same
            DTPolygon clip25 = Translate(diamond, V( 1, -2)); // diamond top, square bottom right vertex same
            DTPolygon clip26 = Translate(diamond, V(-1,  2)); // diamond bottom, square top left vertex same
            DTPolygon clip27 = Translate(diamond, V( 1,  2)); // diamond bottom, square top right vertex same
            // Q single vertex inside P edge
            DTPolygon clip28 = Translate(square, V( 1.5f, -1.5f)); // bottom right edge contains Q vertex
            DTPolygon clip29 = Translate(square, V( 1.5f,  1.5f)); // top right edge contains Q vertex
            DTPolygon clip30 = Translate(square, V(-1.5f,  1.5f)); // top left edge contains Q vertex
            DTPolygon clip31 = Translate(square, V(-1.5f, -1.5f)); // bottom left edge contains Q vertex
            // P single vertex inside Q edge
            DTPolygon clip32 = Translate(diamond, V(-1.5f, -1.5f)); // bottom left vertex in Q edge
            DTPolygon clip33 = Translate(diamond, V( 1.5f, -1.5f)); // bottom right vertex in Q edge
            DTPolygon clip34 = Translate(diamond, V( 1.5f,  1.5f)); // top right vertex in Q edge
            DTPolygon clip35 = Translate(diamond, V(-1.5f,  1.5f)); // top left vertex in Q edge

            List<DTPolygon> expectedSquare = L(square);
            List<DTPolygon> expectedDiamond = L(diamond);
            List<DTPolygon> expectedSmallDiamond = L(smallDiamond);
            List<DTPolygon> expectedOctagon = L(octagon);

            VerifySub(sub, diamond, clip0, expectedDiamond);
            VerifySub(sub, diamond, clip1, expectedDiamond);
            VerifySub(sub, diamond, clip2, expectedDiamond);
            VerifySub(sub, diamond, clip3, expectedDiamond);
            VerifySub(sub, diamond, clip4, expectedDiamond);
            VerifySub(sub, diamond, clip5, expectedDiamond);
            VerifySub(sub, diamond, clip6, expectedDiamond);
            VerifySub(sub, diamond, clip7, expectedDiamond);
            VerifySub(sub, smallDiamond, clip8, expectedSmallDiamond);
            VerifySub(sub, smallDiamond, clip9, expectedSmallDiamond);
            VerifySub(sub, smallDiamond, clip10, expectedSmallDiamond);
            VerifySub(sub, smallDiamond, clip11, expectedSmallDiamond);
            VerifySub(sub, diamond, clip12, expectedDiamond);
            VerifySub(sub, diamond, clip13, expectedDiamond);
            VerifySub(sub, diamond, clip14, expectedDiamond);
            VerifySub(sub, diamond, clip15, expectedDiamond);
            VerifySub(sub, diamond, clip16, expectedDiamond);
            VerifySub(sub, diamond, clip17, expectedDiamond);
            VerifySub(sub, diamond, clip18, expectedDiamond);
            VerifySub(sub, diamond, clip19, expectedDiamond);
            VerifySub(sub, octagon, clip20, expectedOctagon);
            VerifySub(sub, octagon, clip21, expectedOctagon);
            VerifySub(sub, octagon, clip22, expectedOctagon);
            VerifySub(sub, octagon, clip23, expectedOctagon);
            VerifySub(sub, octagon, clip24, expectedOctagon);
            VerifySub(sub, octagon, clip25, expectedOctagon);
            VerifySub(sub, octagon, clip26, expectedOctagon);
            VerifySub(sub, octagon, clip27, expectedOctagon);
            VerifySub(sub, diamond, clip28, expectedDiamond);
            VerifySub(sub, diamond, clip29, expectedDiamond);
            VerifySub(sub, diamond, clip30, expectedDiamond);
            VerifySub(sub, diamond, clip31, expectedDiamond);
            VerifySub(sub, octagon, clip32, expectedOctagon);
            VerifySub(sub, octagon, clip33, expectedOctagon);
            VerifySub(sub, octagon, clip34, expectedOctagon);
            VerifySub(sub, octagon, clip35, expectedOctagon);
        }

        public static void ConvexCasesProducingHoles(IPolygonSubtractor sub) {
            // Diamond hole in center
            DTPolygon clip0 = smallDiamond;
            // Diamond holes touching edge
            DTPolygon clip1 = Translate(smallDiamond, V(0, -0.5f)); // bottom
            DTPolygon clip2 = Translate(smallDiamond, V(0.5f, 0)); // right
            DTPolygon clip3 = Translate(smallDiamond, V(0, 0.5f)); // top
            DTPolygon clip4 = Translate(smallDiamond, V(-0.5f, 0)); // left
            // Exact diamond clip (produces 4 triangles)
            DTPolygon clip5 = diamond;

            // Diamond hole in center
            List<DTPolygon> expected0 = L(new DTPolygon(
                square.Contour,
                L(smallDiamond.Contour.AsEnumerable().Reverse().ToList())
            ));
            // Diamond holes touching edge
            List<DTPolygon> expected1 = L(P(V(-1, -1), V(0, -1), V(-0.5f, -0.5f), V(0, 0), V(0.5f, -0.5f), V(0, -1), V(1, -1), V(1, 1), V(-1, 1)));
            List<DTPolygon> expected1Hole = L(new DTPolygon(
                square.Contour,
                L(clip1.Contour.AsEnumerable().Reverse().ToList())
            ));
            List<DTPolygon> expected2 = L(P(V(-1, -1), V(1, -1), V(1, 0), V(0.5f, -0.5f), V(0, 0), V(0.5f, 0.5f), V(1, 0), V(1, 1), V(-1, 1)));
            List<DTPolygon> expected2Hole = L(new DTPolygon(
                square.Contour,
                L(clip2.Contour.AsEnumerable().Reverse().ToList())
            ));
            List<DTPolygon> expected3 = L(P(V(-1, -1), V(1, -1), V(1, 1), V(0, 1), V(0.5f, 0.5f), V(0, 0), V(-0.5f, 0.5f), V(0, 1), V(-1, 1)));
            List<DTPolygon> expected3Hole = L(new DTPolygon(
                square.Contour,
                L(clip3.Contour.AsEnumerable().Reverse().ToList())
            ));
            List<DTPolygon> expected4 = L(P(V(-1, -1), V(1, -1), V(1, 1), V(-1, 1), V(-1, 0), V(-0.5f, 0.5f), V(0, 0), V(-0.5f, -0.5f), V(-1, 0)));
            List<DTPolygon> expected4Hole = L(new DTPolygon(
                square.Contour,
                L(clip4.Contour.AsEnumerable().Reverse().ToList())
            ));
            // Exact diamond clip (produces 4 triangles)
            List<DTPolygon> expected5 = L(
                P(V(-1, 0), V(-1, -1), V(0, -1)),
                P(V(0, -1), V(1, -1), V(1, 0)),
                P(V(1, 0), V(1, 1), V(0, 1)),
                P(V(0, 1), V(-1, 1), V(-1, 0))
            );
            List<DTPolygon> expected5Hole = L(new DTPolygon(
                square.Contour,
                L(clip5.Contour.AsEnumerable().Reverse().ToList())
            ));
            List<DTPolygon> expected5Single0 = L(P(V(-1, -1), V(0, -1), V(-1, 0), V(0, 1), V(1, 0), V(0, -1), V(1, -1), V(1, 1), V(-1, 1)));
            List<DTPolygon> expected5Single1 = L(P(V(-1, -1), V(1, -1), V(1, 0), V(0, -1), V(-1, 0), V(0, 1), V(1, 0), V(1, 1), V(-1, 1)));
            List<DTPolygon> expected5Single2 = L(P(V(-1, -1), V(1, -1), V(1, 1), V(0, 1), V(1, 0), V(0, -1), V(-1, 0), V(0, 1), V(-1, 1)));
            List<DTPolygon> expected5Single3 = L(P(V(-1, -1), V(1, -1), V(1, 1), V(-1, 1), V(-1, 0), V(0, 1), V(1, 0), V(0, -1), V(-1, 0)));

            VerifySub(sub, square, clip0, expected0);
            VerifySub(sub, square, clip1, expected1, expected1Hole);
            VerifySub(sub, square, clip2, expected2, expected2Hole);
            VerifySub(sub, square, clip3, expected3, expected3Hole);
            VerifySub(sub, square, clip4, expected4, expected4Hole);
            VerifySub(sub, square, clip5, expected5, expected5Hole, expected5Single0, expected5Single1, expected5Single2, expected5Single3);
        }

        public static void BasicPolygroup(IPolygonSubtractor sub) {
            List<DTPolygon> subjectPolygroup = L(
                P(V(-2.5f, 0.5f), V(2.5f, 0.5f), V(2.5f, 1.5f), V(-2.5f, 1.5f)),
                P(V(-2.5f, -0.5f), V(2.5f, -0.5f), V(2.5f, 0.5f), V(-2.5f, 0.5f)),
                P(V(-2.5f, -1.5f), V(2.5f, -1.5f), V(2.5f, -0.5f), V(-2.5f, -0.5f))
            );

            List<DTPolygon> clipPolygroup = L(
                P(V(-1.5f, -1.5f), V(-0.5f, -1.5f), V(-0.5f, 1.5f), V(-1.5f, 1.5f)),
                P(V(0.5f, -1.5f), V(1.5f, -1.5f), V(1.5f, 1.5f), V(0.5f, 1.5f))
            );
            
            List<List<DTPolygon>> expectedPolygroups = L(
                L(
                    P(V(-2.5f, 0.5f), V(-1.5f, 0.5f), V(-1.5f, 1.5f), V(-2.5f, 1.5f)),
                    P(V(-2.5f, -0.5f), V(-1.5f, -0.5f), V(-1.5f, 0.5f), V(-2.5f, 0.5f)),
                    P(V(-2.5f, -1.5f), V(-1.5f, -1.5f), V(-1.5f, -0.5f), V(-2.5f, -0.5f))
                ),
                L(
                    P(V(-0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 1.5f), V(-0.5f, 1.5f)),
                    P(V(-0.5f, -0.5f), V(0.5f, -0.5f), V(0.5f, 0.5f), V(-0.5f, 0.5f)),
                    P(V(-0.5f, -1.5f), V(0.5f, -1.5f), V(0.5f, -0.5f), V(-0.5f, -0.5f))
                ),
                L(
                    P(V(1.5f, 0.5f), V(2.5f, 0.5f), V(2.5f, 1.5f), V(1.5f, 1.5f)),
                    P(V(1.5f, -0.5f), V(2.5f, -0.5f), V(2.5f, 0.5f), V(1.5f, 0.5f)),
                    P(V(1.5f, -1.5f), V(2.5f, -1.5f), V(2.5f, -0.5f), V(1.5f, -0.5f))
                )
            );

            // Clipper does this in a weird way and joins only the first polygroup into a single polygon
            List<List<DTPolygon>> expectedPolygroupsJoinedFirst = L(
                L(
                    P(V(-2.5f, -1.5f), V(-1.5f, -1.5f), V(-1.5f, 1.5f), V(-2.5f, 1.5f))
                ),
                L(
                    P(V(-0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 1.5f), V(-0.5f, 1.5f)),
                    P(V(-0.5f, -0.5f), V(0.5f, -0.5f), V(0.5f, 0.5f), V(-0.5f, 0.5f)),
                    P(V(-0.5f, -1.5f), V(0.5f, -1.5f), V(0.5f, -0.5f), V(-0.5f, -0.5f))
                ),
                L(
                    P(V(1.5f, 0.5f), V(2.5f, 0.5f), V(2.5f, 1.5f), V(1.5f, 1.5f)),
                    P(V(1.5f, -0.5f), V(2.5f, -0.5f), V(2.5f, 0.5f), V(1.5f, 0.5f)),
                    P(V(1.5f, -1.5f), V(2.5f, -1.5f), V(2.5f, -0.5f), V(1.5f, -0.5f))
                )
            );

            VerifySubPolygroups(sub, subjectPolygroup, clipPolygroup, expectedPolygroups, expectedPolygroupsJoinedFirst);
        }



        public static class Clipper
        {
            public static IPolygonSubtractor sub = ClipperSub;

            [Test]
            public static void ConvexBasic() {
                Subtractors.ConvexBasic(sub);
            }

            [Test]
            public static void ConvexDegenerateIntersection() {
                Subtractors.ConvexDegenerateIntersection(sub);
            }

            [Test]
            public static void ConvexDegenerateNoIntersection() {
                Subtractors.ConvexDegenerateNoIntersection(sub);
            }

            [Test]
            public static void ConvexCasesProducingHoles() {
                Subtractors.ConvexCasesProducingHoles(sub);
            }

            [Test]
            public static void BasicPolygroup() {
                Subtractors.BasicPolygroup(sub);
            }
        }

        public static class ORourke
        {
            public static IPolygonSubtractor sub = ORourkeSub;

            [Test]
            public static void ConvexBasic() {
                Subtractors.ConvexBasic(sub);
            }

            [Test]
            public static void ConvexDegenerateIntersection() {
                Subtractors.ConvexDegenerateIntersection(sub);
            }

            [Test]
            public static void ConvexDegenerateNoIntersection() {
                Subtractors.ConvexDegenerateNoIntersection(sub);
            }

            [Test]
            public static void ConvexCasesProducingHoles() {
                Subtractors.ConvexCasesProducingHoles(sub);
            }

            [Test]
            public static void BasicPolygroup() {
                Subtractors.BasicPolygroup(sub);
            }
        }
    }
}

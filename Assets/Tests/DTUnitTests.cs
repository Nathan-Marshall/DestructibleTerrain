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

using DO_Tri_Tri = DestructibleTerrain.Destructible.DO_Triangle_Clip_Collide;

public static class DTUnitTests
{
    private static readonly IExplosionExecutor IterEE = IterativeExplosionExecutor.Instance;

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
        private static void VerifyPolygons(List<DTPolygon> result, params List<DTPolygon>[] expectedOutcomes) {
            foreach (var expectedOutcome in expectedOutcomes) {
                if (DTUtility.ContainSameValues(expectedOutcome, result)) {
                    return;
                }
            }
            Assert.True(false);
        }

        // Return true if the subtraction result of two polygroups is equal to any of the expected outcomes
        private static void VerifyPolygroups(List<List<DTPolygon>> result, params List<List<DTPolygon>>[] expectedOutcomes) {
            foreach (var expectedOutcome in expectedOutcomes) {
                if (DTUtility.PolygroupsEqual(expectedOutcome, result)) {
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

            VerifyPolygons(sub.Subtract(square, clip0), expected0);
            VerifyPolygons(sub.Subtract(square, clip1), expected1);
            VerifyPolygons(sub.Subtract(square, clip2), expected2);
            VerifyPolygons(sub.Subtract(square, clip3), expected3);
            VerifyPolygons(sub.Subtract(square, clip4), expected4);
            VerifyPolygons(sub.Subtract(square, clip5), expected5);
            VerifyPolygons(sub.Subtract(square, clip6), expected6);
            VerifyPolygons(sub.Subtract(square, clip7), expected7);
            VerifyPolygons(sub.Subtract(square, clip8), expected8);

            // Complete removal
            VerifyPolygons(sub.Subtract(smallSquare, square), empty);
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
            VerifyPolygons(sub.Subtract(square, square), empty);

            VerifyPolygons(sub.Subtract(square, clip0), expected0);
            VerifyPolygons(sub.Subtract(square, clip1), expected1);
            VerifyPolygons(sub.Subtract(square, clip2), expected2);
            VerifyPolygons(sub.Subtract(square, clip3), expected3);
            VerifyPolygons(sub.Subtract(square, clip4), expected4);
            VerifyPolygons(sub.Subtract(square, clip5), expected5);
            VerifyPolygons(sub.Subtract(square, clip6), expected6);
            VerifyPolygons(sub.Subtract(square, clip7), expected7);
            VerifyPolygons(sub.Subtract(square, clip8), expected8);
            VerifyPolygons(sub.Subtract(square, clip9), expected9);
            VerifyPolygons(sub.Subtract(square, clip10), expected10);
            VerifyPolygons(sub.Subtract(square, clip11), expected11);
            VerifyPolygons(sub.Subtract(square, clip12), expected12);
            VerifyPolygons(sub.Subtract(square, clip13), expected13);
            VerifyPolygons(sub.Subtract(square, clip14), expected14);
            VerifyPolygons(sub.Subtract(square, clip15), expected15);
            VerifyPolygons(sub.Subtract(square, clip16), expected16);
            VerifyPolygons(sub.Subtract(square, clip17), expected17);
            VerifyPolygons(sub.Subtract(square, clip18), expected18);
            VerifyPolygons(sub.Subtract(square, clip19), expected19);
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

            VerifyPolygons(sub.Subtract(diamond, clip0), expectedDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip1), expectedDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip2), expectedDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip3), expectedDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip4), expectedDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip5), expectedDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip6), expectedDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip7), expectedDiamond);
            VerifyPolygons(sub.Subtract(smallDiamond, clip8), expectedSmallDiamond);
            VerifyPolygons(sub.Subtract(smallDiamond, clip9), expectedSmallDiamond);
            VerifyPolygons(sub.Subtract(smallDiamond, clip10), expectedSmallDiamond);
            VerifyPolygons(sub.Subtract(smallDiamond, clip11), expectedSmallDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip12), expectedDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip13), expectedDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip14), expectedDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip15), expectedDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip16), expectedDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip17), expectedDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip18), expectedDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip19), expectedDiamond);
            VerifyPolygons(sub.Subtract(octagon, clip20), expectedOctagon);
            VerifyPolygons(sub.Subtract(octagon, clip21), expectedOctagon);
            VerifyPolygons(sub.Subtract(octagon, clip22), expectedOctagon);
            VerifyPolygons(sub.Subtract(octagon, clip23), expectedOctagon);
            VerifyPolygons(sub.Subtract(octagon, clip24), expectedOctagon);
            VerifyPolygons(sub.Subtract(octagon, clip25), expectedOctagon);
            VerifyPolygons(sub.Subtract(octagon, clip26), expectedOctagon);
            VerifyPolygons(sub.Subtract(octagon, clip27), expectedOctagon);
            VerifyPolygons(sub.Subtract(diamond, clip28), expectedDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip29), expectedDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip30), expectedDiamond);
            VerifyPolygons(sub.Subtract(diamond, clip31), expectedDiamond);
            VerifyPolygons(sub.Subtract(octagon, clip32), expectedOctagon);
            VerifyPolygons(sub.Subtract(octagon, clip33), expectedOctagon);
            VerifyPolygons(sub.Subtract(octagon, clip34), expectedOctagon);
            VerifyPolygons(sub.Subtract(octagon, clip35), expectedOctagon);
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

            VerifyPolygons(sub.Subtract(square, clip0), expected0);
            VerifyPolygons(sub.Subtract(square, clip1), expected1, expected1Hole);
            VerifyPolygons(sub.Subtract(square, clip2), expected2, expected2Hole);
            VerifyPolygons(sub.Subtract(square, clip3), expected3, expected3Hole);
            VerifyPolygons(sub.Subtract(square, clip4), expected4, expected4Hole);
            VerifyPolygons(sub.Subtract(square, clip5), expected5, expected5Hole, expected5Single0, expected5Single1, expected5Single2, expected5Single3);
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

            VerifyPolygroups(sub.SubtractPolygroup(subjectPolygroup, clipPolygroup), expectedPolygroups, expectedPolygroupsJoinedFirst);
        }

        public static void DO_Subtraction(IPolygonSubtractor sub, DO_Tri_Tri dtObject, float x, float y) {
            DTPolygon box = new DTPolygon(square.Contour, L(smallSquare.Contour));
            
            dtObject.transform.position = new Vector3(x, y, 0);
            dtObject.ApplyPolygonList(L(box));

            Explosion explosion = new Explosion(x + 1, y + 1, 0.2f, 8);

            IterEE.ExecuteExplosions(L(explosion), L(dtObject), sub);
        }

        //[Test]
        public static void DO_Subtraction() {
            var clipperGO = new GameObject().AddComponent<DO_Tri_Tri>();
            var orourkeGO = new GameObject().AddComponent<DO_Tri_Tri>();

            Subtractors.DO_Subtraction(ClipperSub, clipperGO, -2, 0);
            Subtractors.DO_Subtraction(ORourkeSub, orourkeGO, 2, 0);

            var clipperList = clipperGO.GetTransformedPolygonList();
            for (int i = 0; i < clipperList.Count; ++i) {
                clipperList[i] = clipperList[i].Simplify();
                if (clipperList[i] == null) {
                    clipperList.RemoveAt(i--);
                }
            }

            var orourkeList = orourkeGO.GetTransformedPolygonList();
            for (int i = 0; i < orourkeList.Count; ++i) {
                orourkeList[i] = orourkeList[i].Simplify();
                if (orourkeList[i] == null) {
                    orourkeList.RemoveAt(i--);
                }
            }

            VerifyPolygroups(L(clipperList), L(orourkeList));
            DTUtility.CleanUpGameObjects();
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

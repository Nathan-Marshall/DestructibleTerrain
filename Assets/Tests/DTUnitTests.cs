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
    

    public static class Subtractors
    {
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

        private static List<DTPolygon> L(params DTPolygon[] polys) {
            return new List<DTPolygon>(polys);
        }

        private static void VerifySub(IPolygonSubtractor sub, DTPolygon subj, DTPolygon clip, List<DTPolygon> expected) {
            Assert.True(DTUtility.ContainSameValues(expected, sub.Subtract(subj, clip)));
        }

        public static void ConvexBasic(IPolygonSubtractor sub) {
            DTPolygon square = P(V(-1, -1), V(1, -1), V(1, 1), V(-1, 1)); // start with bottom left vertex

            // All four basic corner clips (produces an 'L' shape)
            DTPolygon clip0 = Translate(square, V(-1, -1)); // bottom left
            DTPolygon clip1 = Translate(square, V( 1, -1)); // bottom right
            DTPolygon clip2 = Translate(square, V( 1,  1)); // top right
            DTPolygon clip3 = Translate(square, V(-1,  1)); // top left
            // Diamond clip (produces 4 triangles)
            DTPolygon clip4 = P(V(0, -1.5f), V(1.5f, 0), V(0, 1.5f), V(-1.5f, 0)); // start with bottom vertex

            // All four basic corner clips (produces an 'L' shape)
            List<DTPolygon> expected0 = L(P(V( 0,  0), V(0, -1), V(1, -1), V(1, 1), V(-1, 1), V(-1, 0)));
            List<DTPolygon> expected1 = L(P(V(-1, -1), V(0, -1), V(0,  0), V(1, 0), V( 1, 1), V(-1, 1)));
            List<DTPolygon> expected2 = L(P(V(-1, -1), V(1, -1), V(1,  0), V(0, 0), V( 0, 1), V(-1, 1)));
            List<DTPolygon> expected3 = L(P(V(-1, -1), V(1, -1), V(1,  1), V(0, 1), V( 0, 0), V(-1, 0)));
            // Diamond clip (produces 4 triangles)
            List<DTPolygon> expected4 = L(
                P(V(   -1, -0.5f), V(-1, -1), V(-0.5f,    -1)),
                P(V( 0.5f,    -1), V( 1, -1), V(    1, -0.5f)),
                P(V(    1,  0.5f), V( 1,  1), V( 0.5f,     1)),
                P(V(-0.5f,     1), V(-1,  1), V(   -1,  0.5f))
            );

            VerifySub(sub, square, clip0, expected0);
            VerifySub(sub, square, clip1, expected1);
            VerifySub(sub, square, clip2, expected2);
            VerifySub(sub, square, clip3, expected3);
            VerifySub(sub, square, clip4, expected4);
        }

        public static void ConvexDegenerateNoIntersection(IPolygonSubtractor sub) {
            DTPolygon square = P(V(-1, -1), V(1, -1), V(1, 1), V(-1, 1)); // start with bottom left vertex
            DTPolygon wideRect = P(V(-2, -1), V(2, -1), V(2, 1), V(-2, 1)); // double width
            DTPolygon narrowRect = P(V(-0.5f, -1), V(0.5f, -1), V(0.5f, 1), V(-0.5f, 1)); // half width
            DTPolygon tallRect = P(V(-1, -2), V(1, -2), V(1, 2), V(-1, 2)); // double height
            DTPolygon shortRect = P(V(-1, -0.5f), V(1, -0.5f), V(1, 0.5f), V(-1, 0.5f)); // half height
            DTPolygon diamond = P(V(0, -1), V(1, 0), V(0, 1), V(-1, 0)); // start with bottom vertex

            // Half-overlapping edges
            DTPolygon clip0 = Translate(square, V(-1, -2)); // bottom edge shared, Q shifted left
            DTPolygon clip1 = Translate(square, V( 1, -2)); // bottom edge shared, Q shifted right
            DTPolygon clip2 = Translate(square, V( 2, -1)); // right edge shared, Q shifted down
            DTPolygon clip3 = Translate(square, V( 2,  1)); // right edge shared, Q shifted up
            DTPolygon clip4 = Translate(square, V( 1,  2)); // top edge shared, Q shifted right
            DTPolygon clip5 = Translate(square, V(-1,  2)); // top edge shared, Q shifted left
            DTPolygon clip6 = Translate(square, V(-2,  1)); // left edge shared, Q shifted up
            DTPolygon clip7 = Translate(square, V(-2, -1)); // left edge shared, Q shifted down
            // P edge completely inside Q edge
            DTPolygon clip8  = Translate(wideRect, V( 0, -2)); // bottom edge inside Q edge
            DTPolygon clip9  = Translate(tallRect, V( 2,  0)); // right edge inside Q edge
            DTPolygon clip10 = Translate(wideRect, V( 0,  2)); // top edge inside Q edge
            DTPolygon clip11 = Translate(tallRect, V(-2,  0)); // left edge inside Q edge
            // P edge completely contains Q edge
            DTPolygon clip12 = Translate(narrowRect, V( 0, -2)); // bottom edge contains Q edge
            DTPolygon clip13 = Translate(shortRect,  V( 2,  0)); // right edge contains Q edge
            DTPolygon clip14 = Translate(narrowRect, V( 0,  2)); // top edge contains Q edge
            DTPolygon clip15 = Translate(shortRect,  V(-2,  0)); // left edge contains Q edge
            // P edge and Q edge are the same
            DTPolygon clip16 = Translate(square, V( 0, -2)); // bottom edge same
            DTPolygon clip17 = Translate(square, V( 2,  0)); // right edge same
            DTPolygon clip18 = Translate(square, V( 0,  2)); // top edge same
            DTPolygon clip19 = Translate(square, V(-2,  0)); // left edge same
            // Single shared vertex (square)
            DTPolygon clip20 = Translate(square, V(-2, -2)); // square, bottom left vertex same
            DTPolygon clip21 = Translate(square, V( 2, -2)); // square, bottom right vertex same
            DTPolygon clip22 = Translate(square, V( 2,  2)); // square, top left vertex same
            DTPolygon clip23 = Translate(square, V(-2,  2)); // square, top right vertex same
            // Single shared vertex (diamond)
            DTPolygon clip24 = Translate(diamond, V(-1, -2)); // diamond, bottom left vertex same
            DTPolygon clip25 = Translate(diamond, V( 1, -2)); // diamond, bottom right vertex same
            DTPolygon clip26 = Translate(diamond, V(-1,  2)); // diamond, top left vertex same
            DTPolygon clip27 = Translate(diamond, V( 1,  2)); // diamond, top right vertex same
            // Q single vertex inside P edge
            DTPolygon clip28 = Translate(diamond, V( 0, -2)); // bottom edge contains Q vertex
            DTPolygon clip29 = Translate(diamond, V( 2,  0)); // right edge contains Q vertex
            DTPolygon clip30 = Translate(diamond, V( 0,  2)); // top edge contains Q vertex
            DTPolygon clip31 = Translate(diamond, V(-2,  0)); // left edge contains Q vertex
            // P single vertex inside Q edge
            DTPolygon clip32 = Translate(diamond, V(-1.5f, -1.5f)); // bottom edge contains Q vertex
            DTPolygon clip33 = Translate(diamond, V( 1.5f, -1.5f)); // right edge contains Q vertex
            DTPolygon clip34 = Translate(diamond, V( 1.5f,  1.5f)); // top edge contains Q vertex
            DTPolygon clip35 = Translate(diamond, V(-1.5f,  1.5f)); // left edge contains Q vertex

            List<DTPolygon> expectedNoChange = L(square);
            
            VerifySub(sub, square, clip0, expectedNoChange);
            VerifySub(sub, square, clip1, expectedNoChange);
            VerifySub(sub, square, clip2, expectedNoChange);
            VerifySub(sub, square, clip3, expectedNoChange);
            VerifySub(sub, square, clip4, expectedNoChange);
            VerifySub(sub, square, clip5, expectedNoChange);
            VerifySub(sub, square, clip6, expectedNoChange);
            VerifySub(sub, square, clip7, expectedNoChange);
            VerifySub(sub, square, clip8, expectedNoChange);
            VerifySub(sub, square, clip9, expectedNoChange);
            VerifySub(sub, square, clip10, expectedNoChange);
            VerifySub(sub, square, clip11, expectedNoChange);
            VerifySub(sub, square, clip12, expectedNoChange);
            VerifySub(sub, square, clip13, expectedNoChange);
            VerifySub(sub, square, clip14, expectedNoChange);
            VerifySub(sub, square, clip15, expectedNoChange);
            VerifySub(sub, square, clip16, expectedNoChange);
            VerifySub(sub, square, clip17, expectedNoChange);
            VerifySub(sub, square, clip18, expectedNoChange);
            VerifySub(sub, square, clip19, expectedNoChange);
            VerifySub(sub, square, clip20, expectedNoChange);
            VerifySub(sub, square, clip21, expectedNoChange);
            VerifySub(sub, square, clip22, expectedNoChange);
            VerifySub(sub, square, clip23, expectedNoChange);
            VerifySub(sub, square, clip24, expectedNoChange);
            VerifySub(sub, square, clip25, expectedNoChange);
            VerifySub(sub, square, clip26, expectedNoChange);
            VerifySub(sub, square, clip27, expectedNoChange);
            VerifySub(sub, square, clip28, expectedNoChange);
            VerifySub(sub, square, clip29, expectedNoChange);
            VerifySub(sub, square, clip30, expectedNoChange);
            VerifySub(sub, square, clip31, expectedNoChange);
            VerifySub(sub, square, clip32, expectedNoChange);
            VerifySub(sub, square, clip33, expectedNoChange);
            VerifySub(sub, square, clip34, expectedNoChange);
            VerifySub(sub, square, clip35, expectedNoChange);
        }

        public static class Clipper
        {
            public static IPolygonSubtractor sub = ClipperSub;

            [Test]
            public static void ConvexBasic() {
                Subtractors.ConvexBasic(sub);
            }

            [Test]
            public static void ConvexDegenerateNoIntersection() {
                Subtractors.ConvexDegenerateNoIntersection(sub);
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
            public static void ConvexDegenerateNoIntersection() {
                Subtractors.ConvexDegenerateNoIntersection(sub);
            }
        }
    }
}

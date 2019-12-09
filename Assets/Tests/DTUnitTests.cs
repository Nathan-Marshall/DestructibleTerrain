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
        public static void SubtractConvexFromConvex(IPolygonSubtractor sub) {
            Vector2 v(float x, float y) {
                return new Vector2(x, y);
            }

            DTPolygon p(params Vector2[] va) {
                return new DTPolygon(new List<Vector2>(va));
            }

            DTPolygon subject = p( v(1,1), v(3,1), v(3,3), v(1,3) );
            DTPolygon clip0 = p( v(0,0), v(2,0), v(2,2), v(0,2) );
            DTPolygon clip1 = p( v(2,0), v(4,0), v(4,2), v(2,2) );
            DTPolygon clip2 = p( v(2,2), v(4,2), v(4,4), v(2,4) );
            DTPolygon clip3 = p( v(0,2), v(2,2), v(2,4), v(0,4) );
            DTPolygon clip4 = p( v(2,0.5f), v(3.5f,2), v(2,3.5f), v(0.5f,2) );

            List<DTPolygon> expected0 = new List<DTPolygon>() { p( v(2,2), v(2,1), v(3,1), v(3,3), v(1,3), v(1,2) ) };
            List<DTPolygon> expected1 = new List<DTPolygon>() { p( v(1,1), v(2,1), v(2,2), v(3,2), v(3,3), v(1,3) ) };
            List<DTPolygon> expected2 = new List<DTPolygon>() { p( v(1,1), v(3,1), v(3,2), v(2,2), v(2,3), v(1,3) ) };
            List<DTPolygon> expected3 = new List<DTPolygon>() { p( v(1,1), v(3,1), v(3,3), v(2,3), v(2,2), v(1,2) ) };
            List<DTPolygon> expected4 = new List<DTPolygon>() {
                p( v(1,1.5f), v(1,1), v(1.5f,1) ),
                p( v(2.5f,1), v(3,1), v(3,1.5f) ),
                p( v(3,2.5f), v(3,3), v(2.5f,3) ),
                p( v(1.5f,3), v(1,3), v(1,2.5f) ),
            };

            Assert.True(DTUtility.ContainSameValues(expected0, sub.Subtract(subject, clip0)));
            Assert.True(DTUtility.ContainSameValues(expected1, sub.Subtract(subject, clip1)));
            Assert.True(DTUtility.ContainSameValues(expected2, sub.Subtract(subject, clip2)));
            Assert.True(DTUtility.ContainSameValues(expected3, sub.Subtract(subject, clip3)));
            Assert.True(DTUtility.ContainSameValues(expected4, sub.Subtract(subject, clip4)));
        }

        public static class Clipper
        {
            [Test]
            public static void SubtractConvexFromConvex() {
                Subtractors.SubtractConvexFromConvex(ClipperSub);
            }
        }

        public static class ORourke
        {
            [Test]
            public static void SubtractConvexFromConvex() {
                Subtractors.SubtractConvexFromConvex(ORourkeSub);
            }
        }
    }
}

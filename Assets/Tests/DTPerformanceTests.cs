using DestructibleTerrain;
using DestructibleTerrain.Clipping;
using DestructibleTerrain.Destructible;
using DestructibleTerrain.ExplosionExecution;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.PerformanceTesting;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

using DO_Poly_Poly_TNet = DestructibleTerrain.Destructible.DestructibleObjectPolygonClippingPolygonCollider;
using DO_Poly_Tri_TNet = DestructibleTerrain.Destructible.DestructibleObjectPolygonClippingTriangulatedCollider;
using DO_Tri_Tri_TNet = DestructibleTerrain.Destructible.DestructibleObjectTriangulatedClippingTriangulatedCollider;
using DO_Poly_CHM_TNet = DestructibleTerrain.Destructible.DestructibleObjectPolygonClippingCustomHMCollider;
using DO_CHM_CHM_TNet = DestructibleTerrain.Destructible.DestructibleObjectCustomHMClippingCustomHMCollider;
using DO_Poly_PPHM_TNet = DestructibleTerrain.Destructible.DestructibleObjectPolygonClippingPolyPartitionHMCollider;
using DO_PPHM_PPHM_TNet = DestructibleTerrain.Destructible.DestructibleObjectPolyPartitionHMClippingPolyPartitionHMCollider;

public static class DTPerformanceTests
{
    private static readonly IExplosionExecutor IterEE = IterativeExplosionExecutor.Instance;
    private static readonly IExplosionExecutor BulkEE = BulkExplosionExecutor.Instance;
    private static readonly IExplosionExecutor TrueEE = TrueBulkExplosionExecutor.Instance;

    private static readonly IPolygonSubtractor ClipperSub = ClipperSubtractor.Instance;
    private static readonly IPolygonSubtractor ORourkeSub = ORourkeSubtractor.Instance;

    public static class ProfilerMarkers {
        public static readonly string Namespace = "DTPerformanceTests";

        // DTPerformanceTests ProfilerMarkers
        public static readonly string CreationStr = Namespace + ".Creation";
        public static readonly ProfilerMarker Creation = new ProfilerMarker(CreationStr);

        public static readonly string ProcessExplosionsStr = Namespace + ".ProcessExplosions";
        public static readonly ProfilerMarker ProcessExplosions = new ProfilerMarker(ProcessExplosionsStr);

        public static readonly string TotalAllocatedMemoryStr = Namespace + ".TotalAllocatedMemory";

        // Unity ProfilerMarkers
        public static readonly string PhysicsStr = "FixedUpdate.Physics2DFixedUpdate";
        public static readonly ProfilerMarker Physics = new ProfilerMarker(PhysicsStr);

        public static readonly SampleGroupDefinition[] SampleGroupDefinitions = {
            // DTPerformanceTests ProfilerMarkers
            new SampleGroupDefinition(CreationStr),
            new SampleGroupDefinition(ProcessExplosionsStr),
            
            // DestructibleTerrain ProfilerMarkers
            new SampleGroupDefinition(DTProfileMarkers.TriangulationStr),
            new SampleGroupDefinition(DTProfileMarkers.HertelMehlhornStr),
            
            // Unity ProfilerMarkers
            new SampleGroupDefinition(PhysicsStr),
        };
    }

    public static GameObject CreateFloor(float length = 100, float thickness = 5) {
        GameObject go = new GameObject();

        BoxCollider2D boxCollider = go.AddComponent<BoxCollider2D>();
        boxCollider.size = new Vector2(1, 1);
        boxCollider.offset = new Vector2(0, -0.5f);

        Mesh mesh = new Mesh() {
            vertices = new Vector3[] {
                new Vector3(-0.5f, -1),
                new Vector3( 0.5f, -1),
                new Vector3( 0.5f,  0),
                new Vector3(-0.5f,  0)
            },
            triangles = new int[] { 0, 2, 1, 2, 0, 3 }
        };

        MeshFilter meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        go.AddComponent<MeshRenderer>();

        go.transform.localScale = new Vector3(length, thickness, 1);

        return go;
    }

    public static T CreateRingObject<T> (bool hasHole = true, float radius = 1.0f,
            int numEdges = 24) where T : DestructibleObject {
        float variationAmplitude = 0.1f * radius;
        float angleStep = 2 * Mathf.PI / numEdges;

        List<Vector2> exterior = new List<Vector2>();
        List<Vector2> hole = new List<Vector2>();

        for (int i = 0; i < numEdges; ++i) {
            float rOuter = UnityEngine.Random.Range(radius - variationAmplitude, radius + variationAmplitude);
            float rInner = UnityEngine.Random.Range(radius - variationAmplitude, radius + variationAmplitude) * 0.5f;
            Vector2 dirOuter = new Vector2(Mathf.Cos(i * angleStep), Mathf.Sin(i * angleStep));
            Vector2 dirInner = new Vector2(dirOuter.x, -dirOuter.y);

            // CW exterior
            exterior.Add(dirOuter * rOuter);

            // CCW hole
            if (hasHole) {
                hole.Add(dirInner * rInner);
            }
        }

        DTPolygon polygon;
        if (hasHole) {
            polygon = new DTPolygon(exterior, new List<List<Vector2>>() { hole });
        } else {
            polygon = new DTPolygon(exterior);
        }

        GameObject go = new GameObject();
        T destructibleObject = go.AddComponent<T>();
        destructibleObject.ApplyPolygonList(new List<DTPolygon> { polygon });

        return destructibleObject;
    }

    // Destroys all GameObjects.
    // This should be called at the end of each test.
    public static void CleanUp () {
        DTUtility.CleanUpGameObjects();
    }

    private static IEnumerator CreateRingsAndMeasure<T> (bool hasHoles, float radius, int numEdges, int columns, int rows)
            where T : DestructibleObject {
        using (Measure.ProfilerMarkers(ProfilerMarkers.SampleGroupDefinitions)) {
            ProfilerMarkers.Creation.Begin();

            float spacing = 2.5f * radius;
            float totalWidth = columns * spacing;
            for (int c = 0; c < columns; ++c) {
                for (int r = 0; r < rows; ++r) {
                    Vector2 pos = new Vector2(c * spacing - totalWidth * 0.5f, (r + 0.5f) * spacing);
                    CreateRingObject<T>(hasHoles, radius, numEdges).transform.position = pos;
                }
            }

            ProfilerMarkers.Creation.End();
        }
        yield return null;
    }

    public static IEnumerator PhysicsTest<T> (bool hasHoles, float radius, int numEdges, int columns, int rows, int warmupFrames, int captureFrames)
            where T : DestructibleObject {
        // Set a constant seed so that we get the same results every time
        UnityEngine.Random.InitState(12345);

        new GameObject("Main Camera").AddComponent<Camera>().transform.position = new Vector3(0, 50, -100);
        CreateFloor(1000);

        // Meaure the time to create all destructible objects
        yield return CreateRingsAndMeasure<T>(hasHoles, radius, numEdges, columns, rows);

        // Allow objects to collide before we measure physics
        yield return WarmupFrames(warmupFrames);

        // Measure physics
        yield return CaptureFrames(captureFrames);

        CleanUp();
    }

    public static IEnumerator ContinuousExplosionTest<T> (IExplosionExecutor ee, IPolygonSubtractor sub,
            int numExplosions, float explosionRadius, float explosionInterval, bool hasHoles, float radius, int numEdges,
            int columns, int rows, int warmupFrames, int captureFrames) where T : DestructibleObject {
        // Set a constant seed so that we get the same results every time
        UnityEngine.Random.InitState(12345);

        new GameObject("Main Camera").AddComponent<Camera>().transform.position = new Vector3(0, 50, -100);
        CreateFloor(1000);

        // Meaure the time to create all destructible objects
        yield return CreateRingsAndMeasure<T>(hasHoles, radius, numEdges, columns, rows);

        // Set all objects to static
        var dObjs = DestructibleObject.FindAll();
        foreach (var dObj in dObjs) {
            dObj.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        }

        float explosionTimer = 0;
        // To be called every fixed update;
        // Generates a number of explosions every time the timer reaches a fixed interval.
        void explosionGenerator () {
            explosionTimer += Time.fixedDeltaTime;
            List<Explosion> explosions = new List<Explosion>();
            while (explosionTimer > explosionInterval) {
                explosionTimer -= explosionInterval;
                for (int i = 0; i < numExplosions; ++i) {
                    explosions.Add(new Explosion(UnityEngine.Random.Range(-10, 10), UnityEngine.Random.Range(0, 20),
                        explosionRadius, 24));
                }
            }

            ProfilerMarkers.ProcessExplosions.Begin();
            ee.ExecuteExplosions(explosions, DestructibleObject.FindAll(), sub);
            ProfilerMarkers.ProcessExplosions.End();
        }

        // Allow objects to collide before we measure physics
        yield return WarmupFrames(warmupFrames, explosionGenerator);

        // Measure physics
        yield return CaptureFrames(captureFrames, explosionGenerator);

        CleanUp();
    }

    public static IEnumerator OneTimeExplosionTest<T> (IExplosionExecutor ee, IPolygonSubtractor sub,
            int numExplosions, float explosionRadius, bool hasHoles, float radius, int numEdges,
            int columns, int rows) where T : DestructibleObject {
        // Set a constant seed so that we get the same results every time
        UnityEngine.Random.InitState(12345);

        new GameObject("Main Camera").AddComponent<Camera>().transform.position = new Vector3(0, 50, -100);
        CreateFloor(1000);

        // Meaure the time to create all destructible objects
        yield return CreateRingsAndMeasure<T>(hasHoles, radius, numEdges, columns, rows);

        // Set all objects to static
        var dObjs = DestructibleObject.FindAll();
        foreach (var dObj in dObjs) {
            dObj.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        }

        List<Explosion> explosions = new List<Explosion>();
        for (int i = 0; i < numExplosions; ++i) {
            explosions.Add(new Explosion(UnityEngine.Random.Range(-10, 10), UnityEngine.Random.Range(0, 20),
                explosionRadius, 24));
        }

        using (Measure.ProfilerMarkers(ProfilerMarkers.SampleGroupDefinitions)) {
            ProfilerMarkers.ProcessExplosions.Begin();
            ee.ExecuteExplosions(explosions, DestructibleObject.FindAll(), sub);
            ProfilerMarkers.ProcessExplosions.End();
        }
        yield return null;

        // Wait to observe whether the result is correct
        WaitFrames(250);

        CleanUp();
    }

    private static IEnumerator WarmupFrames(int numFrames, Action onFrame = null) {
        for (int updates = 0; updates < numFrames; ++updates) {
            onFrame?.Invoke();
            yield return new WaitForFixedUpdate();
        }
    }

    private static IEnumerator CaptureFrames(int numFrames, Action onFrame = null) {
        using (Measure.ProfilerMarkers(ProfilerMarkers.SampleGroupDefinitions)) {
            for (int updates = 0; updates < numFrames; ++updates) {
                onFrame?.Invoke();
                Measure.Custom(new SampleGroupDefinition(ProfilerMarkers.TotalAllocatedMemoryStr, SampleUnit.Megabyte),
                    Profiler.GetTotalAllocatedMemoryLong() / 1048576f);
                yield return new WaitForFixedUpdate();
            }
        }
    }

    private static IEnumerator WaitFrames(int numFrames) {
        for (int updates = 0; updates < numFrames; ++updates) {
            yield return new WaitForFixedUpdate();
        }
    }



    public static IEnumerator CollisionTestSolids100<T>() where T : DestructibleObject {
        yield return PhysicsTest<T>(false, 1.0f, 24, 10, 10, 100, 200);
    }

    public static IEnumerator CollisionTestSolids200<T>() where T : DestructibleObject {
        yield return PhysicsTest<T>(false, 1.0f, 24, 20, 10, 100, 200);
    }

    public static IEnumerator CollisionTestSolids400<T>() where T : DestructibleObject {
        yield return PhysicsTest<T>(false, 1.0f, 24, 40, 10, 100, 200);
    }

    public static IEnumerator CollisionTestRings100<T>() where T : DestructibleObject {
        yield return PhysicsTest<T>(true, 1.0f, 24, 10, 10, 100, 200);
    }

    public static IEnumerator CollisionTestRings200<T>() where T : DestructibleObject {
        yield return PhysicsTest<T>(true, 1.0f, 24, 20, 10, 100, 200);
    }

    public static IEnumerator CollisionTestRings400<T>() where T : DestructibleObject {
        yield return PhysicsTest<T>(true, 1.0f, 24, 40, 10, 100, 200);
    }

    public static IEnumerator ContinuousExplosionTestManyRings<T>(IExplosionExecutor ee, IPolygonSubtractor sub)
            where T : DestructibleObject {
        yield return ContinuousExplosionTest<T>(ee, sub, 1, 1, Time.fixedDeltaTime, true, 1.0f, 24, 10, 10, 100, 200);
    }

    public static IEnumerator OneTimeExplosionTestManyRings<T>(IExplosionExecutor ee, IPolygonSubtractor sub)
            where T : DestructibleObject {
        yield return OneTimeExplosionTest<T>(ee, sub, 100, 1, true, 1.0f, 24, 10, 10);
    }

    public static IEnumerator OneTimeExplosionTestLargeComplexRings<T>(IExplosionExecutor ee, IPolygonSubtractor sub)
            where T : DestructibleObject {
        yield return OneTimeExplosionTest<T>(ee, sub, 10, 1, true, 10.0f, 240, 2, 1);
    }



    public static class PureCollision
    {
        public static class Poly_Poly
        {
            //[UnityTest, Performance] public static IEnumerator Solids100() { yield return CollisionTestSolids100<DO_Poly_Poly_TNet>(); }
            //[UnityTest, Performance] public static IEnumerator Solids200() { yield return CollisionTestSolids200<DO_Poly_Poly_TNet>(); }
            [UnityTest, Performance] public static IEnumerator Solids400() { yield return CollisionTestSolids400<DO_Poly_Poly_TNet>(); }
            //[UnityTest, Performance] public static IEnumerator Rings100() { yield return CollisionTestRings100<DO_Poly_Poly_TNet>(); }
            //[UnityTest, Performance] public static IEnumerator Rings200() { yield return CollisionTestRings200<DO_Poly_Poly_TNet>(); }
            [UnityTest, Performance] public static IEnumerator Rings400() { yield return CollisionTestRings400<DO_Poly_Poly_TNet>(); }
        }

        public static class Poly_Tri
        {
            //[UnityTest, Performance] public static IEnumerator Solids100() { yield return CollisionTestSolids100<DO_Poly_Tri_TNet>(); }
            //[UnityTest, Performance] public static IEnumerator Solids200() { yield return CollisionTestSolids200<DO_Poly_Tri_TNet>(); }
            [UnityTest, Performance] public static IEnumerator Solids400() { yield return CollisionTestSolids400<DO_Poly_Tri_TNet>(); }
            //[UnityTest, Performance] public static IEnumerator Rings100() { yield return CollisionTestRings100<DO_Poly_Tri_TNet>(); }
            //[UnityTest, Performance] public static IEnumerator Rings200() { yield return CollisionTestRings200<DO_Poly_Tri_TNet>(); }
            [UnityTest, Performance] public static IEnumerator Rings400() { yield return CollisionTestRings400<DO_Poly_Tri_TNet>(); }
        }

        public static class Poly_CHM
        {
            //[UnityTest, Performance] public static IEnumerator Solids100() { yield return CollisionTestSolids100<DO_Poly_CHM_TNet>(); }
            //[UnityTest, Performance] public static IEnumerator Solids200() { yield return CollisionTestSolids200<DO_Poly_CHM_TNet>(); }
            [UnityTest, Performance] public static IEnumerator Solids400() { yield return CollisionTestSolids400<DO_Poly_CHM_TNet>(); }
            //[UnityTest, Performance] public static IEnumerator Rings100() { yield return CollisionTestRings100<DO_Poly_CHM_TNet>(); }
            //[UnityTest, Performance] public static IEnumerator Rings200() { yield return CollisionTestRings200<DO_Poly_CHM_TNet>(); }
            [UnityTest, Performance] public static IEnumerator Rings400() { yield return CollisionTestRings400<DO_Poly_CHM_TNet>(); }
        }

        public static class Poly_PPHM
        {
            //[UnityTest, Performance] public static IEnumerator Solids100() { yield return CollisionTestSolids100<DO_Poly_PPHM_TNet>(); }
            //[UnityTest, Performance] public static IEnumerator Solids200() { yield return CollisionTestSolids200<DO_Poly_PPHM_TNet>(); }
            [UnityTest, Performance] public static IEnumerator Solids400() { yield return CollisionTestSolids400<DO_Poly_PPHM_TNet>(); }
            //[UnityTest, Performance] public static IEnumerator Rings100() { yield return CollisionTestRings100<DO_Poly_PPHM_TNet>(); }
            //[UnityTest, Performance] public static IEnumerator Rings200() { yield return CollisionTestRings200<DO_Poly_PPHM_TNet>(); }
            [UnityTest, Performance] public static IEnumerator Rings400() { yield return CollisionTestRings400<DO_Poly_PPHM_TNet>(); }
        }
    }

    public static class ExplosionPerFrameTests
    {
        public static class Poly_Poly_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return ContinuousExplosionTestManyRings<DO_Poly_Poly_TNet> (IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return ContinuousExplosionTestManyRings<DO_Poly_Poly_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return ContinuousExplosionTestManyRings<DO_Poly_Poly_TNet>(TrueEE, ClipperSub); }
        }

        public static class Poly_Tri_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return ContinuousExplosionTestManyRings<DO_Poly_Tri_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return ContinuousExplosionTestManyRings<DO_Poly_Tri_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return ContinuousExplosionTestManyRings<DO_Poly_Tri_TNet>(TrueEE, ClipperSub); }
        }

        public static class Poly_CHM_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return ContinuousExplosionTestManyRings<DO_Poly_CHM_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return ContinuousExplosionTestManyRings<DO_Poly_CHM_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return ContinuousExplosionTestManyRings<DO_Poly_CHM_TNet>(TrueEE, ClipperSub); }
        }

        public static class Poly_PPHM_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return ContinuousExplosionTestManyRings<DO_Poly_PPHM_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return ContinuousExplosionTestManyRings<DO_Poly_PPHM_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return ContinuousExplosionTestManyRings<DO_Poly_PPHM_TNet>(TrueEE, ClipperSub); }
        }

        public static class Tri_Tri_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return ContinuousExplosionTestManyRings<DO_Tri_Tri_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return ContinuousExplosionTestManyRings<DO_Tri_Tri_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return ContinuousExplosionTestManyRings<DO_Tri_Tri_TNet>(TrueEE, ClipperSub); }
        }

        public static class CHM_CHM_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return ContinuousExplosionTestManyRings<DO_CHM_CHM_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return ContinuousExplosionTestManyRings<DO_CHM_CHM_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return ContinuousExplosionTestManyRings<DO_CHM_CHM_TNet>(TrueEE, ClipperSub); }
        }

        public static class PPHM_PPHM_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return ContinuousExplosionTestManyRings<DO_PPHM_PPHM_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return ContinuousExplosionTestManyRings<DO_PPHM_PPHM_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return ContinuousExplosionTestManyRings<DO_PPHM_PPHM_TNet>(TrueEE, ClipperSub); }
        }

        public static class Tri_Tri_ORourke
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return ContinuousExplosionTestManyRings<DO_Tri_Tri_TNet>(IterEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return ContinuousExplosionTestManyRings<DO_Tri_Tri_TNet>(BulkEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return ContinuousExplosionTestManyRings<DO_Tri_Tri_TNet>(TrueEE, ORourkeSub); }
        }

        public static class CHM_CHM_ORourke
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return ContinuousExplosionTestManyRings<DO_CHM_CHM_TNet>(IterEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return ContinuousExplosionTestManyRings<DO_CHM_CHM_TNet>(BulkEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return ContinuousExplosionTestManyRings<DO_CHM_CHM_TNet>(TrueEE, ORourkeSub); }
        }

        public static class PPHM_PPHM_ORourke
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return ContinuousExplosionTestManyRings<DO_PPHM_PPHM_TNet>(IterEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return ContinuousExplosionTestManyRings<DO_PPHM_PPHM_TNet>(BulkEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return ContinuousExplosionTestManyRings<DO_PPHM_PPHM_TNet>(TrueEE, ORourkeSub); }
        }
    }

    public static class OneTime100ExplosionTests
    {
        public static class Poly_Poly_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestManyRings<DO_Poly_Poly_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestManyRings<DO_Poly_Poly_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestManyRings<DO_Poly_Poly_TNet>(TrueEE, ClipperSub); }
        }

        public static class Poly_Tri_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestManyRings<DO_Poly_Tri_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestManyRings<DO_Poly_Tri_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestManyRings<DO_Poly_Tri_TNet>(TrueEE, ClipperSub); }
        }

        public static class Poly_CHM_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestManyRings<DO_Poly_CHM_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestManyRings<DO_Poly_CHM_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestManyRings<DO_Poly_CHM_TNet>(TrueEE, ClipperSub); }
        }

        public static class Poly_PPHM_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestManyRings<DO_Poly_PPHM_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestManyRings<DO_Poly_PPHM_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestManyRings<DO_Poly_PPHM_TNet>(TrueEE, ClipperSub); }
        }

        public static class Tri_Tri_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestManyRings<DO_Tri_Tri_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestManyRings<DO_Tri_Tri_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestManyRings<DO_Tri_Tri_TNet>(TrueEE, ClipperSub); }
        }

        public static class CHM_CHM_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestManyRings<DO_CHM_CHM_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestManyRings<DO_CHM_CHM_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestManyRings<DO_CHM_CHM_TNet>(TrueEE, ClipperSub); }
        }

        public static class PPHM_PPHM_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestManyRings<DO_PPHM_PPHM_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestManyRings<DO_PPHM_PPHM_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestManyRings<DO_PPHM_PPHM_TNet>(TrueEE, ClipperSub); }
        }

        public static class Tri_Tri_ORourke
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestManyRings<DO_Tri_Tri_TNet>(IterEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestManyRings<DO_Tri_Tri_TNet>(BulkEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestManyRings<DO_Tri_Tri_TNet>(TrueEE, ORourkeSub); }
        }

        public static class CHM_CHM_ORourke
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestManyRings<DO_CHM_CHM_TNet>(IterEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestManyRings<DO_CHM_CHM_TNet>(BulkEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestManyRings<DO_CHM_CHM_TNet>(TrueEE, ORourkeSub); }
        }

        public static class PPHM_PPHM_ORourke
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestManyRings<DO_PPHM_PPHM_TNet>(IterEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestManyRings<DO_PPHM_PPHM_TNet>(BulkEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestManyRings<DO_PPHM_PPHM_TNet>(TrueEE, ORourkeSub); }
        }
    }

    public static class OneTimeLargeObjectExplosionTests
    {
        public static class Poly_Poly_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Poly_Poly_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Poly_Poly_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Poly_Poly_TNet>(TrueEE, ClipperSub); }
        }

        public static class Poly_Tri_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Poly_Tri_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Poly_Tri_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Poly_Tri_TNet>(TrueEE, ClipperSub); }
        }

        public static class Poly_CHM_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Poly_CHM_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Poly_CHM_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Poly_CHM_TNet>(TrueEE, ClipperSub); }
        }

        public static class Poly_PPHM_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Poly_PPHM_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Poly_PPHM_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Poly_PPHM_TNet>(TrueEE, ClipperSub); }
        }

        public static class Tri_Tri_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Tri_Tri_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Tri_Tri_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Tri_Tri_TNet>(TrueEE, ClipperSub); }
        }

        public static class CHM_CHM_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_CHM_CHM_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_CHM_CHM_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_CHM_CHM_TNet>(TrueEE, ClipperSub); }
        }

        public static class PPHM_PPHM_Clipper
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_PPHM_PPHM_TNet>(IterEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_PPHM_PPHM_TNet>(BulkEE, ClipperSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_PPHM_PPHM_TNet>(TrueEE, ClipperSub); }
        }

        public static class Tri_Tri_ORourke
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Tri_Tri_TNet>(IterEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Tri_Tri_TNet>(BulkEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Tri_Tri_TNet>(TrueEE, ORourkeSub); }
        }

        public static class CHM_CHM_ORourke
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_CHM_CHM_TNet>(IterEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_CHM_CHM_TNet>(BulkEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_CHM_CHM_TNet>(TrueEE, ORourkeSub); }
        }

        public static class PPHM_PPHM_ORourke
        {
            [UnityTest, Performance] public static IEnumerator IterativeClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_PPHM_PPHM_TNet>(IterEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator BulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_PPHM_PPHM_TNet>(BulkEE, ORourkeSub); }
            //[UnityTest, Performance] public static IEnumerator TrueBulkClipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_PPHM_PPHM_TNet>(TrueEE, ORourkeSub); }
        }
    }
}

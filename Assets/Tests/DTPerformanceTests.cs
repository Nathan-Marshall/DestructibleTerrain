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

using DO_Poly_Poly = DestructibleTerrain.Destructible.DO_Polygon_Clip_Collide;

using DO_Poly_Tri = DestructibleTerrain.Destructible.DO_Polygon_Clip_Triangle_Collide;
using DO_Poly_CHM = DestructibleTerrain.Destructible.DO_Polygon_Clip_CustomHM_Collide;
using DO_Poly_PPHM = DestructibleTerrain.Destructible.DO_Polygon_Clip_PolyPartitionHM_Collide;

using DO_Tri_Tri = DestructibleTerrain.Destructible.DO_Triangle_Clip_Collide;
using DO_CHM_CHM = DestructibleTerrain.Destructible.DO_CustomHM_Clip_Collide;
using DO_PPHM_PPHM = DestructibleTerrain.Destructible.DO_PolyPartitionHM_Clip_Collide;

public static class DTPerformanceTests
{
    private static readonly IExplosionExecutor IterEE = IterativeExplosionExecutor.Instance;

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
            new SampleGroupDefinition(DTProfilerMarkers.ApplyColliderStr),
            new SampleGroupDefinition(DTProfilerMarkers.HertelMehlhornStr),
            new SampleGroupDefinition(DTProfilerMarkers.IdentifyHolesStr),
            new SampleGroupDefinition(DTProfilerMarkers.MeshToPolygroupStr),
            new SampleGroupDefinition(DTProfilerMarkers.PolygroupToMeshStr),
            new SampleGroupDefinition(DTProfilerMarkers.PolygrouperStr),
            new SampleGroupDefinition(DTProfilerMarkers.SimplifyPolygonStr),
            new SampleGroupDefinition(DTProfilerMarkers.SubtractPolygroupStr),
            new SampleGroupDefinition(DTProfilerMarkers.TransformationStr),
            new SampleGroupDefinition(DTProfilerMarkers.TriangulationStr),
            new SampleGroupDefinition(DTProfilerMarkers.TriangleNetStr),

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
        float variationAmplitude = 0.0f * radius;
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

    private static void CreateRingsAndMeasure<T> (bool hasHoles, bool isStatic, float radius, int numEdges, int columns, int rows)
            where T : DestructibleObject {
        using (Measure.ProfilerMarkers(ProfilerMarkers.SampleGroupDefinitions)) {
            ProfilerMarkers.Creation.Begin();

            float spacing = 2.5f * radius;
            float totalWidth = columns * spacing;
            for (int c = 0; c < columns; ++c) {
                for (int r = 0; r < rows; ++r) {
                    Vector2 pos = new Vector2(c * spacing - totalWidth * 0.5f, (r + 0.5f) * spacing);
                    pos.x += UnityEngine.Random.Range(-spacing * 0.1f, spacing * 0.1f);
                    T ring = CreateRingObject<T>(hasHoles, radius, numEdges);
                    if (isStatic) {
                        ring.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                    }
                    ring.transform.position = pos;
                }
            }

            ProfilerMarkers.Creation.End();
        }
    }

    public static IEnumerator PhysicsTest<T> (bool hasHoles, float radius, int numEdges, int columns, int rows, int warmupFrames, int captureFrames)
            where T : DestructibleObject {
        // Set a constant seed so that we get the same results every time
        UnityEngine.Random.InitState(12345);

        new GameObject("Main Camera").AddComponent<Camera>().transform.position = new Vector3(0, 50, -100);
        CreateFloor(1000);

        // Meaure the time to create all destructible objects
        CreateRingsAndMeasure<T>(hasHoles, false, radius, numEdges, columns, rows);
        yield return null;

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

        // Meaure the time to create all destructible objects
        CreateRingsAndMeasure<T>(hasHoles, true, radius, numEdges, columns, rows);

        // Set all objects to static
        var dObjs = DestructibleObject.FindAll();
        foreach (var dObj in dObjs) {
            dObj.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        }
        yield return null;

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

        // Meaure the time to create all destructible objects
        CreateRingsAndMeasure<T>(hasHoles, true, radius, numEdges, columns, rows);
        yield return null;

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
        yield return WaitFrames(60);

        CleanUp();
    }

    public static IEnumerator SimpleTest<T>(IExplosionExecutor ee, IPolygonSubtractor sub) where T : DestructibleObject {
        // Set a constant seed so that we get the same results every time
        UnityEngine.Random.InitState(12345);

        new GameObject("Main Camera").AddComponent<Camera>().transform.position = new Vector3(0, 50, -100);

        // Meaure the time to create all destructible objects
        T ring;
        using (Measure.ProfilerMarkers(ProfilerMarkers.SampleGroupDefinitions)) {
            ProfilerMarkers.Creation.Begin();
            ring = CreateRingObject<T>(true, 1, 4);
            ProfilerMarkers.Creation.End();

            ring.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        }
        yield return null;
        ring.transform.position = new Vector2(0, 0);

        List<Explosion> explosions = new List<Explosion>() {
            new Explosion(-2, 0, 2, 4),
            new Explosion(2, 0, 2, 4),
        };

        using (Measure.ProfilerMarkers(ProfilerMarkers.SampleGroupDefinitions)) {
            ProfilerMarkers.ProcessExplosions.Begin();
            ee.ExecuteExplosions(explosions, DestructibleObject.FindAll(), sub);
            ProfilerMarkers.ProcessExplosions.End();
        }
        yield return null;

        // Wait to observe whether the result is correct
        yield return WaitFrames(60);

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



    public static IEnumerator CollisionTestSolids400<T>() where T : DestructibleObject {
        yield return PhysicsTest<T>(false, 1.0f, 24, 40, 10, 50, 100);
    }

    public static IEnumerator CollisionTestRings400<T>() where T : DestructibleObject {
        yield return PhysicsTest<T>(true, 1.0f, 24, 40, 10, 50, 100);
    }

    public static IEnumerator CollisionTestLargeSolids20<T>() where T : DestructibleObject {
        yield return PhysicsTest<T>(false, 10.0f, 240, 5, 4, 275, 200);
    }

    public static IEnumerator CollisionTestLargeRings20<T>() where T : DestructibleObject {
        yield return PhysicsTest<T>(true, 10.0f, 240, 5, 4, 275, 200);
    }

    public static IEnumerator ContinuousExplosionTestManySolids<T>(IExplosionExecutor ee, IPolygonSubtractor sub)
            where T : DestructibleObject {
        yield return ContinuousExplosionTest<T>(ee, sub, 1, 1, Time.fixedDeltaTime, false, 1.0f, 24, 10, 10, 100, 200);
    }

    public static IEnumerator ContinuousExplosionTestManyRings<T>(IExplosionExecutor ee, IPolygonSubtractor sub)
            where T : DestructibleObject {
        yield return ContinuousExplosionTest<T>(ee, sub, 1, 1, Time.fixedDeltaTime, true, 1.0f, 24, 10, 10, 100, 200);
    }

    public static IEnumerator OneTimeExplosionTestManySolids<T>(IExplosionExecutor ee, IPolygonSubtractor sub)
            where T : DestructibleObject {
        yield return OneTimeExplosionTest<T>(ee, sub, 100, 1, false, 1.0f, 24, 10, 10);
    }

    public static IEnumerator OneTimeExplosionTestManyRings<T>(IExplosionExecutor ee, IPolygonSubtractor sub)
            where T : DestructibleObject {
        yield return OneTimeExplosionTest<T>(ee, sub, 100, 1, true, 1.0f, 24, 10, 10);
    }

    public static IEnumerator OneTimeExplosionTestLargeComplexSolids<T>(IExplosionExecutor ee, IPolygonSubtractor sub)
            where T : DestructibleObject {
        yield return OneTimeExplosionTest<T>(ee, sub, 10, 1, false, 10.0f, 240, 2, 1);
    }

    public static IEnumerator OneTimeExplosionTestLargeComplexRings<T>(IExplosionExecutor ee, IPolygonSubtractor sub)
            where T : DestructibleObject {
        yield return OneTimeExplosionTest<T>(ee, sub, 10, 1, true, 10.0f, 240, 2, 1);
    }



    public static class CollisionSolids
    {
        [UnityTest, Performance] public static IEnumerator Poly() { yield return CollisionTestSolids400<DO_Poly_Poly>(); }
        [UnityTest, Performance] public static IEnumerator Tri() { yield return CollisionTestSolids400<DO_Poly_Tri>(); }
        [UnityTest, Performance] public static IEnumerator CHM() { yield return CollisionTestSolids400<DO_Poly_CHM>(); }
        [UnityTest, Performance] public static IEnumerator PPHM() { yield return CollisionTestSolids400<DO_Poly_PPHM>(); }
    }

    public static class CollisionRings
    {
        [UnityTest, Performance] public static IEnumerator Poly() { yield return CollisionTestRings400<DO_Poly_Poly>(); }
        [UnityTest, Performance] public static IEnumerator Tri() { yield return CollisionTestRings400<DO_Poly_Tri>(); }
        [UnityTest, Performance] public static IEnumerator CHM() { yield return CollisionTestRings400<DO_Poly_CHM>(); }
        [UnityTest, Performance] public static IEnumerator PPHM() { yield return CollisionTestRings400<DO_Poly_PPHM>(); }
    }

    public static class CollisionLargeSolids
    {
        [UnityTest, Performance] public static IEnumerator Poly() { yield return CollisionTestLargeSolids20<DO_Poly_Poly>(); }
        [UnityTest, Performance] public static IEnumerator Tri() { yield return CollisionTestLargeSolids20<DO_Poly_Tri>(); }
        [UnityTest, Performance] public static IEnumerator CHM() { yield return CollisionTestLargeSolids20<DO_Poly_CHM>(); }
        [UnityTest, Performance] public static IEnumerator PPHM() { yield return CollisionTestLargeSolids20<DO_Poly_PPHM>(); }
    }

    public static class CollisionLargeRings
    {
        [UnityTest, Performance] public static IEnumerator Poly() { yield return CollisionTestLargeRings20<DO_Poly_Poly>(); }
        [UnityTest, Performance] public static IEnumerator Tri() { yield return CollisionTestLargeRings20<DO_Poly_Tri>(); }
        [UnityTest, Performance] public static IEnumerator CHM() { yield return CollisionTestLargeRings20<DO_Poly_CHM>(); }
        [UnityTest, Performance] public static IEnumerator PPHM() { yield return CollisionTestLargeRings20<DO_Poly_PPHM>(); }
    }

    public static class Simple
    {
        [UnityTest, Performance] public static IEnumerator Poly_Poly_Clipper() { yield return SimpleTest<DO_Poly_Poly>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Poly_Tri_Clipper() { yield return SimpleTest<DO_Poly_Tri>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator Poly_CHM_Clipper() { yield return SimpleTest<DO_Poly_CHM>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator Poly_PPHM_Clipper() { yield return SimpleTest<DO_Poly_PPHM>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Tri_Tri_Clipper() { yield return SimpleTest<DO_Tri_Tri>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator CHM_CHM_Clipper() { yield return SimpleTest<DO_CHM_CHM>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator PPHM_PPHM_Clipper() { yield return SimpleTest<DO_PPHM_PPHM>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Tri_Tri_ORourke() { yield return SimpleTest<DO_Tri_Tri>(IterEE, ORourkeSub); }
        [UnityTest, Performance] public static IEnumerator CHM_CHM_ORourke() { yield return SimpleTest<DO_CHM_CHM>(IterEE, ORourkeSub); }
        //[UnityTest, Performance] public static IEnumerator PPHM_PPHM_ORourke() { yield return SimpleTest<DO_PPHM_PPHM>(IterEE, ORourkeSub); }
    }

    public static class SequentialSolids
    {
        [UnityTest, Performance] public static IEnumerator Poly_Poly_Clipper() { yield return ContinuousExplosionTestManySolids<DO_Poly_Poly>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Poly_Tri_Clipper() { yield return ContinuousExplosionTestManySolids<DO_Poly_Tri>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator Poly_CHM_Clipper() { yield return ContinuousExplosionTestManySolids<DO_Poly_CHM>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator Poly_PPHM_Clipper() { yield return ContinuousExplosionTestManySolids<DO_Poly_PPHM>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Tri_Tri_Clipper() { yield return ContinuousExplosionTestManySolids<DO_Tri_Tri>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator CHM_CHM_Clipper() { yield return ContinuousExplosionTestManySolids<DO_CHM_CHM>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator PPHM_PPHM_Clipper() { yield return ContinuousExplosionTestManySolids<DO_PPHM_PPHM>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Tri_Tri_ORourke() { yield return ContinuousExplosionTestManySolids<DO_Tri_Tri>(IterEE, ORourkeSub); }
        [UnityTest, Performance] public static IEnumerator CHM_CHM_ORourke() { yield return ContinuousExplosionTestManySolids<DO_CHM_CHM>(IterEE, ORourkeSub); }
        //[UnityTest, Performance] public static IEnumerator PPHM_PPHM_ORourke() { yield return ContinuousExplosionTestManySolids<DO_PPHM_PPHM>(IterEE, ORourkeSub); }
    }

    public static class SequentialRings
    {
        [UnityTest, Performance] public static IEnumerator Poly_Poly_Clipper() { yield return ContinuousExplosionTestManyRings<DO_Poly_Poly> (IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Poly_Tri_Clipper() { yield return ContinuousExplosionTestManyRings<DO_Poly_Tri>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator Poly_CHM_Clipper() { yield return ContinuousExplosionTestManyRings<DO_Poly_CHM>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator Poly_PPHM_Clipper() { yield return ContinuousExplosionTestManyRings<DO_Poly_PPHM>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Tri_Tri_Clipper() { yield return ContinuousExplosionTestManyRings<DO_Tri_Tri>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator CHM_CHM_Clipper() { yield return ContinuousExplosionTestManyRings<DO_CHM_CHM>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator PPHM_PPHM_Clipper() { yield return ContinuousExplosionTestManyRings<DO_PPHM_PPHM>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Tri_Tri_ORourke() { yield return ContinuousExplosionTestManyRings<DO_Tri_Tri>(IterEE, ORourkeSub); }
        [UnityTest, Performance] public static IEnumerator CHM_CHM_ORourke() { yield return ContinuousExplosionTestManyRings<DO_CHM_CHM>(IterEE, ORourkeSub); }
        //[UnityTest, Performance] public static IEnumerator PPHM_PPHM_ORourke() { yield return ContinuousExplosionTestManyRings<DO_PPHM_PPHM>(IterEE, ORourkeSub); }
    }

    public static class SimultaneousSolids
    {
        [UnityTest, Performance] public static IEnumerator Poly_Poly_Clipper() { yield return OneTimeExplosionTestManySolids<DO_Poly_Poly>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Poly_Tri_Clipper() { yield return OneTimeExplosionTestManySolids<DO_Poly_Tri>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator Poly_CHM_Clipper() { yield return OneTimeExplosionTestManySolids<DO_Poly_CHM>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator Poly_PPHM_Clipper() { yield return OneTimeExplosionTestManySolids<DO_Poly_PPHM>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Tri_Tri_Clipper() { yield return OneTimeExplosionTestManySolids<DO_Tri_Tri>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator CHM_CHM_Clipper() { yield return OneTimeExplosionTestManySolids<DO_CHM_CHM>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator PPHM_PPHM_Clipper() { yield return OneTimeExplosionTestManySolids<DO_PPHM_PPHM>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Tri_Tri_ORourke() { yield return OneTimeExplosionTestManySolids<DO_Tri_Tri>(IterEE, ORourkeSub); }
        [UnityTest, Performance] public static IEnumerator CHM_CHM_ORourke() { yield return OneTimeExplosionTestManySolids<DO_CHM_CHM>(IterEE, ORourkeSub); }
        //[UnityTest, Performance] public static IEnumerator PPHM_PPHM_ORourke() { yield return OneTimeExplosionTestManySolids<DO_PPHM_PPHM>(IterEE, ORourkeSub); }
    }

    public static class SimultaneousRings
    {
        [UnityTest, Performance] public static IEnumerator Poly_Poly_Clipper() { yield return OneTimeExplosionTestManyRings<DO_Poly_Poly>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Poly_Tri_Clipper() { yield return OneTimeExplosionTestManyRings<DO_Poly_Tri>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator Poly_CHM_Clipper() { yield return OneTimeExplosionTestManyRings<DO_Poly_CHM>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator Poly_PPHM_Clipper() { yield return OneTimeExplosionTestManyRings<DO_Poly_PPHM>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Tri_Tri_Clipper() { yield return OneTimeExplosionTestManyRings<DO_Tri_Tri>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator CHM_CHM_Clipper() { yield return OneTimeExplosionTestManyRings<DO_CHM_CHM>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator PPHM_PPHM_Clipper() { yield return OneTimeExplosionTestManyRings<DO_PPHM_PPHM>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Tri_Tri_ORourke() { yield return OneTimeExplosionTestManyRings<DO_Tri_Tri>(IterEE, ORourkeSub); }
        [UnityTest, Performance] public static IEnumerator CHM_CHM_ORourke() { yield return OneTimeExplosionTestManyRings<DO_CHM_CHM>(IterEE, ORourkeSub); }
        //[UnityTest, Performance] public static IEnumerator PPHM_PPHM_ORourke() { yield return OneTimeExplosionTestManyRings<DO_PPHM_PPHM>(IterEE, ORourkeSub); }
    }

    public static class SimultaneousLargeSolids
    {
        [UnityTest, Performance] public static IEnumerator Poly_Poly_Clipper() { yield return OneTimeExplosionTestLargeComplexSolids<DO_Poly_Poly>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Poly_Tri_Clipper() { yield return OneTimeExplosionTestLargeComplexSolids<DO_Poly_Tri>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator Poly_CHM_Clipper() { yield return OneTimeExplosionTestLargeComplexSolids<DO_Poly_CHM>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator Poly_PPHM_Clipper() { yield return OneTimeExplosionTestLargeComplexSolids<DO_Poly_PPHM>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Tri_Tri_Clipper() { yield return OneTimeExplosionTestLargeComplexSolids<DO_Tri_Tri>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator CHM_CHM_Clipper() { yield return OneTimeExplosionTestLargeComplexSolids<DO_CHM_CHM>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator PPHM_PPHM_Clipper() { yield return OneTimeExplosionTestLargeComplexSolids<DO_PPHM_PPHM>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Tri_Tri_ORourke() { yield return OneTimeExplosionTestLargeComplexSolids<DO_Tri_Tri>(IterEE, ORourkeSub); }
        [UnityTest, Performance] public static IEnumerator CHM_CHM_ORourke() { yield return OneTimeExplosionTestLargeComplexSolids<DO_CHM_CHM>(IterEE, ORourkeSub); }
        //[UnityTest, Performance] public static IEnumerator PPHM_PPHM_ORourke() { yield return OneTimeExplosionTestLargeComplexSolids<DO_PPHM_PPHM>(IterEE, ORourkeSub); }
    }

    public static class SimultaneousLargeRings
    {
        [UnityTest, Performance] public static IEnumerator Poly_Poly_Clipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Poly_Poly>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Poly_Tri_Clipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Poly_Tri>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator Poly_CHM_Clipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Poly_CHM>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator Poly_PPHM_Clipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Poly_PPHM>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Tri_Tri_Clipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_Tri_Tri>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator CHM_CHM_Clipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_CHM_CHM>(IterEE, ClipperSub); }
        [UnityTest, Performance] public static IEnumerator PPHM_PPHM_Clipper() { yield return OneTimeExplosionTestLargeComplexRings<DO_PPHM_PPHM>(IterEE, ClipperSub); }

        [UnityTest, Performance] public static IEnumerator Tri_Tri_ORourke() { yield return OneTimeExplosionTestLargeComplexRings<DO_Tri_Tri>(IterEE, ORourkeSub); }
        [UnityTest, Performance] public static IEnumerator CHM_CHM_ORourke() { yield return OneTimeExplosionTestLargeComplexRings<DO_CHM_CHM>(IterEE, ORourkeSub); }
        //[UnityTest, Performance] public static IEnumerator PPHM_PPHM_ORourke() { yield return OneTimeExplosionTestLargeComplexRings<DO_PPHM_PPHM>(IterEE, ORourkeSub); }
    }
}

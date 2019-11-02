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

public static class DestructibleTerrainTests
{
    public static class ProfilerMarkers {
        public static readonly string Namespace = "DestructibleTerrainTests";

        // DestructibleTerrainTests ProfilerMarkers
        public static readonly string CreationStr = Namespace + ".Creation";
        public static readonly ProfilerMarker Creation = new ProfilerMarker(CreationStr);

        public static readonly string ProcessExplosionsStr = Namespace + ".ProcessExplosions";
        public static readonly ProfilerMarker ProcessExplosions = new ProfilerMarker(ProcessExplosionsStr);

        public static readonly string TotalAllocatedMemoryStr = Namespace + ".TotalAllocatedMemory";

        // Unity ProfilerMarkers
        public static readonly string PhysicsStr = "FixedUpdate.Physics2DFixedUpdate";
        public static readonly ProfilerMarker Physics = new ProfilerMarker(PhysicsStr);

        public static readonly SampleGroupDefinition[] SampleGroupDefinitions = {
            // DestructibleTerrainTests ProfilerMarkers
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

    public static T CreateRingObject<T> (bool hasHole = true, float radius = 1.0f,
            float variationAmplitude = 0.1f, int numEdges = 24) where T : DestructibleObject {
        float angleStep = 2 * Mathf.PI / numEdges;

        List<Vector2> exterior = new List<Vector2>();
        List<Vector2> hole = new List<Vector2>();

        for (int i = 0; i < numEdges; ++i) {
            float rOuter = UnityEngine.Random.Range(radius - variationAmplitude, radius + variationAmplitude);
            float rInner = UnityEngine.Random.Range(radius - variationAmplitude, radius + variationAmplitude) * 0.5f;
            Vector2 dirOuter = new Vector2(Mathf.Cos(i * angleStep), -Mathf.Sin(i * angleStep));
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
        GameObject[] gos = UnityEngine.Object.FindObjectsOfType<GameObject>();
        foreach (GameObject go in gos) {
            UnityEngine.Object.Destroy(go);
        }
    }

    private static IEnumerator CreateRingsAndMeasure<T> (bool hasHoles, int columns, int rows)
            where T : DestructibleObject {
        using (Measure.ProfilerMarkers(ProfilerMarkers.SampleGroupDefinitions)) {
            ProfilerMarkers.Creation.Begin();

            float spacing = 2.5f;
            float totalWidth = columns * spacing * 0.5f;
            for (int c = 0; c < columns; ++c) {
                for (int r = 0; r < rows; ++r) {
                    Vector2 pos = new Vector2(c * spacing - totalWidth, (r + 1) * spacing);
                    CreateRingObject<T>(hasHoles).transform.position = pos;
                }
            }

            ProfilerMarkers.Creation.End();
        }
        yield return null;
    }

    public static IEnumerator PhysicsTest<T> (bool hasHoles, int columns, int rows, int warmupFrames, int captureFrames)
            where T : DestructibleObject {
        // Set a constant seed so that we get the same results every time
        UnityEngine.Random.InitState(12345);

        new GameObject("Main Camera").AddComponent<Camera>().transform.position = new Vector3(0, 50, -100);
        CreateFloor(1000);

        // Meaure the time to create all destructible objects
        yield return CreateRingsAndMeasure<T>(hasHoles, columns, rows);

        // Allow objects to collide before we measure physics
        yield return WarmupFrames(warmupFrames);

        // Measure physics
        yield return CaptureFrames(captureFrames);

        CleanUp();
    }

    public static IEnumerator ContinuousExplosionTest<T> (IExplosionExecutor explosionExecutor, IPolygonSubtractor subtractor,
            int numExplosions, float explosionRadius, float explosionInterval, bool hasHoles, int columns, int rows,
            int warmupFrames, int captureFrames) where T : DestructibleObject {
        // Set a constant seed so that we get the same results every time
        UnityEngine.Random.InitState(12345);

        new GameObject("Main Camera").AddComponent<Camera>().transform.position = new Vector3(0, 50, -100);
        CreateFloor(1000);

        // Meaure the time to create all destructible objects
        yield return CreateRingsAndMeasure<T>(hasHoles, columns, rows);

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
            explosionExecutor.ExecuteExplosions(explosions, DestructibleObject.FindAll(), subtractor);
            ProfilerMarkers.ProcessExplosions.End();
        }

        // Allow objects to collide before we measure physics
        yield return WarmupFrames(warmupFrames, explosionGenerator);

        // Measure physics
        yield return CaptureFrames(captureFrames, explosionGenerator);

        CleanUp();
    }

    public static IEnumerator OneTimeExplosionTest<T> (IExplosionExecutor explosionExecutor, IPolygonSubtractor subtractor,
            int numExplosions, float explosionRadius, float explosionInterval, bool hasHoles, int columns, int rows)
            where T : DestructibleObject {
        // Set a constant seed so that we get the same results every time
        UnityEngine.Random.InitState(12345);

        new GameObject("Main Camera").AddComponent<Camera>().transform.position = new Vector3(0, 50, -100);
        CreateFloor(1000);

        // Meaure the time to create all destructible objects
        yield return CreateRingsAndMeasure<T>(hasHoles, columns, rows);

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
            explosionExecutor.ExecuteExplosions(explosions, DestructibleObject.FindAll(), subtractor);
            ProfilerMarkers.ProcessExplosions.End();
        }
        yield return null;

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

    public static class PureCollision
    {
        public static class PolygonClippingPolygonCollider
        {
            //[UnityTest, Performance]
            public static IEnumerator Solids100() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingPolygonCollider>(false, 10, 10, 100, 200);
            }

            //[UnityTest, Performance]
            public static IEnumerator Solids200() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingPolygonCollider>(false, 20, 10, 100, 200);
            }

            [UnityTest, Performance]
            public static IEnumerator Solids400() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingPolygonCollider>(false, 40, 10, 100, 200);
            }

            //[UnityTest, Performance]
            public static IEnumerator Rings100() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingPolygonCollider>(true, 10, 10, 100, 200);
            }

            //[UnityTest, Performance]
            public static IEnumerator Rings200() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingPolygonCollider>(true, 20, 10, 100, 200);
            }

            [UnityTest, Performance]
            public static IEnumerator Rings400() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingPolygonCollider>(true, 40, 10, 100, 200);
            }
        }

        public static class PolygonClippingTriangulatedCollider
        {
            //[UnityTest, Performance]
            public static IEnumerator Solids100() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingTriangulatedCollider>(false, 10, 10, 100, 200);
            }

            //[UnityTest, Performance]
            public static IEnumerator Solids200() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingTriangulatedCollider>(false, 20, 10, 100, 200);
            }

            [UnityTest, Performance]
            public static IEnumerator Solids400() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingTriangulatedCollider>(false, 40, 10, 100, 200);
            }

            //[UnityTest, Performance]
            public static IEnumerator Rings100() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingTriangulatedCollider>(true, 10, 10, 100, 200);
            }

            //[UnityTest, Performance]
            public static IEnumerator Rings200() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingTriangulatedCollider>(true, 20, 10, 100, 200);
            }

            [UnityTest, Performance]
            public static IEnumerator Rings400() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingTriangulatedCollider>(true, 40, 10, 100, 200);
            }
        }

        public static class TriangulatedClippingTriangulatedCollider
        {
            //[UnityTest, Performance]
            public static IEnumerator Solids100() {
                yield return PhysicsTest<DestructibleObjectTriangulatedClippingTriangulatedCollider>(false, 10, 10, 100, 200);
            }

            //[UnityTest, Performance]
            public static IEnumerator Solids200() {
                yield return PhysicsTest<DestructibleObjectTriangulatedClippingTriangulatedCollider>(false, 20, 10, 100, 200);
            }

            [UnityTest, Performance]
            public static IEnumerator Solids400() {
                yield return PhysicsTest<DestructibleObjectTriangulatedClippingTriangulatedCollider>(false, 40, 10, 100, 200);
            }

            //[UnityTest, Performance]
            public static IEnumerator Rings100() {
                yield return PhysicsTest<DestructibleObjectTriangulatedClippingTriangulatedCollider>(true, 10, 10, 100, 200);
            }

            //[UnityTest, Performance]
            public static IEnumerator Rings200() {
                yield return PhysicsTest<DestructibleObjectTriangulatedClippingTriangulatedCollider>(true, 20, 10, 100, 200);
            }

            [UnityTest, Performance]
            public static IEnumerator Rings400() {
                yield return PhysicsTest<DestructibleObjectTriangulatedClippingTriangulatedCollider>(true, 40, 10, 100, 200);
            }
        }

        public static class PolygonClippingHMCollider
        {
            //[UnityTest, Performance]
            public static IEnumerator Solids100() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingHMCollider>(false, 10, 10, 100, 200);
            }

            //[UnityTest, Performance]
            public static IEnumerator Solids200() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingHMCollider>(false, 20, 10, 100, 200);
            }

            [UnityTest, Performance]
            public static IEnumerator Solids400() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingHMCollider>(false, 40, 10, 100, 200);
            }

            //[UnityTest, Performance]
            public static IEnumerator Rings100() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingHMCollider>(true, 10, 10, 100, 200);
            }

            //[UnityTest, Performance]
            public static IEnumerator Rings200() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingHMCollider>(true, 20, 10, 100, 200);
            }

            [UnityTest, Performance]
            public static IEnumerator Rings400() {
                yield return PhysicsTest<DestructibleObjectPolygonClippingHMCollider>(true, 40, 10, 100, 200);
            }
        }
    }

    public static class ExplosionPerFrameTests
    {
        public static class PolygonClippingPolygonCollider
        {
            [UnityTest, Performance]
            public static IEnumerator IterativeClipper() {
                yield return ContinuousExplosionTest<DestructibleObjectPolygonClippingPolygonCollider> (
                    IterativeExplosionExecutor.Instance, ClipperAdapter.Instance, 1, 1,
                    Time.fixedDeltaTime, true, 10, 10, 100, 200);
            }

            [UnityTest, Performance]
            public static IEnumerator BulkClipper() {
                yield return ContinuousExplosionTest<DestructibleObjectPolygonClippingPolygonCollider> (
                    BulkExplosionExecutor.Instance, ClipperAdapter.Instance, 1, 1,
                    Time.fixedDeltaTime, true, 10, 10, 100, 200);
            }

            //[UnityTest, Performance]
            public static IEnumerator TrueBulkClipper() {
                yield return ContinuousExplosionTest<DestructibleObjectPolygonClippingPolygonCollider> (
                    TrueBulkExplosionExecutor.Instance, ClipperAdapter.Instance, 1, 1,
                    Time.fixedDeltaTime, true, 10, 10, 100, 200);
            }
        }

        public static class PolygonClippingTriangulatedCollider
        {
            [UnityTest, Performance]
            public static IEnumerator IterativeClipper() {
                yield return ContinuousExplosionTest<DestructibleObjectPolygonClippingTriangulatedCollider>(
                    IterativeExplosionExecutor.Instance, ClipperAdapter.Instance, 1, 1,
                    Time.fixedDeltaTime, true, 10, 10, 100, 200);
            }

            [UnityTest, Performance]
            public static IEnumerator BulkClipper() {
                yield return ContinuousExplosionTest<DestructibleObjectPolygonClippingTriangulatedCollider>(
                    BulkExplosionExecutor.Instance, ClipperAdapter.Instance, 1, 1,
                    Time.fixedDeltaTime, true, 10, 10, 100, 200);
            }

            //[UnityTest, Performance]
            public static IEnumerator TrueBulkClipper() {
                yield return ContinuousExplosionTest<DestructibleObjectPolygonClippingTriangulatedCollider>(
                    TrueBulkExplosionExecutor.Instance, ClipperAdapter.Instance, 1, 1,
                    Time.fixedDeltaTime, true, 10, 10, 100, 200);
            }
        }

        public static class TriangulatedClippingTriangulatedCollider
        {
            [UnityTest, Performance]
            public static IEnumerator IterativeClipper() {
                yield return ContinuousExplosionTest<DestructibleObjectTriangulatedClippingTriangulatedCollider>(
                    IterativeExplosionExecutor.Instance, ClipperAdapter.Instance, 1, 1,
                    Time.fixedDeltaTime, true, 10, 10, 100, 200);
            }

            [UnityTest, Performance]
            public static IEnumerator BulkClipper() {
                yield return ContinuousExplosionTest<DestructibleObjectTriangulatedClippingTriangulatedCollider>(
                    BulkExplosionExecutor.Instance, ClipperAdapter.Instance, 1, 1,
                    Time.fixedDeltaTime, true, 10, 10, 100, 200);
            }

            //[UnityTest, Performance]
            public static IEnumerator TrueBulkClipper() {
                yield return ContinuousExplosionTest<DestructibleObjectTriangulatedClippingTriangulatedCollider>(
                    TrueBulkExplosionExecutor.Instance, ClipperAdapter.Instance, 1, 1,
                    Time.fixedDeltaTime, true, 10, 10, 100, 200);
            }
        }

        public static class PolygonClippingHMCollider
        {
            [UnityTest, Performance]
            public static IEnumerator IterativeClipper() {
                yield return ContinuousExplosionTest<DestructibleObjectPolygonClippingHMCollider>(
                    IterativeExplosionExecutor.Instance, ClipperAdapter.Instance, 1, 1,
                    Time.fixedDeltaTime, true, 10, 10, 100, 200);
            }

            [UnityTest, Performance]
            public static IEnumerator BulkClipper() {
                yield return ContinuousExplosionTest<DestructibleObjectPolygonClippingHMCollider>(
                    BulkExplosionExecutor.Instance, ClipperAdapter.Instance, 1, 1,
                    Time.fixedDeltaTime, true, 10, 10, 100, 200);
            }

            //[UnityTest, Performance]
            public static IEnumerator TrueBulkClipper() {
                yield return ContinuousExplosionTest<DestructibleObjectPolygonClippingHMCollider>(
                    TrueBulkExplosionExecutor.Instance, ClipperAdapter.Instance, 1, 1,
                    Time.fixedDeltaTime, true, 10, 10, 100, 200);
            }
        }
    }

    public static class OneTime100ExplosionTests
    {
        public static class PolygonClippingPolygonCollider
        {
            [UnityTest, Performance]
            public static IEnumerator IterativeClipper() {
                yield return OneTimeExplosionTest<DestructibleObjectPolygonClippingPolygonCollider> (
                    IterativeExplosionExecutor.Instance, ClipperAdapter.Instance, 100, 1,
                    Time.fixedDeltaTime, true, 10, 10);
            }

            [UnityTest, Performance]
            public static IEnumerator BulkClipper() {
                yield return OneTimeExplosionTest<DestructibleObjectPolygonClippingPolygonCollider> (
                    BulkExplosionExecutor.Instance, ClipperAdapter.Instance, 100, 1,
                    Time.fixedDeltaTime, true, 10, 10);
            }

            //[UnityTest, Performance]
            public static IEnumerator TrueBulkClipper() {
                yield return OneTimeExplosionTest<DestructibleObjectPolygonClippingPolygonCollider> (
                    TrueBulkExplosionExecutor.Instance, ClipperAdapter.Instance, 100, 1,
                    Time.fixedDeltaTime, true, 10, 10);
            }
        }

        public static class PolygonClippingTriangulatedCollider
        {
            [UnityTest, Performance]
            public static IEnumerator IterativeClipper() {
                yield return OneTimeExplosionTest<DestructibleObjectPolygonClippingTriangulatedCollider> (
                    IterativeExplosionExecutor.Instance, ClipperAdapter.Instance, 100, 1,
                    Time.fixedDeltaTime, true, 10, 10);
            }

            [UnityTest, Performance]
            public static IEnumerator BulkClipper() {
                yield return OneTimeExplosionTest<DestructibleObjectPolygonClippingTriangulatedCollider> (
                    BulkExplosionExecutor.Instance, ClipperAdapter.Instance, 100, 1,
                    Time.fixedDeltaTime, true, 10, 10);
            }

            //[UnityTest, Performance]
            public static IEnumerator TrueBulkClipper() {
                yield return OneTimeExplosionTest<DestructibleObjectPolygonClippingTriangulatedCollider> (
                    TrueBulkExplosionExecutor.Instance, ClipperAdapter.Instance, 100, 1,
                    Time.fixedDeltaTime, true, 10, 10);
            }
        }

        public static class TriangulatedClippingTriangulatedCollider
        {
            [UnityTest, Performance]
            public static IEnumerator IterativeClipper() {
                yield return OneTimeExplosionTest<DestructibleObjectTriangulatedClippingTriangulatedCollider> (
                    IterativeExplosionExecutor.Instance, ClipperAdapter.Instance, 100, 1,
                    Time.fixedDeltaTime, true, 10, 10);
            }

            [UnityTest, Performance]
            public static IEnumerator BulkClipper() {
                yield return OneTimeExplosionTest<DestructibleObjectTriangulatedClippingTriangulatedCollider> (
                    BulkExplosionExecutor.Instance, ClipperAdapter.Instance, 100, 1,
                    Time.fixedDeltaTime, true, 10, 10);
            }

            //[UnityTest, Performance]
            public static IEnumerator TrueBulkClipper() {
                yield return OneTimeExplosionTest<DestructibleObjectTriangulatedClippingTriangulatedCollider> (
                    TrueBulkExplosionExecutor.Instance, ClipperAdapter.Instance, 100, 1,
                    Time.fixedDeltaTime, true, 10, 10);
            }
        }

        public static class PolygonClippingHMCollider
        {
            [UnityTest, Performance]
            public static IEnumerator IterativeClipper() {
                yield return OneTimeExplosionTest<DestructibleObjectPolygonClippingHMCollider>(
                    IterativeExplosionExecutor.Instance, ClipperAdapter.Instance, 100, 1,
                    Time.fixedDeltaTime, true, 10, 10);
            }

            [UnityTest, Performance]
            public static IEnumerator BulkClipper() {
                yield return OneTimeExplosionTest<DestructibleObjectPolygonClippingHMCollider>(
                    BulkExplosionExecutor.Instance, ClipperAdapter.Instance, 100, 1,
                    Time.fixedDeltaTime, true, 10, 10);
            }

            //[UnityTest, Performance]
            public static IEnumerator TrueBulkClipper() {
                yield return OneTimeExplosionTest<DestructibleObjectPolygonClippingHMCollider>(
                    TrueBulkExplosionExecutor.Instance, ClipperAdapter.Instance, 100, 1,
                    Time.fixedDeltaTime, true, 10, 10);
            }
        }
    }
}

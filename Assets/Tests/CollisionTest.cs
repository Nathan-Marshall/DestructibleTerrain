using DestrictubleTerrain;
using NUnit.Framework;
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

        // Custom ProfilerMarkers
        public static readonly string CreationStr = Namespace + ".Creation";
        public static readonly ProfilerMarker Creation = new ProfilerMarker(CreationStr);

        public static readonly string TotalAllocatedMemoryStr = Namespace + ".TotalAllocatedMemory";

        // Unity ProfilerMarkers
        public static readonly string PhysicsStr = "FixedUpdate.Physics2DFixedUpdate";
        public static readonly ProfilerMarker Physics = new ProfilerMarker(PhysicsStr);

        public static readonly SampleGroupDefinition[] SampleGroupDefinitions = {
            new SampleGroupDefinition(CreationStr),

            new SampleGroupDefinition(PhysicsStr),
    };
    }

    public static GameObject CreateFloor(float length = 100, float thickness = 5) {
        GameObject go = new GameObject();

        BoxCollider2D boxCollider = go.AddComponent<BoxCollider2D>();
        boxCollider.size = new Vector2(1, 1);
        boxCollider.offset = new Vector2(0.5f, -0.5f);

        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[] {
            new Vector3(0, -1),
            new Vector3(0,  0),
            new Vector3(1,  0),
            new Vector3(1, -1)
        };
        mesh.triangles = new int[] { 0, 1, 2, 2, 3, 0 };

        MeshFilter meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        go.AddComponent<MeshRenderer>();

        go.transform.localScale = new Vector3(length, thickness, 1);

        return go;
    }

    public static DestructibleObject CreateRingObject(bool hasHole = true, float radius = 1.0f, float variationAmplitude = 0.1f, int numEdges = 24) {
        float angleStep = 2 * Mathf.PI / numEdges;

        List<Vector2> exterior = new List<Vector2>();
        List<Vector2> hole = new List<Vector2>();

        for (int i = 0; i < numEdges; ++i) {
            float rOuter = Random.Range(radius - variationAmplitude, radius + variationAmplitude);
            float rInner = Random.Range(radius - variationAmplitude, radius + variationAmplitude) * 0.5f;
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
        DestructibleObject destructibleObject = go.AddComponent<DestructibleObject>();
        destructibleObject.ApplyPolygon(polygon);

        return destructibleObject;
    }

    // Destroys all GameObjects.
    // This should be called at the end of each test.
    public static void CleanUp () {
        GameObject[] gos = Object.FindObjectsOfType<GameObject>();
        foreach (GameObject go in gos) {
            Object.Destroy(go);
        }
    }

    public static IEnumerator PhysicsTest (bool hasHoles, int rows, int columns, int warmupFrames, int captureFrames) {
        new GameObject("Main Camera").AddComponent<Camera>().transform.position = new Vector3(10, 0, -100);
        CreateFloor(1000).transform.position = new Vector2(-500, -2.0f);

        // Meaure the time to create all destructible objects
        using (Measure.ProfilerMarkers(ProfilerMarkers.SampleGroupDefinitions)) {
            ProfilerMarkers.Creation.Begin();

            for (int c = 0; c < columns; ++c) {
                for (int r = 0; r < rows; ++r) {
                    Vector2 pos = new Vector2(c * 2.5f, r * 2.5f);
                    CreateRingObject(hasHoles).transform.position = pos;
                }
            }

            ProfilerMarkers.Creation.End();
        }
        yield return null;

        // Allow objects to collide before we measure physics
        for (int updates = 0; updates < warmupFrames; ++updates) {
            yield return new WaitForFixedUpdate();
        }

        // Measure physics
        using (Measure.ProfilerMarkers(ProfilerMarkers.SampleGroupDefinitions)) {
            for (int updates = 0; updates < captureFrames; ++updates) {
                Measure.Custom(new SampleGroupDefinition(ProfilerMarkers.TotalAllocatedMemoryStr, SampleUnit.Megabyte), Profiler.GetTotalAllocatedMemoryLong() / 1048576f);
                yield return new WaitForFixedUpdate();
            }
        }

        CleanUp();
    }

    [UnityTest, Performance]
    public static IEnumerator CollisionTest100Solids() {
        yield return PhysicsTest(false, 10, 10, 100, 200);
    }

    [UnityTest, Performance]
    public static IEnumerator CollisionTest400Solids() {
        yield return PhysicsTest(false, 20, 20, 100, 200);
    }

    [UnityTest, Performance]
    public static IEnumerator CollisionTest100Rings() {
        yield return PhysicsTest(true, 10, 10, 100, 200);
    }

    [UnityTest, Performance]
    public static IEnumerator CollisionTest400Rings() {
        yield return PhysicsTest(true, 20, 20, 100, 200);
    }
}

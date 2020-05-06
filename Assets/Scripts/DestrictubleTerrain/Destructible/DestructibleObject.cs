using ClipperLib;
using DestructibleTerrain;
using DestructibleTerrain.Clipping;
using DestructibleTerrain.Triangulation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TriangleNet.Geometry;
using UnityEngine;

namespace DestructibleTerrain.Destructible
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(PolygonCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    [ExecuteAlways]
    abstract public class DestructibleObject : MonoBehaviour
    {
        // Static

        public static string DestructibleObjectTag = "DestructibleObject";

        // Destroy this GameObject if the mass drops below this threshold after finished updating collider
        public static float MassCutoff = 0.02f;

        public static IEnumerable<DestructibleObject> FindAll() {
            return GameObject.FindGameObjectsWithTag(DestructibleObjectTag).Select(go => go.GetComponent<DestructibleObject>());
        }

        // Non-static

        protected DTMesh dtRenderMesh;

        protected virtual void Start() {
            tag = DestructibleObjectTag;
            GetComponent<Rigidbody2D>().useAutoMass = true;

            // Assign default polygon when this component is attached in the editor
            if (GetTransformedPolygonList() == null && Application.isEditor) {
                ApplyPolygonList(new List<DTPolygon>() {
                    new DTPolygon(
                        new List<Vector2> {
                            new Vector2(-1, -1),
                            new Vector2( 1, -1),
                            new Vector2( 1,  1),
                            new Vector2(-1,  1)
                        },
                        new List<List<Vector2>> {
                            new List<Vector2> {
                                new Vector2(-0.75f, -0.75f),
                                new Vector2(-0.75f,  0.75f),
                                new Vector2( 0.75f,  0.75f),
                                new Vector2( 0.75f, -0.75f)
                            }
                        })
                });
            }
        }

        public Vector2 TransformPoint(Vector2 p) {
            var v3 = transform.TransformPoint(p.x, p.y, 0);
            return new Vector2(v3.x, v3.y);
        }

        public List<Vector2> TransformPoints(IEnumerable<Vector2> points) {
            return points.Select(TransformPoint).ToList();
        }

        public Vector2 InverseTransformPoint(Vector2 p) {
            var v3 = transform.InverseTransformPoint(p.x, p.y, 0);
            return new Vector2(v3.x, v3.y);
        }

        public List<Vector2> InverseTransformPoints(IEnumerable<Vector2> points) {
            return points.Select(InverseTransformPoint).ToList();
        }

        public abstract List<DTPolygon> GetTransformedPolygonList();

        // Applies the collider and mesh in whatever format the DestructibleObject subclass stores its data
        public abstract void ApplyPolygonList(List<DTPolygon> dtPolygonList);

        public abstract void ApplyTransformedPolygonList(List<DTPolygon> transformedPolygonList);

        protected void ApplyCollider(DTPolygon dtPolygon) {
            PolygonCollider2D polygonCollider = GetComponent<PolygonCollider2D>();
            polygonCollider.pathCount = 1 + dtPolygon.Holes.Count;
            polygonCollider.SetPath(0, dtPolygon.Contour);
            for (int i = 0; i < dtPolygon.Holes.Count; i++) {
                polygonCollider.SetPath(i + 1, dtPolygon.Holes[i]);
            }

            if (GetComponent<Rigidbody2D>().mass < MassCutoff) {
                Destroy(gameObject);
            }
        }
        
        protected void ApplyCollider(DTConvexPolygroup polygroup) {
            PolygonCollider2D polygonCollider = GetComponent<PolygonCollider2D>();
            polygonCollider.pathCount = polygroup.Count;
            for (int i = 0; i < polygroup.Count; i++) {
                polygonCollider.SetPath(i, polygroup[i]);
            }

            if (GetComponent<Rigidbody2D>().mass < MassCutoff) {
                Destroy(gameObject);
            }
        }

        protected void ApplyCollider(DTMesh dtMesh) {
            PolygonCollider2D polygonCollider = GetComponent<PolygonCollider2D>();
            polygonCollider.pathCount = dtMesh.Partitions.Count;
            int i = 0;
            foreach (var partition in dtMesh.Partitions) {
                polygonCollider.SetPath(i++, partition.Select(vIndex => dtMesh.Vertices[vIndex]).ToArray());
            }

            if (GetComponent<Rigidbody2D>().mass < MassCutoff) {
                Destroy(gameObject);
            }
        }

        protected void ApplyRenderMesh(DTMesh dtMesh) {
            if (dtRenderMesh == dtMesh) {
                return;
            }
            dtRenderMesh = dtMesh;

            DTProfilerMarkers.ApplyRenderMesh.Begin();

            MeshFilter mf = GetComponent<MeshFilter>();
            PolygonCollider2D polygonCollider = GetComponent<PolygonCollider2D>();
            if (Application.isEditor && !Application.isPlaying) {
                // Use sharedMesh in Editor to avoid leaking meshes
                mf.sharedMesh = polygonCollider.CreateMesh(false, false);
                Vector3 s = transform.localScale;
                mf.sharedMesh.vertices = mf.sharedMesh.vertices.Select(v => new Vector3(v.x / s.x, v.y / s.y, 0)).ToArray();
            } else {
                // Use mesh in Play mode because otherwise the mesh is not immediately generated (creates race condition)
                mf.mesh = polygonCollider.CreateMesh(false, false);
                Vector3 s = transform.localScale;
                mf.mesh.vertices = mf.sharedMesh.vertices.Select(v => new Vector3(v.x / s.x, v.y / s.y, 0)).ToArray();
            }

            DTProfilerMarkers.ApplyRenderMesh.End();
        }

        protected ITriangulator GetTriangulator() {
            return TriangleNetTriangulator.Instance;
        }
    }
}
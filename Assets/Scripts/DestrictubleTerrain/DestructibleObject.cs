using ClipperLib;
using DestrictubleTerrain;
using DestrictubleTerrain.Clipping;
using DestrictubleTerrain.Triangulation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TriangleNet.Geometry;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(PolygonCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
[ExecuteAlways]
public class DestructibleObject : MonoBehaviour
{
    // Static

    public static string DestructibleObjectTag = "DestructibleObject";

    // Destroy this GameObject if the mass drops below this threshold after finished updating collider
    public static float MassCutoff = 0.02f;

    public static IEnumerable<DestructibleObject> FindAll() {
        return GameObject.FindGameObjectsWithTag(DestructibleObjectTag).Select(go => go.GetComponent<DestructibleObject>());
    }

    // Non-static

    private DTPolygon dtPolygon;

    private DTMesh dtMesh;

    void Start() {
        tag = DestructibleObjectTag;
        GetComponent<Rigidbody2D>().useAutoMass = true;

        // Assign default polygon when this component is attached in the editor
        if (dtPolygon == null && Application.isEditor) {
            ApplyPolygon(new DTPolygon(
                new List<Vector2> {
                new Vector2(-1, -1),
                new Vector2(-1,  1),
                new Vector2( 1,  1),
                new Vector2( 1, -1)
                },
                new List<List<Vector2>> {
                new List<Vector2> {
                    new Vector2(-0.75f, -0.75f),
                    new Vector2( 0.75f, -0.75f),
                    new Vector2( 0.75f,  0.75f),
                    new Vector2(-0.75f,  0.75f)
                }
                }));
        }
    }

    public DTPolygon GetTransformedPolygon() {
        Vector2 transform2DPoint(Vector2 p) {
            var v3 = transform.TransformPoint(p.x, p.y, 0);
            return new Vector2(v3.x, v3.y);
        }
        return new DTPolygon(
            dtPolygon.Contour.Select(transform2DPoint).ToList(),
            dtPolygon.Holes.Select(hole => hole.Select(transform2DPoint).ToList()).ToList());
    }

    public void ApplyPolygon(DTPolygon dtPolygon) {
        if (this.dtPolygon == dtPolygon) {
            return;
        }

        this.dtPolygon = dtPolygon;

        // Collider from polygon
        ApplyCollider(dtPolygon);

        // Triangulate polygon
        dtMesh = TriangleNetAdapter.Instance.Triangulate(dtPolygon);

        // Create mesh from triangles
        ApplyMesh(dtMesh);
    }

    public void ApplyTransformedPolygon(DTPolygon transformedPolygon) {
        Vector2 inverseTransform2DPoint(Vector2 p) {
            var v3 = transform.InverseTransformPoint(p.x, p.y, 0);
            return new Vector2(v3.x, v3.y);
        }
        DTPolygon dtPolygon = new DTPolygon(
            transformedPolygon.Contour.Select(inverseTransform2DPoint).ToList(),
            transformedPolygon.Holes.Select(hole => hole.Select(inverseTransform2DPoint).ToList()).ToList());

        ApplyPolygon(dtPolygon);
    }

    private void ApplyCollider(DTPolygon dtPolygon) {
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

    private void ApplyMesh(DTMesh dtMesh) {
        MeshFilter mf = GetComponent<MeshFilter>();
        mf.sharedMesh = new Mesh() {
            vertices = dtMesh.Vertices.Select(v => new Vector3(v.x, v.y)).ToArray(),
            uv = dtMesh.Vertices.ToArray(),
            triangles = dtMesh.Partitions.SelectMany(t => t.GetRange(0, 3)).ToArray()
        };
    }
}

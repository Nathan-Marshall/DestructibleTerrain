using ClipperLib;
using DestrictubleTerrain;
using DestrictubleTerrain.Clipping;
using DestrictubleTerrain.Triangulation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TriangleNet.Geometry;
using UnityEngine;

public class DestructibleObjectPolygonClippingPolygonCollider : DestructibleObject
{
    private DTPolygon dtPolygon;

    protected override void Start() {
        base.Start();

        // Assign default polygon when this component is attached in the editor
        if (dtPolygon == null && Application.isEditor) {
            ApplyPolygonList(new List<DTPolygon>() {
                new DTPolygon(
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
                    })
            });
        }
    }

    public override List<DTPolygon> GetTransformedPolygonList() {
        return new List<DTPolygon>() {
            new DTPolygon(
                dtPolygon.Contour.Select(TransformPoint).ToList(),
                dtPolygon.Holes.Select(hole => hole.Select(TransformPoint).ToList()).ToList())
        };
    }

    public override void ApplyPolygonList(List<DTPolygon> dtPolygonList) {
        if (dtPolygon == dtPolygonList[0]) {
            return;
        }
        dtPolygon = dtPolygonList[0];

        // Collider from polygon
        ApplyCollider(dtPolygon);

        // Create mesh from triangulated polygon
        ApplyRenderMesh(TriangleNetAdapter.Instance.PolygonToMesh(dtPolygon));
    }

    public override void ApplyTransformedPolygonList(List<DTPolygon> transformedPolygonList) {
        List<DTPolygon> dtPolygonList = new List<DTPolygon>() {
            new DTPolygon(
                transformedPolygonList[0].Contour.Select(InverseTransformPoint).ToList(),
                transformedPolygonList[0].Holes.Select(hole => hole.Select(InverseTransformPoint).ToList()).ToList())
        };

        ApplyPolygonList(dtPolygonList);
    }
}

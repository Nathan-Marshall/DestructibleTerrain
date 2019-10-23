using ClipperLib;
using DestrictubleTerrain;
using DestrictubleTerrain.Clipping;
using DestrictubleTerrain.Triangulation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TriangleNet.Geometry;
using UnityEngine;

public class DestructibleObjectTriangulatedClippingTriangulatedCollider : DestructibleObject
{
    private List<DTPolygon> dtPolygonList;

    protected override void Start() {
        base.Start();

        // Assign default polygon when this component is attached in the editor
        if (dtPolygonList == null && Application.isEditor) {
            ApplyPolygonList(TriangleNetAdapter.Instance.PolygonToTriangleList(new DTPolygon(
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
                })));
        }
    }

    public override List<DTPolygon> GetTransformedPolygonList() {
        // Assume no holes in polygon list
        return dtPolygonList.Select(poly => new DTPolygon(
            poly.Contour.Select(TransformPoint).ToList())).ToList();
    }

    public override void ApplyPolygonList(List<DTPolygon> clippedPolygonList) {
        dtPolygonList = new List<DTPolygon>();
        // These polygons could potentially be concave or have holes, so we will triangulate each one before applying
        foreach (DTPolygon poly in clippedPolygonList) {
            var triangleList = TriangleNetAdapter.Instance.PolygonToTriangleList(poly);
            foreach (DTPolygon triangle in triangleList) {
                dtPolygonList.Add(triangle);
            }
        }

        // Collider from polygon
        ApplyCollider(dtPolygonList);

        // Create mesh from triangulated polygon
        ApplyRenderMesh(DTUtility.SimplePolygonListToMesh(dtPolygonList));
    }

    public override void ApplyTransformedPolygonList(List<DTPolygon> transformedPolygonList) {
        // Assume no holes in polygon list
        var newPolyList = transformedPolygonList.Select(poly => new DTPolygon(
            poly.Contour.Select(InverseTransformPoint).ToList())).ToList();

        ApplyPolygonList(newPolyList);
    }
}

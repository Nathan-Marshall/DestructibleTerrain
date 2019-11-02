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
    public class DestructibleObjectTriangulatedClippingTriangulatedCollider : DestructibleObject
    {
        private DTConvexPolygonGroup dtPolyGroup;

        protected override void Start() {
            base.Start();

            // Assign default polygon when this component is attached in the editor
            if (dtPolyGroup == null && Application.isEditor) {
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
                    })).ToPolygonList());
            }
        }

        public override List<DTPolygon> GetTransformedPolygonList() {
            // Assume no holes in polygon list
            return dtPolyGroup.Select(poly => new DTPolygon(
                poly.Select(TransformPoint).ToList())).ToList();
        }

        public override void ApplyPolygonList(List<DTPolygon> clippedPolygonList) {
            // The clipped polygons could potentially be concave or have holes, so we will triangulate each one before applying
            DTProfileMarkers.Triangulation.Begin();
            dtPolyGroup = DTUtility.TriangulateAll(clippedPolygonList, TriangleNetAdapter.Instance);
            DTProfileMarkers.Triangulation.End();

            // Collider from polygon
            ApplyCollider(dtPolyGroup);

            // Create mesh from triangulated polygon
            ApplyRenderMesh(dtPolyGroup.ToMesh());
        }

        public override void ApplyTransformedPolygonList(List<DTPolygon> transformedPolygonList) {
            // Assume no holes in polygon list
            var newPolyList = transformedPolygonList.Select(poly => new DTPolygon(
                poly.Contour.Select(InverseTransformPoint).ToList())).ToList();

            ApplyPolygonList(newPolyList);
        }
    }
}
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
    // DestructibleObject with triangulated clipping, triangulated collider
    public class DO_Triangle_Clip_Collide : DestructibleObject
    {
        private DTConvexPolygroup dtPolygroup;

        public override List<DTPolygon> GetTransformedPolygonList() {
            if (dtPolygroup == null) {
                return null;
            }

            DTProfilerMarkers.Transformation.Begin();
            // Assume no holes in polygon list
            List<DTPolygon> polygonList = dtPolygroup.Select(
                poly => new DTPolygon(poly.Select(TransformPoint).ToList())).ToList();
            DTProfilerMarkers.Transformation.End();
            return polygonList;
        }

        public override void ApplyPolygonList(List<DTPolygon> clippedPolygonList) {
            // The clipped polygons could potentially be concave or have holes, so we will triangulate each one before applying
            DTProfilerMarkers.Triangulation.Begin();
            dtPolygroup = DTUtility.TriangulateAll(clippedPolygonList, GetTriangulator());
            DTProfilerMarkers.Triangulation.End();

            // Collider from polygon
            DTProfilerMarkers.ApplyCollider.Begin();
            ApplyCollider(dtPolygroup);
            DTProfilerMarkers.ApplyCollider.End();

            // Create mesh from triangulated polygon
            ApplyRenderMesh(dtPolygroup.ToMesh());
        }

        public override void ApplyTransformedPolygonList(List<DTPolygon> transformedPolygonList) {
            DTProfilerMarkers.Transformation.Begin();
            var newPolyList = transformedPolygonList.Select(poly => new DTPolygon(
                poly.Contour.Select(InverseTransformPoint).ToList(),
                poly.Holes.Select(hole => hole.Select(InverseTransformPoint).ToList()).ToList())).ToList();
            DTProfilerMarkers.Transformation.End();

            ApplyPolygonList(newPolyList);
        }
    }
}
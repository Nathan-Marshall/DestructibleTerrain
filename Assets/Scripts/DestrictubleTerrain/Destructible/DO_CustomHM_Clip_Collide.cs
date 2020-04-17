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
    // DestructibleObject with custom Hertel-Mehlhorn partitioned clipping, custom Hertel-Mehlhorn partitioned collider
    public class DO_CustomHM_Clip_Collide : DestructibleObject
    {
        private DTMesh hmMesh;

        public override List<DTPolygon> GetTransformedPolygonList() {
            if (hmMesh == null) {
                return null;
            }

            DTProfilerMarkers.Transformation.Begin();
            // Assume no holes in polygon list
            List<DTPolygon> polygonList = hmMesh.Partitions.Select(
                poly => new DTPolygon(poly.Select(i => TransformPoint(hmMesh.Vertices[i])).ToList())).ToList();
            DTProfilerMarkers.Transformation.End();
            return polygonList;
        }

        public override void ApplyPolygonList(List<DTPolygon> clippedPolygonList) {
            // The clipped polygons could potentially be concave or have holes, so we will triangulate each one before applying
            DTProfilerMarkers.Triangulation.Begin();
            DTConvexPolygroup triangleList = DTUtility.TriangulateAll(clippedPolygonList, GetTriangulator());
            DTProfilerMarkers.Triangulation.End();

            // Our Hertel-Mehlhorn implementation takes a DTMesh, so convert before instead of after
            DTMesh triangulatedMesh = triangleList.ToMesh();

            // Collider from polygon
            DTProfilerMarkers.HertelMehlhorn.Begin();
            hmMesh = HertelMehlhorn.CustomHM.Instance.ExecuteToMesh(triangulatedMesh);
            DTProfilerMarkers.HertelMehlhorn.End();

            // Collider from polygon
            DTProfilerMarkers.ApplyCollider.Begin();
            ApplyCollider(hmMesh);
            DTProfilerMarkers.ApplyCollider.End();

            // Create mesh from triangulated polygon
            ApplyRenderMesh(triangulatedMesh);
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
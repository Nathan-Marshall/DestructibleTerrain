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


            DTProfileMarkers.Transformation.Begin();
            // Assume no holes in polygon list
            List<DTPolygon> polygonList = hmMesh.Partitions.Select(
                poly => new DTPolygon(poly.Select(i => TransformPoint(hmMesh.Vertices[i])).ToList())).ToList();
            DTProfileMarkers.Transformation.End();
            return polygonList;
        }

        public override void ApplyPolygonList(List<DTPolygon> clippedPolygonList) {
            // The clipped polygons could potentially be concave or have holes, so we will triangulate each one before applying
            DTProfileMarkers.Triangulation.Begin();
            DTMesh triangulatedMesh = DTUtility.TriangulateAll(clippedPolygonList, GetTriangulator()).ToMesh();
            DTProfileMarkers.Triangulation.End();

            // Collider from polygon
            DTProfileMarkers.HertelMehlhorn.Begin();
            hmMesh = HertelMehlhorn.CustomHM.Instance.ExecuteToMesh(triangulatedMesh);
            DTProfileMarkers.HertelMehlhorn.End();

            // Collider from polygon
            ApplyCollider(hmMesh);

            // Create mesh from triangulated polygon
            ApplyRenderMesh(triangulatedMesh);
        }

        public override void ApplyTransformedPolygonList(List<DTPolygon> transformedPolygonList) {
            DTProfileMarkers.Transformation.Begin();
            var newPolyList = transformedPolygonList.Select(poly => new DTPolygon(
                poly.Contour.Select(InverseTransformPoint).ToList(),
                poly.Holes.Select(hole => hole.Select(InverseTransformPoint).ToList()).ToList())).ToList();
            DTProfileMarkers.Transformation.End();

            ApplyPolygonList(newPolyList);
        }
    }
}
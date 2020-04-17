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
    // DestructibleObject with PolyPartition Hertel-Mehlhorn partitioned clipping, PolyPartition Hertel-Mehlhorn partitioned collider
    public class DO_PolyPartitionHM_Clip_Collide : DestructibleObject
    {
        private DTConvexPolygroup hmPolygroup;

        public override List<DTPolygon> GetTransformedPolygonList() {
            if (hmPolygroup == null) {
                return null;
            }

            DTProfileMarkers.Transformation.Begin();
            // Assume no holes in polygon list
            List<DTPolygon> polygonList = hmPolygroup.Select(
                poly => new DTPolygon(poly.Select(TransformPoint).ToList())).ToList();
            DTProfileMarkers.Transformation.End();
            return polygonList;
        }

        public override void ApplyPolygonList(List<DTPolygon> clippedPolygonList) {
            // The clipped polygons could potentially be concave or have holes, so we will triangulate each one before applying
            DTProfileMarkers.Triangulation.Begin();
            DTConvexPolygroup triangulatedPolygroup = DTUtility.TriangulateAll(clippedPolygonList, GetTriangulator());
            DTProfileMarkers.Triangulation.End();

            // Collider from polygon
            DTProfileMarkers.HertelMehlhorn.Begin();
            hmPolygroup = HertelMehlhorn.PolyPartitionHM.Instance.ExecuteToPolygroup(triangulatedPolygroup);
            DTProfileMarkers.HertelMehlhorn.End();

            // Collider from polygon
            DTProfileMarkers.ApplyCollider.Begin();
            ApplyCollider(hmPolygroup);
            DTProfileMarkers.ApplyCollider.End();

            // Create mesh from triangulated polygon
            ApplyRenderMesh(triangulatedPolygroup.ToMesh());
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
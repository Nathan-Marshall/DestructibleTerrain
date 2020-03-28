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
    public class DestructibleObjectPolyPartitionHMClippingPolyPartitionHMCollider : DestructibleObject
    {
        private DTConvexPolygroup hmPolygroup;

        public override List<DTPolygon> GetTransformedPolygonList() {
            if (hmPolygroup == null) {
                return null;
            }

            // Assume no holes in polygon list
            return hmPolygroup.Select(poly => new DTPolygon(poly.Select(TransformPoint).ToList())).ToList();
        }

        public override void ApplyPolygonList(List<DTPolygon> clippedPolygonList) {
            // The clipped polygons could potentially be concave or have holes, so we will triangulate each one before applying
            DTProfileMarkers.Triangulation.Begin();
            DTConvexPolygroup triangulatedPolygroup = DTUtility.TriangulateAll(clippedPolygonList, GetTriangulator());
            DTMesh dtMesh = triangulatedPolygroup.ToMesh();
            DTProfileMarkers.Triangulation.End();

            // Collider from polygon
            DTProfileMarkers.HertelMehlhorn.Begin();
            hmPolygroup = HertelMehlhorn.PolyPartitionHM.Instance.ExecuteToPolygroup(triangulatedPolygroup);
            DTProfileMarkers.HertelMehlhorn.End();

            // Collider from polygon
            ApplyCollider(hmPolygroup);

            // Create mesh from triangulated polygon
            ApplyRenderMesh(dtMesh);
        }

        public override void ApplyTransformedPolygonList(List<DTPolygon> transformedPolygonList) {
            // Assume no holes in polygon list
            var newPolyList = transformedPolygonList.Select(poly => new DTPolygon(
                poly.Contour.Select(InverseTransformPoint).ToList())).ToList();

            ApplyPolygonList(newPolyList);
        }
    }
}
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
    // DestructibleObject with polygon clipping, PolyPartition Hertel-Mehlhorn partitioned collider
    public class DO_Polygon_Clip_PolyPartitionHM_Collide : DestructibleObject
    {
        private DTPolygon dtPolygon;

        public override List<DTPolygon> GetTransformedPolygonList() {
            if (dtPolygon == null) {
                return null;
            }

            DTProfileMarkers.Transformation.Begin();
            List<DTPolygon> polygonList = new List<DTPolygon>() {
                new DTPolygon(
                    dtPolygon.Contour.Select(TransformPoint).ToList(),
                    dtPolygon.Holes.Select(hole => hole.Select(TransformPoint).ToList()).ToList())
            };
            DTProfileMarkers.Transformation.End();
            return polygonList;
        }

        public override void ApplyPolygonList(List<DTPolygon> dtPolygonList) {
            if (dtPolygon == dtPolygonList[0]) {
                return;
            }
            dtPolygon = dtPolygonList[0];

            DTProfileMarkers.Triangulation.Begin();
            DTMesh triMesh = GetTriangulator().PolygonToMesh(dtPolygon);
            DTProfileMarkers.Triangulation.End();

            DTConvexPolygroup triPolygroup = triMesh.ToPolygroup();

            // Collider from polygon
            DTProfileMarkers.HertelMehlhorn.Begin();
            DTConvexPolygroup hmPolygroup = HertelMehlhorn.PolyPartitionHM.Instance.ExecuteToPolygroup(triPolygroup);
            DTProfileMarkers.HertelMehlhorn.End();

            DTProfileMarkers.ApplyCollider.Begin();
            ApplyCollider(hmPolygroup);
            DTProfileMarkers.ApplyCollider.End();

            // Create mesh from triangulated polygon
            ApplyRenderMesh(triMesh);
        }

        public override void ApplyTransformedPolygonList(List<DTPolygon> transformedPolygonList) {
            DTProfileMarkers.Transformation.Begin();
            List<DTPolygon> dtPolygonList = new List<DTPolygon>() {
                new DTPolygon(
                    transformedPolygonList[0].Contour.Select(InverseTransformPoint).ToList(),
                    transformedPolygonList[0].Holes.Select(hole => hole.Select(InverseTransformPoint).ToList()).ToList())
            };
            DTProfileMarkers.Transformation.End();

            ApplyPolygonList(dtPolygonList);
        }
    }
}
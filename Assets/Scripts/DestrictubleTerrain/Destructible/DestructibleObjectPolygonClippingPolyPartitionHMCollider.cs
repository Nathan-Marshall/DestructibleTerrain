﻿using ClipperLib;
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
    public class DestructibleObjectPolygonClippingPolyPartitionHMCollider : DestructibleObject
    {
        private DTPolygon dtPolygon;

        public override List<DTPolygon> GetTransformedPolygonList() {
            if (dtPolygon == null) {
                return null;
            }

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

            DTProfileMarkers.Triangulation.Begin();
            DTMesh dtMesh = GetTriangulator().PolygonToMesh(dtPolygon);
            DTProfileMarkers.Triangulation.End();

            // Collider from polygon
            DTProfileMarkers.HertelMehlhorn.Begin();
            DTConvexPolygroup hmPolygroup = HertelMehlhorn.PolyPartitionHM.Instance.ExecuteToPolygroup(dtMesh);
            DTProfileMarkers.HertelMehlhorn.End();

            ApplyCollider(hmPolygroup);

            // Create mesh from triangulated polygon
            ApplyRenderMesh(dtMesh);
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
}
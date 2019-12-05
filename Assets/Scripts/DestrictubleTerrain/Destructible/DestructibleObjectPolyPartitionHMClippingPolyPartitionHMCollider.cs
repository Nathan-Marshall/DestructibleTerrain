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
    public abstract class DestructibleObjectPolyPartitionHMClippingCustomHMCollider : DestructibleObject
    {
        private DTConvexPolygonGroup hmPolyGroup;

        public override List<DTPolygon> GetTransformedPolygonList() {
            if (hmPolyGroup == null) {
                return null;
            }

            // Assume no holes in polygon list
            return hmPolyGroup.Select(poly => new DTPolygon(poly.Select(TransformPoint).ToList())).ToList();
        }

        public override void ApplyPolygonList(List<DTPolygon> clippedPolygonList) {
            // The clipped polygons could potentially be concave or have holes, so we will triangulate each one before applying
            DTProfileMarkers.Triangulation.Begin();
            DTConvexPolygonGroup triangulatedPolyGroup = DTUtility.TriangulateAll(clippedPolygonList, GetTriangulator());
            DTMesh dtMesh = triangulatedPolyGroup.ToMesh();
            DTProfileMarkers.Triangulation.End();

            // Collider from polygon
            DTProfileMarkers.HertelMehlhorn.Begin();
            hmPolyGroup = HertelMehlhorn.PolyPartitionHM.Instance.ExecuteToPolyGroup(triangulatedPolyGroup);
            DTProfileMarkers.HertelMehlhorn.End();

            // Collider from polygon
            ApplyCollider(hmPolyGroup);

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
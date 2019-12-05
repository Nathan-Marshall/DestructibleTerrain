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
    public abstract class DestructibleObjectCustomHMClippingCustomHMCollider : DestructibleObject
    {
        private DTMesh hmMesh;

        public override List<DTPolygon> GetTransformedPolygonList() {
            if (hmMesh == null) {
                return null;
            }

            // Assume no holes in polygon list
            return hmMesh.Partitions.Select(
                poly => new DTPolygon(poly.Select(i => TransformPoint(hmMesh.Vertices[i])).ToList())).ToList();
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
            // Assume no holes in polygon list
            var newPolyList = transformedPolygonList.Select(poly => new DTPolygon(
                poly.Contour.Select(InverseTransformPoint).ToList())).ToList();

            ApplyPolygonList(newPolyList);
        }
    }
}
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
    // DestructibleObject with triangulated clipping, triangulated collider, Triangle.Net triangulator
    public class DestructibleObjectTriangulatedClippingTriangulatedCollider : DestructibleObject
    {
        private DTConvexPolygroup dtPolygroup;

        public override List<DTPolygon> GetTransformedPolygonList() {
            if (dtPolygroup == null) {
                return null;
            }

            // Assume no holes in polygon list
            return dtPolygroup.Select(poly => new DTPolygon(poly.Select(TransformPoint).ToList())).ToList();
        }

        public override void ApplyPolygonList(List<DTPolygon> clippedPolygonList) {
            // The clipped polygons could potentially be concave or have holes, so we will triangulate each one before applying
            DTProfileMarkers.Triangulation.Begin();
            dtPolygroup = DTUtility.TriangulateAll(clippedPolygonList, GetTriangulator());
            DTProfileMarkers.Triangulation.End();

            // Collider from polygon
            ApplyCollider(dtPolygroup);

            // Create mesh from triangulated polygon
            ApplyRenderMesh(dtPolygroup.ToMesh());
        }

        public override void ApplyTransformedPolygonList(List<DTPolygon> transformedPolygonList) {
            var newPolyList = transformedPolygonList.Select(poly => new DTPolygon(
                poly.Contour.Select(InverseTransformPoint).ToList(),
                poly.Holes.Select(hole => hole.Select(InverseTransformPoint).ToList()).ToList()
                )).ToList();

            ApplyPolygonList(newPolyList);
        }
    }
}
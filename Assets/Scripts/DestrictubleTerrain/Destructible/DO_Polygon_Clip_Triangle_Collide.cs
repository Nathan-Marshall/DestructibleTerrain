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
    // DestructibleObject with polygon clipping, triangulated collider
    public class DO_Polygon_Clip_Triangle_Collide : DestructibleObject
    {
        private DTPolygon dtPolygon;

        public override List<DTPolygon> GetTransformedPolygonList() {
            if (dtPolygon == null) {
                return null;
            }

            DTProfilerMarkers.Transformation.Begin();
            List<DTPolygon> polygonList = new List<DTPolygon>() {
                new DTPolygon(
                    dtPolygon.Contour.Select(TransformPoint).ToList(),
                    dtPolygon.Holes.Select(hole => hole.Select(TransformPoint).ToList()).ToList())
            };
            DTProfilerMarkers.Transformation.End();
            return polygonList;
        }

        public override void ApplyPolygonList(List<DTPolygon> dtPolygonList) {
            if (dtPolygon == dtPolygonList[0]) {
                return;
            }
            dtPolygon = dtPolygonList[0];

            DTProfilerMarkers.Triangulation.Begin();
            DTMesh dtMesh = GetTriangulator().PolygonToMesh(dtPolygon);
            DTProfilerMarkers.Triangulation.End();

            // Collider from polygon
            DTProfilerMarkers.ApplyCollider.Begin();
            ApplyCollider(dtMesh);
            DTProfilerMarkers.ApplyCollider.End();

            // Create mesh from triangulated polygon
            ApplyRenderMesh(dtMesh);
        }

        public override void ApplyTransformedPolygonList(List<DTPolygon> transformedPolygonList) {
            DTProfilerMarkers.Transformation.Begin();
            List<DTPolygon> dtPolygonList = new List<DTPolygon>() {
                new DTPolygon(
                    transformedPolygonList[0].Contour.Select(InverseTransformPoint).ToList(),
                    transformedPolygonList[0].Holes.Select(hole => hole.Select(InverseTransformPoint).ToList()).ToList())
            };
            DTProfilerMarkers.Transformation.End();

            ApplyPolygonList(dtPolygonList);
        }
    }
}
using ClipperLib;
using DestructibleTerrain;
using DestructibleTerrain.Clipping;
using DestructibleTerrain.Triangulation;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TriangleNet.Geometry;
using UnityEngine;

namespace DestructibleTerrain.Destructible
{
    // DestructibleObject with triangulated clipping, triangulated collider
    public class DO_Advanced_Triangle_Clip_Collide : DestructibleObject
    {
        private PolygroupModifier polygroup;

        public override List<DTPolygon> GetTransformedPolygonList() {
            return polygroup?.KeptPolygons.Select(poly => new DTPolygon(poly.Select(TransformPoint).ToList())).ToList();
        }

        public PolygroupModifier GetPolygroup() {
            return polygroup;
        }

        public void SetPolygroup(PolygroupModifier polygroup) {
            this.polygroup = polygroup;
        }

        public override void ApplyPolygonList(List<DTPolygon> clippedPolygonList) {
            DTConvexPolygroup triangles = DTUtility.TriangulateAll(clippedPolygonList, GetTriangulator());
            ApplyPolygroupModifier(new PolygroupModifier(null, null, triangles));
        }

        public override void ApplyTransformedPolygonList(List<DTPolygon> transformedPolygonList) {
            ApplyPolygonList(transformedPolygonList.Select(poly => new DTPolygon(
                poly.Contour.Select(InverseTransformPoint).ToList(),
                poly.Holes.Select(hole => hole.Select(InverseTransformPoint).ToList()).ToList()
                )).ToList());
        }

        public void ApplyPolygroupModifier(PolygroupModifier mod) {
            // Collider from polygon
            DTProfilerMarkers.ApplyCollider.Begin();

            PolygonCollider2D polygonCollider = GetComponent<PolygonCollider2D>();

            // If there is no existing polygroup, create one
            if (polygroup == null) {
                // Create a polygroup from the new polygons passed in
                polygroup = new PolygroupModifier(new DTConvexPolygroup(mod.newPolygons),
                    Enumerable.Range(0, mod.newPolygons.Count).ToList(), null);

                // Add all polygons as paths
                polygonCollider.pathCount = mod.newPolygons.Count;
                for(int i = 0; i < mod.newPolygons.Count; i++) {
                    polygonCollider.SetPath(i, mod.newPolygons[i]);
                }
                DTProfilerMarkers.ApplyCollider.End();

                // Create mesh from triangulated polygon
                ApplyRenderMesh(polygroup.KeptPolygons.ToMesh());

                return;
            }

            // Calculate the number of paths in the output. Must be at least large enough to contain the last kept
            // polygon. New polygons will fill gaps. If there are more gaps than new polygons, the gaps will remain.
            // If there are more new polygons than gaps, the number of paths will be extended accordingly.
            int minimumPaths = mod.keptIndices.Count == 0 ? 0 : mod.keptIndices[mod.keptIndices.Count - 1] + 1;
            int gaps = minimumPaths - mod.keptIndices.Count;
            int numPaths = minimumPaths + Mathf.Max(0, mod.newPolygons.Count - gaps);

            // Set number of paths in stored polygroup and in collider based on the calculations above
            if (polygroup.originalPolygroup.Count < numPaths) {
                polygroup.originalPolygroup.AddRange(Enumerable.Repeat<List<Vector2>>(null, numPaths - polygroup.originalPolygroup.Count));
            } else if (polygroup.originalPolygroup.Count > numPaths) {
                polygroup.originalPolygroup.RemoveRange(numPaths, polygroup.originalPolygroup.Count - numPaths);
            }
            polygonCollider.pathCount = numPaths;

            int keptIndicesIndex = 0;
            int nextKeptIndex = keptIndicesIndex < polygroup.keptIndices.Count ? polygroup.keptIndices[keptIndicesIndex] : -1;

            int modKeptIndicesIndex = 0;
            int modNextKeptIndex = modKeptIndicesIndex < mod.keptIndices.Count ? mod.keptIndices[modKeptIndicesIndex] : -1;

            int modNewPolygonsIndex = 0;

            List<int> newKeptIndices = new List<int>();

            for (int i = 0; i < numPaths; i++) {
                // If we are keeping this polygon, determine which polygon to keep next
                if (i == modNextKeptIndex) {
                    keptIndicesIndex++;
                    nextKeptIndex = keptIndicesIndex < polygroup.keptIndices.Count ? polygroup.keptIndices[keptIndicesIndex] : -1;

                    modKeptIndicesIndex++;
                    modNextKeptIndex = modKeptIndicesIndex < mod.keptIndices.Count ? mod.keptIndices[modKeptIndicesIndex] : -1;

                    newKeptIndices.Add(i);
                }
                // If a new polygon should fill this spot, do so now
                else if (modNewPolygonsIndex < mod.newPolygons.Count) {
                    polygroup.originalPolygroup[i] = mod.newPolygons[modNewPolygonsIndex];

                    polygonCollider.SetPath(i, mod.newPolygons[modNewPolygonsIndex]);
                    modNewPolygonsIndex++;

                    newKeptIndices.Add(i);

                    // If the new polygon replaced an old one, determine the next polygon in the old list
                    if (i == nextKeptIndex) {
                        keptIndicesIndex++;
                        nextKeptIndex = keptIndicesIndex < polygroup.keptIndices.Count ? polygroup.keptIndices[keptIndicesIndex] : -1;
                    }
                }
                // If we are discarding this polygon and there are no new polygons to replace it, but we are keeping subsequent polygons, empty this path
                else if (i == nextKeptIndex) {
                    keptIndicesIndex++;
                    nextKeptIndex = keptIndicesIndex < polygroup.keptIndices.Count ? polygroup.keptIndices[keptIndicesIndex] : -1;

                    polygonCollider.SetPath(i, new Vector2[0]);
                }
            }
            polygroup.keptIndices = newKeptIndices;

            if (GetComponent<Rigidbody2D>().mass < MassCutoff) {
                Destroy(gameObject);
            }

            DTProfilerMarkers.ApplyCollider.End();

            // Create mesh from triangulated polygon
            ApplyRenderMesh(polygroup.KeptPolygons.ToMesh());
        }
    }
}
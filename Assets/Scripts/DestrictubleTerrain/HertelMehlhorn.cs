using ClipperLib;
using DestrictubleTerrain;
using DestrictubleTerrain.Destructible;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class HertelMehlhorn
{
    struct SimplifiedEdge
    {
        public int P0 { get; set; }
        public int P1 { get; set; }

        public SimplifiedEdge(int p0, int p1) {
            P0 = p0;
            P1 = p1;
        }

        public SimplifiedEdge(Edge e) {
            P0 = e.P0;
            P1 = e.P1;
        }

        public int IndexOfPoint(int p) {
            return p == P0 ? 0 : p == P1 ? 1 : -1;
        }

        public static bool operator == (SimplifiedEdge e1, SimplifiedEdge e2) {
            return e1.P0 == e2.P0 && e1.P1 == e2.P1
                || e1.P0 == e2.P1 && e1.P1 == e2.P0;
        }

        public static bool operator != (SimplifiedEdge e1, SimplifiedEdge e2) {
            return !(e1 == e2);
        }

        public override bool Equals(object o) {
            // Check for null and compare run-time types.
            if (o == null || !GetType().Equals(o.GetType())) {
                return false;
            }
            return this == (SimplifiedEdge)o;
        }

        public override int GetHashCode() {
            return P0 ^ P1;
        }
    }

    class Edge
    {
        public int P0 { get; set; }
        public int P1 { get; set; }
        public bool IsBidirectional { get; set; }
        public int RightPoly { get; set; }
        public int LeftPoly { get; set; }

        public Edge(SimplifiedEdge e, int rightPoly) {
            P0 = e.P0;
            P1 = e.P1;
            IsBidirectional = false;
            RightPoly = rightPoly;
        }

        public int IndexOfPoint (int p) {
            return p == P0 ? 0 : p == P1 ? 1 : -1;
        }

        public int IndexOfPoly(int p) {
            return p == RightPoly ? 0 : p == LeftPoly ? 1 : -1;
        }
    }


    // Note: allows output to have consecutive colinear edges in a partition
    public static DTMesh Execute(DTMesh input) {
        Dictionary<SimplifiedEdge, Edge> realEdges = new Dictionary<SimplifiedEdge, Edge>();

        List<int>[] tempPartitions = new List<int>[input.Partitions.Count];
        int[] polyRemapper = new int[input.Partitions.Count];
        for (int i = 0; i < input.Partitions.Count; i++) {
            tempPartitions[i] = new List<int>(input.Partitions[i]);
            polyRemapper[i] = i;
        }
        List<int> getPolygon (int i) {
            return tempPartitions[polyRemapper[i]];
        }

        // Determine internal edges and map them to their connected polygons
        for (int polyIndex = 0; polyIndex < input.Partitions.Count; polyIndex++) {
            List<int> poly = input.Partitions[polyIndex];

            // Add all the initial partitions (triangles if the input was a triangulation)
            for (int vertexIndex = 0; vertexIndex < poly.Count; vertexIndex++) {
                int v = poly[vertexIndex];
                int vNext = poly.GetCircular(vertexIndex + 1);

                SimplifiedEdge e = new SimplifiedEdge(v, vNext);
                if (!realEdges.ContainsKey(e)) {
                    // If this is the first time visiting this edge, make a new entry and add the right polygon
                    realEdges[e] = new Edge(e, polyIndex);
                } else {
                    // If we have already visited this edge, add the left polygon and make the edge bidirectional
                    realEdges[e].IsBidirectional = true;
                    realEdges[e].LeftPoly = polyIndex;
                }
            }
        }

        // Iterate internal edges and check if they can be removed without creating concave partitions
        foreach (var internalEdgePair in realEdges.Where(edgePair => edgePair.Value.IsBidirectional)) {
            SimplifiedEdge simpleEdge = internalEdgePair.Key;
            Edge realEdge = internalEdgePair.Value;

            // Find the index within the right polygon where the shared edge ends (P1)
            List<int> rightPoly = getPolygon(realEdge.RightPoly);
            int rightPolyStart = -1;
            for (int i = 0; i < rightPoly.Count; i++) {
                int p = rightPoly[i];
                if (p == simpleEdge.P1) {
                    rightPolyStart = i;
                    break;
                }
            }

            // Find the index within the left polygon where the shared edge ends (P0)
            List<int> leftPoly = getPolygon(realEdge.LeftPoly);
            int leftPolyStart = -1;
            for (int i = 0; i < leftPoly.Count; i++) {
                int p = leftPoly[i];
                if (p == simpleEdge.P0) {
                    leftPolyStart = i;
                    break;
                }
            }

            // Determine if the angle at the edge's P0 would become reflex if this edge were removed
            Vector2 poly0LastEdge = input.Vertices[rightPoly.GetCircular(rightPolyStart - 1)] - input.Vertices[rightPoly.GetCircular(rightPolyStart - 2)];
            Vector2 poly1FirstEdge = input.Vertices[leftPoly.GetCircular(leftPolyStart + 1)] - input.Vertices[leftPoly.GetCircular(leftPolyStart)];
            bool p0Reflex = Vector3.Cross(poly0LastEdge, poly1FirstEdge).z > 0;

            // Determine if the angle at the edge's P1 would become reflex if this edge were removed
            Vector2 poly1LastEdge = input.Vertices[leftPoly.GetCircular(leftPolyStart - 1)] - input.Vertices[leftPoly.GetCircular(leftPolyStart - 2)];
            Vector2 poly0FirstEdge = input.Vertices[rightPoly.GetCircular(rightPolyStart + 1)] - input.Vertices[rightPoly.GetCircular(rightPolyStart)];
            bool p1Reflex = Vector3.Cross(poly1LastEdge, poly0FirstEdge).z > 0;

            if (!p0Reflex && !p1Reflex) {
                // Remove the edge and merge the two polygons since the result will be convex

                // Add points from rightPoly in CW order from the removed edge's P1 to P0, including P1 but not P0
                List<int> newPoly = rightPoly.GetRange(rightPolyStart, rightPoly.Count - rightPolyStart);
                newPoly.AddRange(rightPoly.GetRange(0, rightPolyStart));
                newPoly.RemoveAt(newPoly.Count - 1);

                // Add points from left in CW order from the removed edge's P0 to P1, including P0 but not P1
                newPoly.AddRange(leftPoly.GetRange(leftPolyStart, leftPoly.Count - leftPolyStart));
                newPoly.AddRange(leftPoly.GetRange(0, leftPolyStart));
                newPoly.RemoveAt(newPoly.Count - 1);

                // Modify the right polygon
                rightPoly.Clear();
                rightPoly.AddRange(newPoly);

                // Clear the left polygon
                leftPoly.Clear();
                
                // Remap the left polygon index to match the right
                polyRemapper[realEdge.LeftPoly] = polyRemapper[realEdge.RightPoly];
            }
        }

        // Return a new mesh that copies the vertices of the original, but uses the merged partitions
        // (ignores the left partitions that we cleared)
        return new DTMesh(new List<Vector2>(input.Vertices),
            tempPartitions.Where(partition => partition.Count > 0).ToList());
    }

    public static DTConvexPolygonGroup Execute(DTConvexPolygonGroup input) {
        return null;
    }
}

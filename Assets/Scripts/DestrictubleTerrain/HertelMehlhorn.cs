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
        public int Poly0 { get; set; }
        public int Poly1 { get; set; }

        public Edge(SimplifiedEdge e, int poly0) {
            P0 = e.P0;
            P1 = e.P1;
            IsBidirectional = false;
            Poly0 = poly0;
        }

        public int IndexOfPoint (int p) {
            return p == P0 ? 0 : p == P1 ? 1 : -1;
        }

        public int IndexOfPoly(int p) {
            return p == Poly0 ? 0 : p == Poly1 ? 1 : -1;
        }
    }

    public static DTMesh Simplify(DTMesh input) {
        HashSet<int>[] verticesToPolys = new HashSet<int>[input.Vertices.Count];
        HashSet<SimplifiedEdge>[] verticesToEdges = new HashSet<SimplifiedEdge>[input.Vertices.Count];
        Dictionary<SimplifiedEdge, Edge> realEdges = new Dictionary<SimplifiedEdge, Edge>();

        // Map all vertices to polygons and edges
        for (int polyIndex = 0; polyIndex < input.Partitions.Count; polyIndex++) {
            List<int> poly = input.Partitions[polyIndex];

            for (int vertexIndex = 0; vertexIndex < poly.Count; vertexIndex++) {
                int v = poly[vertexIndex];

                if (verticesToPolys[v] == null) {
                    verticesToPolys[v] = new HashSet<int>();
                    verticesToEdges[v] = new HashSet<SimplifiedEdge>();
                }
                verticesToPolys[v].Add(polyIndex);

                int vPrev = poly.GetCircular(vertexIndex - 1);
                int vNext = poly.GetCircular(vertexIndex + 1);

                SimplifiedEdge ePrev = new SimplifiedEdge(vPrev, v);
                verticesToEdges[v].Add(ePrev);
                if (!realEdges.ContainsKey(ePrev)) {
                    // If this is the first time visiting this edge, make a new entry and add the first polygon
                    realEdges[ePrev] = new Edge(ePrev, polyIndex);
                } else {
                    // If we have already visited this edge, add the second polygon and make the edge bidirectional
                    realEdges[ePrev].IsBidirectional = true;
                    realEdges[ePrev].Poly1 = polyIndex;
                }

                SimplifiedEdge eNext = new SimplifiedEdge(v, vNext);
                verticesToEdges[v].Add(eNext);
                if (!realEdges.ContainsKey(eNext)) {
                    // If this is the first time visiting this edge, make a new entry and add the first polygon
                    realEdges[eNext] = new Edge(ePrev, polyIndex);
                } else {
                    // If we have already visited this edge, add the second polygon and make the edge bidirectional
                    realEdges[eNext].IsBidirectional = true;
                    realEdges[eNext].Poly1 = polyIndex;
                }
            }
        }

        // Iterate internal edges and check if they can be removed without creating concave partitions
        foreach (var internalEdgePair in realEdges.Where(edgePair => edgePair.Value.IsBidirectional)) {
            SimplifiedEdge simpleEdge = internalEdgePair.Key;
            Edge realEdge = internalEdgePair.Value;

            // Find the index within the first polygon where the shared edge ends
            List<int> poly0 = input.Partitions[realEdge.Poly0];
            int poly0Start = -1;
            for (int i = 0; i < poly0.Count; i++) {
                int p = poly0[i];
                if (p == simpleEdge.P1) {
                    poly0Start = i;
                    break;
                }
            }

            // Find the index within the second polygon where the shared edge ends
            List<int> poly1 = input.Partitions[realEdge.Poly1];
            int poly1Start = -1;
            for (int i = 0; i < poly1.Count; i++) {
                int p = poly1[i];
                if (p == simpleEdge.P0) {
                    poly1Start = i;
                    break;
                }
            }

            // Determine if the angle at the edge's P0 would become reflex if this edge were removed
            Vector2 poly0LastEdge = input.Vertices[poly0.GetCircular(poly0Start - 1)] - input.Vertices[poly0.GetCircular(poly0Start - 2)];
            Vector2 poly1FirstEdge = input.Vertices[poly1.GetCircular(poly1Start + 1)] - input.Vertices[poly1.GetCircular(poly1Start)];
            bool p0Reflex = Vector3.Cross(poly0LastEdge, poly1FirstEdge).z > 0;

            // Determine if the angle at the edge's P1 would become reflex if this edge were removed
            Vector2 poly1LastEdge = input.Vertices[poly1.GetCircular(poly1Start - 1)] - input.Vertices[poly1.GetCircular(poly1Start - 2)];
            Vector2 poly0FirstEdge = input.Vertices[poly0.GetCircular(poly0Start + 1)] - input.Vertices[poly0.GetCircular(poly0Start)];
            bool p1Reflex = Vector3.Cross(poly1LastEdge, poly0FirstEdge).z > 0;

            if (!p0Reflex && !p1Reflex) {
                // Remove the edge and merge the two polygons since the result will be convex

            }
        }
    }

    public static DTConvexPolygonGroup Simplify(DTConvexPolygonGroup input) {
        return null;
    }
}

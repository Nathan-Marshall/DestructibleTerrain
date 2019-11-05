using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Topology;
using UnityEngine;
using UnityEngine.Assertions;

namespace DestructibleTerrain.HertelMehlhorn
{
    public class CustomHM : IHertelMehlhorn
    {
        private static readonly Lazy<CustomHM> lazyInstance = new Lazy<CustomHM>(() => new CustomHM());

        // Singleton intance
        public static CustomHM Instance {
            get { return lazyInstance.Value; }
        }

        private CustomHM() { }

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

            public static bool operator ==(SimplifiedEdge e1, SimplifiedEdge e2) {
                return e1.P0 == e2.P0 && e1.P1 == e2.P1
                    || e1.P0 == e2.P1 && e1.P1 == e2.P0;
            }

            public static bool operator !=(SimplifiedEdge e1, SimplifiedEdge e2) {
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

            public int IndexOfPoint(int p) {
                return p == P0 ? 0 : p == P1 ? 1 : -1;
            }

            public int IndexOfPoly(int p) {
                return p == RightPoly ? 0 : p == LeftPoly ? 1 : -1;
            }
        }


        // Note: allows output to have consecutive colinear edges in a partition
        public DTMesh ExecuteToMesh(DTMesh input) {
            List<int> clearedPolys = new List<int>();
            Dictionary<SimplifiedEdge, Edge> realEdges = new Dictionary<SimplifiedEdge, Edge>();

            List<int>[] tempPartitions = new List<int>[input.Partitions.Count];
            int[] polyRemapper = new int[input.Partitions.Count];

            // Local function that gets the remapped polygon index for the given original index
            int getRemappedIndex(int i) {
                Stack<int> collapseStack = new Stack<int>();
                while (i != polyRemapper[i]) {
                    collapseStack.Push(i);
                    i = polyRemapper[i];
                }
                // Lazily collapse remapping chains (entries that remap multiple times should remap only once)
                while (collapseStack.Count > 0) {
                    polyRemapper[collapseStack.Pop()] = i;
                }
                return i;
            }

            // Determine internal edges and map them to their connected polygons
            for (int polyIndex = 0; polyIndex < input.Partitions.Count; polyIndex++) {
                List<int> poly = input.Partitions[polyIndex];
                tempPartitions[polyIndex] = new List<int>(poly);
                polyRemapper[polyIndex] = polyIndex;

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
                int rightPolyIndex = getRemappedIndex(realEdge.RightPoly);
                List<int> rightPoly = tempPartitions[rightPolyIndex];
                int rightPolyStart = -1;
                for (int i = 0; i < rightPoly.Count; i++) {
                    int p = rightPoly[i];
                    if (p == simpleEdge.P1) {
                        rightPolyStart = i;
                        break;
                    }
                }

                // Find the index within the left polygon where the shared edge ends (P0)
                int leftPolyIndex = getRemappedIndex(realEdge.LeftPoly);
                List<int> leftPoly = tempPartitions[leftPolyIndex];
                int leftPolyStart = -1;
                for (int i = 0; i < leftPoly.Count; i++) {
                    int p = leftPoly[i];
                    if (p == simpleEdge.P0) {
                        leftPolyStart = i;
                        break;
                    }
                }

                // Determine if the angle at the edge's P0 would become reflex if this edge were removed
                Vector2 rightPolyLastEdge = input.Vertices[rightPoly.GetCircular(rightPolyStart - 1)] - input.Vertices[rightPoly.GetCircular(rightPolyStart - 2)];
                Vector2 leftPolyFirstEdge = input.Vertices[leftPoly.GetCircular(leftPolyStart + 1)] - input.Vertices[leftPoly.GetCircular(leftPolyStart)];
                bool p0Reflex = Vector3.Cross(rightPolyLastEdge, leftPolyFirstEdge).z > 0;

                // Determine if the angle at the edge's P1 would become reflex if this edge were removed
                Vector2 leftPolyLastEdge = input.Vertices[leftPoly.GetCircular(leftPolyStart - 1)] - input.Vertices[leftPoly.GetCircular(leftPolyStart - 2)];
                Vector2 rightPolyFirstEdge = input.Vertices[rightPoly.GetCircular(rightPolyStart + 1)] - input.Vertices[rightPoly.GetCircular(rightPolyStart)];
                bool p1Reflex = Vector3.Cross(leftPolyLastEdge, rightPolyFirstEdge).z > 0;

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
                    clearedPolys.Add(leftPolyIndex);

                    // Remap the left polygon index to match the right
                    polyRemapper[leftPolyIndex] = rightPolyIndex;
                }
            }

            // Return a new mesh that copies the vertices of the original, but uses the merged partitions
            // (ignores the left partitions that we cleared)
            return new DTMesh(new List<Vector2>(input.Vertices),
                tempPartitions.Where(partition => partition.Count > 0).ToList());
        }

        public DTMesh ExecuteToMesh(DTConvexPolygonGroup input) {
            return ExecuteToMesh(input.ToMesh());
        }

        public DTConvexPolygonGroup ExecuteToPolyGroup(DTMesh input) {
            return ExecuteToMesh(input).ToPolyGroup();
        }

        public DTConvexPolygonGroup ExecuteToPolyGroup(DTConvexPolygonGroup input) {
            return ExecuteToPolyGroup(input.ToMesh());
        }
    }
}

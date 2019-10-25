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

namespace DestrictubleTerrain.Triangulation
{
    public class TriangleNetAdapter : ITriangulator {
        private static readonly Lazy<TriangleNetAdapter> lazyInstance = new Lazy<TriangleNetAdapter>(() => new TriangleNetAdapter());

        // Singleton intance
        public static TriangleNetAdapter Instance {
            get { return lazyInstance.Value; }
        }

        private TriangleNetAdapter() { }

        public DTMesh PolygonToMesh(DTPolygon subject) {
            // Format polygon input and execute
            Polygon polygon = new Polygon();
            polygon.Add(new Contour(subject.Contour.ToVertexList()), false);
            foreach (var hole in subject.Holes) {
                polygon.Add(new Contour(hole.ToVertexList()), true);
            }
            IMesh triangleNetOutput = polygon.Triangulate();

            // Convert Triangle.NET output into DTMesh
            List<Vector2> vertices = triangleNetOutput.Vertices.ToVector2List();
            List<List<int>> triangles = triangleNetOutput.Triangles.ToPartitionList();
            return new DTMesh(vertices, triangles);
        }

        public DTConvexPolygonGroup PolygonToTriangleList(DTPolygon subject) {
            // Format polygon input and execute
            Polygon polygon = new Polygon();
            polygon.Add(new Contour(subject.Contour.ToVertexList()), false);
            foreach (var hole in subject.Holes) {
                polygon.Add(new Contour(hole.ToVertexList()), true);
            }
            IMesh triangleNetOutput = polygon.Triangulate();

            // Convert Triangle.NET output into poly group
            return new DTConvexPolygonGroup(triangleNetOutput.Triangles.Select(t => t.ToVertexList()).ToList());
        }

        public DTMesh PolygonToHMMesh(DTPolygon subject) {
            // Format polygon input and execute
            Polygon polygon = new Polygon();
            polygon.Add(new Contour(subject.Contour.ToVertexList()), false);
            foreach (var hole in subject.Holes) {
                polygon.Add(new Contour(hole.ToVertexList()), true);
            }
            IMesh triangleNetOutput = polygon.Triangulate();

            // Convert Triangle.NET output into DTMesh
            List<Vector2> vertices = triangleNetOutput.Vertices.ToVector2List();
            List<List<int>> triangles = triangleNetOutput.Triangles.ToPartitionList();
            List<Edge> edges = triangleNetOutput.Triangles.ToEdgeList();

            return new DTMesh(vertices, triangles);
        }
    }

    struct Edge {
        public int P0 { get; set; }
        public int P1 { get; set; }
        public int Poly0 { get; set; }
        public int Poly1 { get; set; }

        public Edge(int p0, int p1, int poly0, int poly1) {
            P0 = p0;
            P1 = p1;
            Poly0 = poly0;
            Poly1 = poly1;
        }
    }

    static class ExtensionsForClipperAdapter
    {
        public static Vertex ToVertex(this Vector2 p) {
            return new Vertex(p.x, p.y);
        }

        public static Vector2 ToVector2(this Vertex p) {
            return new Vector2((float)p.X, (float)p.Y);
        }

        public static Edge ToEdge(this ISegment e) {
            var t0 = e.GetTriangle(0);
            var t1 = e.GetTriangle(1);
            return new Edge(e.P0, e.P1, t0?.ID ?? -1, t1?.ID ?? -1);
        }

        public static List<int> ToIntList(this Triangle t) {
            // Note that Triangle.NET outputs CCW but we want CW
            return new List<int> { t.GetVertexID(2), t.GetVertexID(1), t.GetVertexID(0) };
        }

        public static List<Vector2> ToVertexList(this Triangle t) {
            // Note that Triangle.NET outputs CCW but we want CW
            return new List<Vector2> {
                t.GetVertex(2).ToVector2(),
                t.GetVertex(1).ToVector2(),
                t.GetVertex(0).ToVector2()
            };
        }

        public static List<Vertex> ToVertexList(this IEnumerable<Vector2> vectors) {
            return vectors.Select(ToVertex).ToList();
        }

        public static List<Vector2> ToVector2List(this IEnumerable<Vertex> vertices) {
            return vertices.Select(ToVector2).ToList();
        }

        public static List<List<int>> ToPartitionList(this IEnumerable<Triangle> triangles) {
            return triangles.Select(ToIntList).ToList();
        }

        public static List<Edge> ToEdgeList(this IEnumerable<Triangle> triangles) {
            return triangles.SelectMany(t => new Edge[] {
                t.GetSegment(0).ToEdge(),
                t.GetSegment(1).ToEdge(),
                t.GetSegment(2).ToEdge()
            }).Distinct().ToList();
        }
    }
}

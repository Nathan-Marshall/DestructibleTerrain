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

namespace DestructibleTerrain.Triangulation
{
    public class TriangleNetTriangulator : ITriangulator {
        private static readonly Lazy<TriangleNetTriangulator> lazyInstance = new Lazy<TriangleNetTriangulator>(() => new TriangleNetTriangulator());

        // Singleton intance
        public static TriangleNetTriangulator Instance {
            get { return lazyInstance.Value; }
        }

        private TriangleNetTriangulator() { }

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

        public DTConvexPolygroup PolygonToTriangleList(DTPolygon subject) {
            // Format polygon input and execute
            Polygon polygon = new Polygon();
            polygon.Add(new Contour(subject.Contour.ToVertexList()), false);
            foreach (var hole in subject.Holes) {
                polygon.Add(new Contour(hole.ToVertexList()), true);
            }
            IMesh triangleNetOutput = polygon.Triangulate();

            // Convert Triangle.NET output into poly group
            return new DTConvexPolygroup(triangleNetOutput.Triangles.Select(t => t.ToVertexList()).ToList());
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
    }
}

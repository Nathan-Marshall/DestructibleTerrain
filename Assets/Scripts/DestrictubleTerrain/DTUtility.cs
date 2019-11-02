using ClipperLib;
using DestructibleTerrain;
using DestructibleTerrain.Destructible;
using DestructibleTerrain.Triangulation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class DTUtility
{
    public static T GetCircular<T>(this IList<T> list, int i) {
        i = ((i % list.Count) + list.Count) % list.Count;
        return list[i];
    }

    // -1 means the object bounds are completely outside the explosion.
    // 0 means the bounds intersect.
    // 1 means the object bounds are completely inside the explosion.
    public static int BoundsCheck(DestructibleObject dtObj, Explosion exp) {
        Bounds oBounds = dtObj.GetComponent<Collider2D>().bounds;
        Vector3 ePos = new Vector3(exp.Position.x, exp.Position.y);
        float radSq = exp.Radius * exp.Radius;

        // Completely outside
        if ((oBounds.ClosestPoint(ePos) - ePos).sqrMagnitude >= radSq) {
            return -1;
        }

        // Compute furthest point
        Vector3 furthestPoint = new Vector3(oBounds.min.x, oBounds.min.y, 0);
        if (oBounds.center.x > ePos.x) {
            furthestPoint.x = oBounds.max.x;
        }
        if (oBounds.center.y > ePos.y) {
            furthestPoint.y = oBounds.max.y;
        }

        // Completely inside
        if ((furthestPoint - ePos).sqrMagnitude <= radSq) {
            return 1;
        }

        // Bounds intersect
        return 0;
    }

    public static Bounds GetBounds(IEnumerable<Vector2> points) {
        Vector2 min = points.First();
        Vector2 max = points.First();
        foreach (Vector2 p in points) {
            if (p.x < min.x) {
                min.x = p.x;
            }
            if (p.x > max.x) {
                max.x = p.x;
            }
            if (p.y < min.y) {
                min.y = p.y;
            }
            if (p.y > max.y) {
                max.y = p.y;
            }
        }
        Bounds b = new Bounds();
        b.SetMinMax(min, max);
        return b;
    }

    public static DTConvexPolygonGroup TriangulateAll(List<DTPolygon> polygonList, ITriangulator triangulator) {
        return new DTConvexPolygonGroup(polygonList.SelectMany(
            poly => triangulator.PolygonToTriangleList(poly)).ToList());
    }

    public static DTConvexPolygonGroup ToPolyGroup(this DTMesh mesh) {
        return new DTConvexPolygonGroup(mesh.Partitions.Select(part => part.Select(i => mesh.Vertices[i]).ToList()).ToList());
    }
    
    public static DTMesh ToMesh(this DTConvexPolygonGroup polyGroup) {
        const long FixedDecimalConversion = 100000;

        IntPoint ToIntPoint(Vector2 p) {
            return new IntPoint(p.x * FixedDecimalConversion, p.y * FixedDecimalConversion);
        }

        Dictionary<IntPoint, int> vertexMap = new Dictionary<IntPoint, int>();
        List<Vector2> vertices = new List<Vector2>();
        foreach (var poly in polyGroup) {
            // Assume no holes
            foreach (var v in poly) {
                try {
                    // Add the vertex only if it has not already been added
                    vertexMap.Add(ToIntPoint(v), vertices.Count);
                    vertices.Add(v);
                } catch (ArgumentException) { }
            }
        }

        List<List<int>> partitions = new List<List<int>>(polyGroup.Count);
        foreach (var poly in polyGroup) {
            List<int> indices = new List<int>();
            // Assume no holes
            foreach (var v in poly) {
                indices.Add(vertexMap[ToIntPoint(v)]);
            }
            partitions.Add(indices);
        }

        return new DTMesh(vertices, partitions);
    }
}

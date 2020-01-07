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
    public const int FixedPointConversion = 10000000;

    public class ApproximateVector2Comparer : IEqualityComparer<Vector2>
    {
        public bool Equals(Vector2 a, Vector2 b) {
            return a.Approximately(b);
        }

        public int GetHashCode(Vector2 v) {
            int x = v.x.ToFixedPoint();
            int y = v.y.ToFixedPoint();

            return ListHashCode(new int[] { x, y });
        }
    }

    public static int ListHashCode<T>(IList<T> list) {
        // Multiplicative hash modified from opendatastructures.org chapter 5.3

        long p = (1L << 32) - 5;   // prime: 2^32 - 5
        long z = 0x64b6055aL;  // 32 random bits
        int z2 = 0x5067d19d;   // random odd 32 bit number
        long s = 0;
        long zi = 1;
        for (int i = 0; i < list.Count; i++) {
            // reduce to 31 bits
            long xi = (list[i].GetHashCode() * z2) >> 1;
            s = (s + zi * xi) % p;
            zi = (zi * z) % p;
        }
        s = (s + zi * (p - 1)) % p;
        return (int)s;
    }

    public static int ToFixedPoint(this float n) {
        return (int)Mathf.Round(n * FixedPointConversion);
    }

    public static float FromFixedPoint(this int n) {
        return n / (float)FixedPointConversion;
    }

    public static bool Approximately(this Vector2 a, Vector2 b) {
        return a.x.ToFixedPoint() == b.x.ToFixedPoint() && a.y.ToFixedPoint() == b.y.ToFixedPoint();
    }

    public static int FixedPointHashCode(this Vector2 v) {
        int x = v.x.ToFixedPoint();
        int y = v.y.ToFixedPoint();

        return ListHashCode(new int[] { x, y });
    }

    public static bool ContainSameValues<T> (IEnumerable<T> inA, IEnumerable<T> inB) where T : IEquatable<T> {
        if (inA.Count() != inB.Count()) {
            return false;
        }
        List<T> a = new List<T>(inA);
        List<T> b = new List<T>(inB);
        for (int i = 0; i < a.Count; ++i) {
            for (int j = 0; j < b.Count; ++j) {
                if (a[i].Equals(b[j])) {
                    a.RemoveAt(i--);
                    b.RemoveAt(j--);
                    break;
                }
            }
        }
        return a.Count == 0 && b.Count == 0;
    }

    public static bool PolygroupsEqual (IEnumerable<IEnumerable<DTPolygon>> inA, IEnumerable<IEnumerable<DTPolygon>> inB) {
        if (inA.Count() != inB.Count()) {
            return false;
        }
        List<IEnumerable<DTPolygon>> a = new List<IEnumerable<DTPolygon>>(inA);
        List<IEnumerable<DTPolygon>> b = new List<IEnumerable<DTPolygon>>(inB);
        for (int i = 0; i < a.Count; ++i) {
            for (int j = 0; j < b.Count; ++j) {
                if (ContainSameValues(a[i], b[j])) {
                    a.RemoveAt(i--);
                    b.RemoveAt(j--);
                    break;
                }
            }
        }
        return a.Count == 0 && b.Count == 0;
    }

    public static T GetCircular<T>(this IList<T> list, int i) {
        i = ((i % list.Count) + list.Count) % list.Count;
        return list[i];
    }

    // Removes unnecessary vertices
    public static List<Vector2> SimplifyContour(List<Vector2> inContour) {
        List<Vector2> contour = inContour.ToList();

        for (int i = 0; i < contour.Count; ++i) {
            Vector2 fromPrev = contour.GetCircular(i) - contour.GetCircular(i - 1);
            Vector2 toNext = contour.GetCircular(i + 1) - contour.GetCircular(i);
            if (fromPrev.Cross(toNext) == 0) {
                contour.RemoveAt((i % contour.Count + contour.Count) % contour.Count);
                i -= 2;
            }
        }

        return contour;
    }

    // Removes unnecessary vertices
    public static DTPolygon Simplify(this DTPolygon inPoly) {
        var simplifiedContour = SimplifyContour(inPoly.Contour);
        if (simplifiedContour.Count == 0) {
            return null;
        }

        List<List<Vector2>> simplifiedHoles = new List<List<Vector2>>();
        foreach (var hole in inPoly.Holes) {
            var simplifiedHole = SimplifyContour(hole);
            if (simplifiedContour.Count != 0) {
                simplifiedHoles.Add(simplifiedHole);
            }
        }

        return new DTPolygon(simplifiedContour, simplifiedHoles);
    }

    public static float Dot(this Vector2 a, Vector2 b) {
        return Vector2.Dot(a, b);
    }

    public static float Cross(this Vector2 a, Vector2 b) {
        return (a.x * b.y) - (a.y * b.x);
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
    
    // Returns true if bounds overlap
    public static bool BoundsCheck(DTPolygon a, DTPolygon b) {
        Vector2 aMin = a.Contour[0];
        Vector2 aMax = a.Contour[0];
        foreach (Vector2 v in a.Contour) {
            if (v.x < aMin.x) {
                aMin.x = v.x;
            }
            if (v.y < aMin.y) {
                aMin.y = v.y;
            }
            if (v.x > aMax.x) {
                aMax.x = v.x;
            }
            if (v.y > aMax.y) {
                aMax.y = v.y;
            }
        }

        Vector2 bMin = b.Contour[0];
        Vector2 bMax = b.Contour[0];
        foreach (Vector2 v in b.Contour) {
            if (v.x < bMin.x) {
                bMin.x = v.x;
            }
            if (v.y < bMin.y) {
                bMin.y = v.y;
            }
            if (v.x > bMax.x) {
                bMax.x = v.x;
            }
            if (v.y > bMax.y) {
                bMax.y = v.y;
            }
        }

        return bMax.x > aMin.x && bMax.y > aMin.y && aMax.x > bMin.x && aMax.y > bMin.y;
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

    public static DTConvexPolygroup TriangulateAll(List<DTPolygon> polygonList, ITriangulator triangulator) {
        return new DTConvexPolygroup(polygonList.SelectMany(
            poly => triangulator.PolygonToTriangleList(poly)).ToList());
    }

    public static DTConvexPolygroup ToPolygroup(this DTMesh mesh) {
        return new DTConvexPolygroup(mesh.Partitions.Select(part => part.Select(i => mesh.Vertices[i]).ToList()).ToList());
    }
    
    public static DTMesh ToMesh(this DTConvexPolygroup polygroup) {
        const long FixedDecimalConversion = 100000;

        IntPoint ToIntPoint(Vector2 p) {
            return new IntPoint(p.x * FixedDecimalConversion, p.y * FixedDecimalConversion);
        }

        Dictionary<IntPoint, int> vertexMap = new Dictionary<IntPoint, int>();
        List<Vector2> vertices = new List<Vector2>();
        foreach (var poly in polygroup) {
            // Assume no holes
            foreach (var v in poly) {
                try {
                    // Add the vertex only if it has not already been added
                    vertexMap.Add(ToIntPoint(v), vertices.Count);
                    vertices.Add(v);
                } catch (ArgumentException) { }
            }
        }

        List<List<int>> partitions = new List<List<int>>(polygroup.Count);
        foreach (var poly in polygroup) {
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

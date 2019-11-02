//Copyright (C) 2011 by Ivan Fratric
//Copyright (C) 2019 by Nathan Marshall
//
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in
//all copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//THE SOFTWARE.

using DestructibleTerrain;
using System.Linq;
using UnityEngine;

using TPPLPolyList = System.Collections.Generic.List<PolyPartitionHM.TPPLPoly>;

public static class PolyPartitionHM
{
    public static TPPLPolyList ToTPPLPolyList(this DTPolygon dtPoly) {
        TPPLPolyList polyList = new TPPLPolyList();

        TPPLPoly tpplContour = new TPPLPoly(dtPoly.Contour.Count);
        for (int i = 0; i < dtPoly.Contour.Count; i++) {
            tpplContour[dtPoly.Contour.Count - 1 - i] = new TPPLPoint(dtPoly.Contour[i]);
        }
        polyList.Add(tpplContour);

        foreach (var hole in dtPoly.Holes) {
            TPPLPoly tpplHole = new TPPLPoly(hole.Count);
            tpplHole.SetHole(true);
            for (int i = 0; i < hole.Count; i++) {
                tpplHole[hole.Count - 1 - i] = new TPPLPoint(hole[i]);
            }
            polyList.Add(tpplHole);
        }

        return polyList;
    }

    public static TPPLPolyList ToTPPLPolyList(this DTConvexPolygonGroup polyGroup) {
        TPPLPolyList polyList = new TPPLPolyList();

        foreach (var poly in polyGroup) {
            TPPLPoly tpplHole = new TPPLPoly(poly.Count);
            tpplHole.SetHole(true);
            for (int i = 0; i < poly.Count; i++) {
                tpplHole[poly.Count - 1 - i] = new TPPLPoint(poly[i]);
            }
            polyList.Add(tpplHole);
        }

        return polyList;
    }

    public static DTConvexPolygonGroup ToPolyGroup(this TPPLPolyList polyList) {
        return new DTConvexPolygonGroup(polyList.Select(tpplPoly => tpplPoly.GetPoints().Select(
            p => new Vector2(p.x, p.y)).Reverse().ToList()).ToList());
    }

    // 2D point structure
    public class TPPLPoint {
        public float x;
        public float y;
        // User-specified vertex identifier.  Note that this isn't used internally
        // by the library, but will be faithfully copied around.
        public readonly int id;

        public TPPLPoint(Vector2 p = new Vector2(), int id = -1) {
            x = p.x;
            y = p.y;
            this.id = id;
        }

        public static TPPLPoint operator + (TPPLPoint p1, TPPLPoint p2) {
            return new TPPLPoint() {
                x = p1.x + p2.x,
                y = p1.y + p2.y
            };
        }

        public static TPPLPoint operator - (TPPLPoint p1, TPPLPoint p2) {
            return new TPPLPoint() {
                x = p1.x - p2.x,
                y = p1.y - p2.y
            };
        }

        public static TPPLPoint operator * (TPPLPoint p, float f) {
            return new TPPLPoint() {
                x = p.x * f,
                y = p.y * f
            };
        }

        public static TPPLPoint operator / (TPPLPoint p, float f) {
            return new TPPLPoint() {
                x = p.x / f,
                y = p.y / f
            };
        }

        public static bool operator == (TPPLPoint p1, TPPLPoint p2) {
            return (p1.x == p2.x) && (p1.y == p2.y);
        }

        public static bool operator != (TPPLPoint p1, TPPLPoint p2) {
            return !(p1 == p2);
        }

        public override bool Equals(object o) {
            // Check for null and compare run-time types.
            if (o == null || !GetType().Equals(o.GetType())) {
                return false;
            }
            return this == (TPPLPoint)o;
        }

        public override int GetHashCode() {
            return x.GetHashCode() ^ y.GetHashCode();
        }
    };

    // Polygon implemented as an array of points with a 'hole' flag
    public class TPPLPoly {
        private TPPLPoint[] points;
        private bool hole;

        public int NumPoints {
            get => points.Length;
        }

        // Constructors/destructors
        public TPPLPoly() {
            points = new TPPLPoint[0];
            hole = false;
        }

        public TPPLPoly(TPPLPoly src) {
            points = src.points;
            hole = src.hole;
        }
        
        public TPPLPoly(int numpoints) {
            points = new TPPLPoint[numpoints];
            hole = false;
        }

        // Creates a triangle with points p1,p2,p3
        public TPPLPoly(TPPLPoint p1, TPPLPoint p2, TPPLPoint p3) {
            points = new TPPLPoint[] { p1, p2, p3 };
            hole = false;
        }
        
        public bool IsHole() {
            return hole;
        }
        
        public void SetHole(bool hole) {
            this.hole = hole;
        }
        
        public TPPLPoint GetPoint(int i) {
            return points[i];
        }

        public TPPLPoint[] GetPoints() {
            return points;
        }
        
        public TPPLPoint this[int i] {
            get => points[i];
            set { points[i] = value; }
        }

        //checks whether a polygon is valid or not
        public bool Valid() {
            return NumPoints >= 3;
        }
    }

    public class PartitionVertex
    {
        public bool isActive;
        public bool isConvex;
        public bool isEar;

        public TPPLPoint p;
        public float angle;
        public PartitionVertex previous;
        public PartitionVertex next;
    };

    // Helper functions
    private static bool IsConvex(TPPLPoint p1, TPPLPoint p2, TPPLPoint p3) {
        return (p3.y - p1.y) * (p2.x - p1.x) - (p3.x - p1.x) * (p2.y - p1.y) > 0;
    }

    private static bool IsReflex(TPPLPoint p1, TPPLPoint p2, TPPLPoint p3) {
        return (p3.y - p1.y) * (p2.x - p1.x) - (p3.x - p1.x) * (p2.y - p1.y) < 0;
    }

    private static bool IsInside(TPPLPoint p1, TPPLPoint p2, TPPLPoint p3, TPPLPoint p) {
        if (IsConvex(p1, p, p2)) return false;
        if (IsConvex(p2, p, p3)) return false;
        if (IsConvex(p3, p, p1)) return false;
        return true;
    }

    private static bool InCone(TPPLPoint p1, TPPLPoint p2, TPPLPoint p3, TPPLPoint p) {
        bool convex;

        convex = IsConvex(p1, p2, p3);

        if (convex) {
            if (!IsConvex(p1, p2, p)) return false;
            if (!IsConvex(p2, p3, p)) return false;
            return true;
        } else {
            if (IsConvex(p1, p2, p)) return true;
            if (IsConvex(p2, p3, p)) return true;
            return false;
        }
    }

    private static bool Intersects(TPPLPoint p11, TPPLPoint p12, TPPLPoint p21, TPPLPoint p22) {
        if ((p11.x == p21.x) && (p11.y == p21.y)) return false;
        if ((p11.x == p22.x) && (p11.y == p22.y)) return false;
        if ((p12.x == p21.x) && (p12.y == p21.y)) return false;
        if ((p12.x == p22.x) && (p12.y == p22.y)) return false;

        TPPLPoint v1ort = new TPPLPoint();
        TPPLPoint v2ort = new TPPLPoint();
        TPPLPoint v = new TPPLPoint();
        float dot11, dot12, dot21, dot22;

        v1ort.x = p12.y - p11.y;
        v1ort.y = p11.x - p12.x;

        v2ort.x = p22.y - p21.y;
        v2ort.y = p21.x - p22.x;

        v = p21 - p11;
        dot21 = v.x * v1ort.x + v.y * v1ort.y;
        v = p22 - p11;
        dot22 = v.x * v1ort.x + v.y * v1ort.y;

        v = p11 - p21;
        dot11 = v.x * v2ort.x + v.y * v2ort.y;
        v = p12 - p21;
        dot12 = v.x * v2ort.x + v.y * v2ort.y;

        if (dot11 * dot12 > 0) return false;
        if (dot21 * dot22 > 0) return false;

        return true;
    }

    private static TPPLPoint Normalize(TPPLPoint p) {
        TPPLPoint r = new TPPLPoint();
        float n = Mathf.Sqrt(p.x * p.x + p.y * p.y);
        if (n != 0) {
            r = p / n;
        } else {
            r.x = 0;
            r.y = 0;
        }
        return r;
    }
        
    private static void UpdateVertex(PartitionVertex v, PartitionVertex[] vertices, int numvertices) {
        PartitionVertex v1 = v.previous;
        PartitionVertex v3 = v.next;

        v.isConvex = IsConvex(v1.p, v.p, v3.p);

        TPPLPoint vec1 = Normalize(v1.p - v.p);
        TPPLPoint vec3 = Normalize(v3.p - v.p);
        v.angle = vec1.x * vec3.x + vec1.y * vec3.y;

        if (v.isConvex) {
            v.isEar = true;
            for (int i = 0; i < numvertices; i++) {
                if ((vertices[i].p.x == v.p.x) && (vertices[i].p.y == v.p.y)) continue;
                if ((vertices[i].p.x == v1.p.x) && (vertices[i].p.y == v1.p.y)) continue;
                if ((vertices[i].p.x == v3.p.x) && (vertices[i].p.y == v3.p.y)) continue;
                if (IsInside(v1.p, v.p, v3.p, vertices[i].p)) {
                    v.isEar = false;
                    break;
                }
            }
        } else {
            v.isEar = false;
        }
    }
    
    // Simple heuristic procedure for removing holes from a list of polygons
    // works by creating a diagonal from the rightmost hole vertex to some visible vertex
    // time complexity: O(h*(n^2)), h is the number of holes, n is the number of vertices
    // space complexity: O(n)
    // params:
    //    inpolys : a list of polygons that can contain holes
    //              vertices of all non-hole polys have to be in counter-clockwise order
    //              vertices of all hole polys have to be in clockwise order
    //    outpolys : a list of polygons without holes
    // Returns true on success, false on failure
    public static bool RemoveHoles(TPPLPolyList inpolys, out TPPLPolyList outpolys) {
        TPPLPolyList polys = new TPPLPolyList(inpolys);

        // Check for trivial case (no holes)
        bool hasholes = false;
        foreach (var poly in inpolys) {
            if (poly.IsHole()) {
                hasholes = true;
                break;
            }
        }
        if (!hasholes) {
            outpolys = new TPPLPolyList(inpolys);
            return true;
        }

        while (true) {
            int holeIndex = 0;
            int polyIndex = 0;

            int holepointindex = 0;
            int polypointindex = 0;

            //find the hole point with the largest x
            hasholes = false;
            for (int index = 0; index < polys.Count; index++) {
                var poly = polys[index];

                if (!poly.IsHole()) continue;

                if (!hasholes) {
                    hasholes = true;
                    holeIndex = index;
                    holepointindex = 0;
                }

                for (int i = 0; i < poly.NumPoints; i++) {
                    if (poly.GetPoint(i).x > polys[holeIndex].GetPoint(holepointindex).x) {
                        holeIndex = index;
                        holepointindex = i;
                    }
                }
            }
            if (!hasholes) break;
            TPPLPoint holepoint = polys[holeIndex].GetPoint(holepointindex);

            TPPLPoint bestpolypoint = null;
            bool pointfound = false;
            for (int index = 0; index < polys.Count; index++) {
                var poly = polys[index];

                if (poly.IsHole()) continue;
                for (int i = 0; i < poly.NumPoints; i++) {
                    if (poly.GetPoint(i).x <= holepoint.x) continue;
                    if (!InCone(poly.GetPoint((i + poly.NumPoints - 1) % poly.NumPoints),
                        poly.GetPoint(i),
                        poly.GetPoint((i + 1) % poly.NumPoints),
                        holepoint))
                        continue;
                    TPPLPoint polypoint = poly.GetPoint(i);
                    if (pointfound) {
                        TPPLPoint v1 = Normalize(polypoint - holepoint);
                        TPPLPoint v2 = Normalize(bestpolypoint - holepoint);
                        if (v2.x > v1.x) continue;
                    }
                    bool pointvisible = true;
                    foreach (var poly2 in polys) {
                        if (poly2.IsHole()) continue;
                        for (int j = 0; j < poly2.NumPoints; j++) {
                            TPPLPoint linep1 = poly2.GetPoint(j);
                            TPPLPoint linep2 = poly2.GetPoint((j + 1) % poly2.NumPoints);
                            if (Intersects(holepoint, polypoint, linep1, linep2)) {
                                pointvisible = false;
                                break;
                            }
                        }
                        if (!pointvisible) break;
                    }
                    if (pointvisible) {
                        pointfound = true;
                        bestpolypoint = polypoint;
                        polyIndex = index;
                        polypointindex = i;
                    }
                }
            }

            if (!pointfound) {
                outpolys = null;
                return false;
            }

            TPPLPoly newpoly = new TPPLPoly(polys[holeIndex].NumPoints + polys[polyIndex].NumPoints + 2);
            int i2 = 0;
            for (int i = 0; i <= polypointindex; i++) {
                newpoly[i2] = polys[polyIndex].GetPoint(i);
                i2++;
            }
            for (int i = 0; i <= polys[holeIndex].NumPoints; i++) {
                newpoly[i2] = polys[holeIndex].GetPoint((i + holepointindex) % polys[holeIndex].NumPoints);
                i2++;
            }
            for (int i = polypointindex; i < polys[polyIndex].NumPoints; i++) {
                newpoly[i2] = polys[polyIndex].GetPoint(i);
                i2++;
            }

            polys.RemoveAt(holeIndex);
            if (polyIndex > holeIndex) {
                polyIndex--;
            }
            polys.RemoveAt(polyIndex);
            polys.Add(newpoly);
        }

        outpolys = polys;
        return true;
    }

    // Triangulates a polygon by ear clipping
    // time complexity O(n^2), n is the number of vertices
    // space complexity: O(n)
    // params:
    //    poly : an input polygon to be triangulated
    //           vertices have to be in counter-clockwise order
    //    triangles : a list of triangles (result)
    // returns true on success, false on failure
    public static bool Triangulate_EC(TPPLPoly poly, out TPPLPolyList triangles) {
        triangles = new TPPLPolyList();

        if (!poly.Valid()) return false;

        int numvertices = poly.NumPoints;

        if (numvertices < 3) return false;
        if (numvertices == 3) {
            triangles.Add(poly);
            return true;
        }

        PartitionVertex[] vertices = new PartitionVertex[numvertices];
        PartitionVertex ear = null;
        for (int i = 0; i < numvertices; i++) {
            vertices[i] = new PartitionVertex();
        }
        for (int i = 0; i < numvertices; i++) {
            vertices[i].isActive = true;
            vertices[i].p = poly.GetPoint(i);
            if (i == (numvertices - 1)) vertices[i].next = vertices[0];
            else vertices[i].next = vertices[i + 1];
            if (i == 0) vertices[i].previous = vertices[numvertices - 1];
            else vertices[i].previous = vertices[i - 1];
        }
        for (int i = 0; i < numvertices; i++) {
            UpdateVertex(vertices[i], vertices, numvertices);
        }

        for (int i = 0; i < numvertices - 3; i++) {
            bool earfound = false;
            //find the most extruded ear
            for (int j = 0; j < numvertices; j++) {
                if (!vertices[j].isActive) continue;
                if (!vertices[j].isEar) continue;
                if (!earfound) {
                    earfound = true;
                    ear = vertices[j];
                } else {
                    if (vertices[j].angle > ear.angle) {
                        ear = vertices[j];
                    }
                }
            }
            if (!earfound) return false;
            
            triangles.Add(new TPPLPoly(ear.previous.p, ear.p, ear.next.p));

            ear.isActive = false;
            ear.previous.next = ear.next;
            ear.next.previous = ear.previous;

            if (i == numvertices - 4) break;

            UpdateVertex(ear.previous, vertices, numvertices);
            UpdateVertex(ear.next, vertices, numvertices);
        }
        for (int i = 0; i < numvertices; i++) {
            if (vertices[i].isActive) {
                triangles.Add(new TPPLPoly(vertices[i].previous.p, vertices[i].p, vertices[i].next.p));
                break;
            }
        }

        return true;
    }

    // Partitions a polygon into convex polygons by using Hertel-Mehlhorn algorithm
    // the algorithm gives at most four times the number of parts as the optimal algorithm
    // however, in practice it works much better than that and often gives optimal partition
    // uses triangulation obtained by ear clipping as intermediate result
    // time complexity O(n^2), n is the number of vertices
    // space complexity: O(n)
    // params:
    //    poly : an input polygon to be partitioned
    //           vertices have to be in counter-clockwise order
    //    parts : resulting list of convex polygons
    // Returns true on success, false on failure
    public static bool ConvexPartition_HM(TPPLPoly poly, out TPPLPolyList parts) {
        parts = new TPPLPolyList();

        if (!poly.Valid()) return false;

        int i12 = 0;
        int i13 = 0;
        int i21 = 0;
        int i22 = 0;
        int i23 = 0;
        TPPLPoint d1, d2, p1, p2, p3;
        bool isdiagonal;
        long numreflex;

        //check if the poly is already convex
        numreflex = 0;
        for (int i11 = 0; i11 < poly.NumPoints; i11++) {
            if (i11 == 0) i12 = poly.NumPoints - 1;
            else i12 = i11 - 1;
            if (i11 == (poly.NumPoints - 1)) i13 = 0;
            else i13 = i11 + 1;
            if (IsReflex(poly.GetPoint(i12), poly.GetPoint(i11), poly.GetPoint(i13))) {
                numreflex = 1;
                break;
            }
        }
        if (numreflex == 0) {
            parts.Add(poly);
            return true;
        }

        if (!Triangulate_EC(poly, out TPPLPolyList triangles)) return false;

        for (int iter1 = 0; iter1 < triangles.Count; iter1++) {
            TPPLPoly poly1 = triangles[iter1];
            for (int i11 = 0; i11 < poly1.NumPoints; i11++) {
                d1 = poly1.GetPoint(i11);
                i12 = (i11 + 1) % poly1.NumPoints;
                d2 = poly1.GetPoint(i12);
                TPPLPoly poly2 = null;

                isdiagonal = false;
                int iter2;
                for (iter2 = iter1 + 1; iter2 < triangles.Count; iter2++) {
                    poly2 = triangles[iter2];

                    for (i21 = 0; i21 < poly2.NumPoints; i21++) {
                        if ((d2.x != poly2.GetPoint(i21).x) || (d2.y != poly2.GetPoint(i21).y)) continue;
                        i22 = (i21 + 1) % poly2.NumPoints;
                        if ((d1.x != poly2.GetPoint(i22).x) || (d1.y != poly2.GetPoint(i22).y)) continue;
                        isdiagonal = true;
                        break;
                    }
                    if (isdiagonal) break;
                }

                if (!isdiagonal) continue;

                p2 = poly1.GetPoint(i11);
                if (i11 == 0) i13 = poly1.NumPoints - 1;
                else i13 = i11 - 1;
                p1 = poly1.GetPoint(i13);
                if (i22 == (poly2.NumPoints - 1)) i23 = 0;
                else i23 = i22 + 1;
                p3 = poly2.GetPoint(i23);

                if (!IsConvex(p1, p2, p3)) continue;

                p2 = poly1.GetPoint(i12);
                if (i12 == (poly1.NumPoints - 1)) i13 = 0;
                else i13 = i12 + 1;
                p3 = poly1.GetPoint(i13);
                if (i21 == 0) i23 = poly2.NumPoints - 1;
                else i23 = i21 - 1;
                p1 = poly2.GetPoint(i23);

                if (!IsConvex(p1, p2, p3)) continue;

                TPPLPoly newpoly = new TPPLPoly(poly1.NumPoints + poly2.NumPoints - 2);
                int k = 0;
                for (int j = i12; j != i11; j = (j + 1) % poly1.NumPoints) {
                    newpoly[k] = poly1.GetPoint(j);
                    k++;
                }
                for (int j = i22; j != i21; j = (j + 1) % poly2.NumPoints) {
                    newpoly[k] = poly2.GetPoint(j);
                    k++;
                }

                triangles.RemoveAt(iter2);
                if (iter1 > iter2) {
                    iter1--;
                }
                triangles[iter1] = newpoly;
                poly1 = newpoly;
                i11 = -1;

                continue;
            }
        }
        
        parts = triangles;
        return true;
    }
}

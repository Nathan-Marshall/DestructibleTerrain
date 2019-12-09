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

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DestructibleTerrain.HertelMehlhorn
{
    using TPPLPolyList = List<TPPLPoly>;

    public class PolyPartitionHM : IHertelMehlhorn {
        private static readonly Lazy<PolyPartitionHM> lazyInstance = new Lazy<PolyPartitionHM>(() => new PolyPartitionHM());

        // Singleton intance
        public static PolyPartitionHM Instance {
            get { return lazyInstance.Value; }
        }

        private PolyPartitionHM() { }


        public DTMesh ExecuteToMesh(DTMesh input) {
            return ExecuteToMesh(input.ToPolyGroup());
        }

        public DTMesh ExecuteToMesh(DTConvexPolygonGroup input) {
            return ExecuteToPolyGroup(input).ToMesh();
        }

        public DTConvexPolygonGroup ExecuteToPolyGroup(DTMesh input) {
            return ExecuteToPolyGroup(input.ToPolyGroup());
        }

        public DTConvexPolygonGroup ExecuteToPolyGroup(DTConvexPolygonGroup input) {
            HertelMehlhorn(input.ToTPPLPolyList(), out TPPLPolyList output);
            return output.ToPolyGroup();
        }


        // Helper functions
        private static bool IsConvex(Vector2 p1, Vector2 p2, Vector2 p3) {
            return (p3.y - p1.y) * (p2.x - p1.x) - (p3.x - p1.x) * (p2.y - p1.y) > 0;
        }

        // Converts a polygon triangulation to a decomposition of fewer convex partitions by removing
        // some internal edges with the Hertel-Mehlhorn algorithm.
        // The algorithm gives at most four times the number of parts as the optimal algorithm,
        // though in practice it works much better than that and often gives optimal partition.
        // time complexity O(n^2), n is the number of vertices
        // space complexity: O(n)
        // params:
        //    triangles : a triangulation of a polygon
        //           vertices have to be in counter-clockwise order
        //    parts : resulting list of convex polygons
        // Returns true on success, false on failure
        private static bool HertelMehlhorn(TPPLPolyList triangles, out TPPLPolyList parts) {
            int i11;
            int i12;
            int i13;
            int i21 = 0;
            int i22 = 0;
            int i23;
            Vector2 d1, d2, p1, p2, p3;
            bool isdiagonal;

            for (int iter1 = 0; iter1 < triangles.Count; iter1++) {
                TPPLPoly poly1 = triangles[iter1];
                for (i11 = 0; i11 < poly1.NumPoints; i11++) {
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

    // Polygon implemented as an array of points with a 'hole' flag
    class TPPLPoly
    {
        private Vector2[] points;
        private bool hole;

        public int NumPoints {
            get => points.Length;
        }

        // Constructors/destructors
        public TPPLPoly() {
            points = new Vector2[0];
            hole = false;
        }

        public TPPLPoly(TPPLPoly src) {
            points = src.points;
            hole = src.hole;
        }

        public TPPLPoly(int numpoints) {
            points = new Vector2[numpoints];
            hole = false;
        }

        // Creates a triangle with points p1,p2,p3
        public TPPLPoly(Vector2 p1, Vector2 p2, Vector2 p3) {
            points = new Vector2[] { p1, p2, p3 };
            hole = false;
        }

        public bool IsHole() {
            return hole;
        }

        public void SetHole(bool hole) {
            this.hole = hole;
        }

        public Vector2 GetPoint(int i) {
            return points[i];
        }

        public Vector2[] GetPoints() {
            return points;
        }

        public Vector2 this[int i] {
            get => points[i];
            set { points[i] = value; }
        }

        //checks whether a polygon is valid or not
        public bool Valid() {
            return NumPoints >= 3;
        }
    }

    static class ExtensionsForPolyPartitionHM
    {
        public static TPPLPolyList ToTPPLPolyList(this DTConvexPolygonGroup polyGroup) {
            TPPLPolyList polyList = new TPPLPolyList();

            foreach (var poly in polyGroup) {
                TPPLPoly tpplHole = new TPPLPoly(poly.Count);
                tpplHole.SetHole(true);
                for (int i = 0; i < poly.Count; i++) {
                    tpplHole[poly.Count - 1 - i] = poly[i];
                }
                polyList.Add(tpplHole);
            }

            return polyList;
        }

        public static DTConvexPolygonGroup ToPolyGroup(this TPPLPolyList polyList) {
            return new DTConvexPolygonGroup(polyList.Select(tpplPoly => tpplPoly.GetPoints().Select(
                p => new Vector2(p.x, p.y)).ToList()).ToList());
        }
    }
}

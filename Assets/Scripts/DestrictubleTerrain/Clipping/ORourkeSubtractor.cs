using ClipperLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;

namespace DestructibleTerrain.Clipping
{
    public sealed class ORourkeSubtractor : IPolygonSubtractor
    {
        struct Intersection
        {
            public Vector2 position;
            public float at;
            public float bt;

            public Intersection(Vector2 position, float at, float bt) {
                this.position = position;
                this.at = at;
                this.bt = bt;
            }
        }

        struct ORourkeIntersection
        {
            public Vector2 position;
            public int pIndex;
            public int qIndex;
            public float pt;
            public float qt;

            public ORourkeIntersection(Vector2 position, int pIndex, int qIndex, float pt, float qt) {
                this.position = position;
                this.pIndex = pIndex;
                this.qIndex = qIndex;
                this.pt = pt;
                this.qt = qt;
            }
        }

        delegate bool BinarySearchDirectionCheck(int i);

        private static readonly Lazy<ORourkeSubtractor> lazyInstance = new Lazy<ORourkeSubtractor>(() => new ORourkeSubtractor());

        // Singleton intance
        public static ORourkeSubtractor Instance {
            get { return lazyInstance.Value; }
        }

        DTPolygon polyP;
        DTPolygon polyQ;
        DTPolygon inside;
        int pIndex;
        int qIndex;

        Vector2 PPrev {
            get { return polyP.V(pIndex - 1); }
        }
        Vector2 P {
            get {
                return polyP.V(pIndex);
            }
        }
        Vector2 PEdge {
            get {
                return P - PPrev;
            }
        }

        Vector2 QPrev {
            get { return polyQ.V(qIndex - 1); }
        }
        Vector2 Q {
            get { return polyQ.V(qIndex); }
        }
        Vector2 QEdge {
            get {
                return Q - QPrev;
            }
        }


        private ORourkeSubtractor() {}

        public List<DTPolygon> Subtract(DTPolygon subject, DTPolygon clippingPolygon) {
            polyP = subject;
            polyQ = clippingPolygon;
            inside = null;
            pIndex = 0;
            qIndex = 0;
            bool foundFirstIntersectionPreviousIteration = false;
            bool finished = false;
            List<Vector2> intersectionVertices = new List<Vector2>();

            for (int i = 0; i <= 2*(polyP.Contour.Count + polyQ.Contour.Count); ++i) {
                Intersection? intersection = PQIntersection();
                if (intersection.HasValue) {
                    if (intersectionVertices.Count > 0 && intersection.Value.position == intersectionVertices[0]
                            && !foundFirstIntersectionPreviousIteration) {
                        finished = true;
                        break;
                    } else {
                        intersectionVertices.Add(intersection.Value.position);
                        foundFirstIntersectionPreviousIteration = intersectionVertices.Count == 1;
                        inside = InHalfPlaneQ(P) ? polyP : polyQ;
                    }
                }
                if (QEdge.Cross(PEdge) >= 0) {
                    if (InHalfPlaneQ(P)) {
                        // Advance Q
                        if (inside == polyQ) {
                            intersectionVertices.Add(Q);
                            foundFirstIntersectionPreviousIteration = false;
                        }
                        ++qIndex;
                    } else {
                        // Advance P
                        if (inside == polyP) {
                            intersectionVertices.Add(P);
                            foundFirstIntersectionPreviousIteration = false;
                        }
                        ++pIndex;
                    }
                } else {
                    if (InHalfPlaneP(Q)) {
                        // Advance P
                        if (inside == polyP) {
                            intersectionVertices.Add(P);
                            foundFirstIntersectionPreviousIteration = false;
                        }
                        ++pIndex;
                    }
                    else {
                        // Advance Q
                        if (inside == polyP) {
                            intersectionVertices.Add(Q);
                            foundFirstIntersectionPreviousIteration = false;
                        }
                        ++qIndex;
                    }
                }
            }
            if (!finished) {
                if (polyQ.Contains(P)) {
                    intersectionVertices.AddRange(polyP.Contour);
                } else if (polyP.Contains(Q)) {
                    intersectionVertices.AddRange(polyQ.Contour);
                }
            }

            // intersectionVertices now contains all intersection vertices

            return null;
        }
        
        public List<List<DTPolygon>> SubtractPolyGroup(IEnumerable<DTPolygon> inputPolyGroup, IEnumerable<DTPolygon> clippingPolygons) {
            return null;
        }

        public List<List<List<DTPolygon>>> SubtractBulk(IEnumerable<IEnumerable<DTPolygon>> inputPolyGroups, IEnumerable<DTPolygon> clippingPolygons) {
            return null;
        }

        // Note: does not verify if the polygon is convex, but checks that there are at least 3 vertices, no holes, and
        // that - assuming the polygon is convex - it is in CCW order
        private bool IsProbablyValid(DTPolygon poly) {
            if (poly == null) {
                return false;
            }

            if (poly.Contour.Count < 3) {
                return false;
            }

            if (poly.Holes.Count > 0) {
                return false;
            }

            Vector2 v0 = poly.Contour[1] - poly.Contour[0];
            Vector2 v1 = poly.Contour[2] - poly.Contour[1];

            // Return false if the first 3 vertices are CW. We will assume the polygon is convex and that the rest of
            // the vertices follow the same wrapping direction as these 3
            if (v0.Cross(v1) < 0) {
                return false;
            }

            return true;
        }

        private bool InHalfPlaneP(Vector2 x) {
            return PEdge.Cross(x - PPrev) >= 0;
        }

        private bool InHalfPlaneQ(Vector2 x) {
            return QEdge.Cross(x - QPrev) >= 0;
        }

        private Intersection? PQIntersection() {
            return SegmentSegmentIntersection(PPrev, P, QPrev, Q);
        }

        private Intersection? SegmentSegmentIntersection(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1) {
            // Starting from the formula,
            // A0 + at*(A1 - A0) = B0 + bt*(B1 - B0)

            // We can get values t and u like so:
            // at = (B0 - Q0) x (B1 - B0) / (A1 - A0) x (B1 - B0)
            // bt = (B0 - Q0) x (A1 - A0) / (A1 - A0) x (B1 - B0)

            float aCrossB = (a1 - a0).Cross(b1 - b0);

            // Note that we consider overlapping segments to NEVER intersect in O'Rourke's algorithm
            if (aCrossB == 0) {
                return null;
            }

            float diffCrossA = (b0 - a0).Cross(a1 - a0);
            float diffCrossB = (b0 - a0).Cross(b1 - b0);

            float at = diffCrossB / aCrossB;
            float bt = diffCrossA / aCrossB;

            if (0 <= at && at <= 1 && 0 <= bt && bt <= 1) {
                Vector2 intersectionPoint = a0 + at * (a1 - a0);
                return new Intersection(intersectionPoint, at, bt);
            }
            else {
                return null;
            }
        }
    }

    static class ExtensionsForYangSubtractor
    {
        public static float Dot(this Vector2 a, Vector2 b) {
            return Vector2.Dot(a, b);
        }

        public static float Cross(this Vector2 a, Vector2 b) {
            return (a.x * b.y) - (a.y * b.x);
        }

        public static Vector2 V(this DTPolygon poly, int i) {
            return poly.Contour.GetCircular(i);
        }

        public static bool Contains(this DTPolygon poly, Vector2 point) {
            for (int i = 0; i < poly.Contour.Count; i++) {
                if ((poly.V(i) - poly.V(i - 1)).Cross(point - poly.V(i - 1)) < 0) {
                    return false;
                }
            }
            return true;
        }
    }
}

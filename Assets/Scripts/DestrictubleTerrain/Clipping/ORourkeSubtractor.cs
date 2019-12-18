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
        delegate bool BinarySearchDirectionCheck(int i);

        private static readonly Lazy<ORourkeSubtractor> lazyInstance = new Lazy<ORourkeSubtractor>(() => new ORourkeSubtractor());

        // Singleton intance
        public static ORourkeSubtractor Instance {
            get { return lazyInstance.Value; }
        }

        private struct SegmentSegmentIntersectionResult
        {
            public Vector2 position;
            public float at;
            public float bt;

            public SegmentSegmentIntersectionResult(Vector2 position, float at, float bt) {
                this.position = position;
                this.at = at;
                this.bt = bt;
            }
        }

        DTPolygon polyP;
        DTPolygon polyQ;
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
            pIndex = 0;
            qIndex = 0;

            // True if Q is in the half plane of PEdge
            bool qInside = false;

            // The first entry intersection point of the first output polgon. If we return to this point, break the loop
            bool firstIntersectionFound = false;
            int? firstIntersectionPIndex = null;
            int? firstIntersectionQIndex = null;

            // A flag to indicate whether firstIntersection was found in the previous loop iteration.
            // This is to handle a degenerate case, and is specifically mentioned in O'Rourke's paper.
            bool firstIntersectionFoundPreviousIteration = false;

            // List of disjoint output polygons after subtraction
            List<DTPolygon> outputPolygons = new List<DTPolygon>();

            // Working output polygon vertices
            Vector2? polygonBegin = null;
            List<Vector2> pVertices = new List<Vector2>();
            List<Vector2> qVertices = new List<Vector2>();

            for (int i = 0; i <= 2*(polyP.Contour.Count + polyQ.Contour.Count); ++i) {
                Vector2? intersection = PQIntersection();
                if (intersection.HasValue) {
                    if (firstIntersectionFound && !firstIntersectionFoundPreviousIteration
                            && ModP(pIndex) == ModP(firstIntersectionPIndex.Value)
                            && ModQ(qIndex) == ModQ(firstIntersectionQIndex.Value)) {
                        break;
                    }
                    else {
                        // This flag can be cleared, since we just checked it, and it should be checked only once after being set
                        firstIntersectionFoundPreviousIteration = false;

                        qInside = InHalfPlaneP(Q);
                        if (qInside) {
                            // Entry intersection point for output polygon
                            polygonBegin = intersection.Value;

                            // Keep track of this point if it is the entry intersection for the 1st output polygon
                            if (!firstIntersectionFound) {
                                firstIntersectionFound = true;
                                firstIntersectionFoundPreviousIteration = true;
                                firstIntersectionPIndex = pIndex;
                                firstIntersectionQIndex = qIndex;
                            }
                        }
                        else if (polygonBegin.HasValue) {
                            // Exit intersection point for output polygon: construct output polygon
                            DTPolygon poly = new DTPolygon();
                            poly.Contour.Add(polygonBegin.Value);
                            poly.Contour.AddRange(pVertices);
                            poly.Contour.Add(intersection.Value);
                            poly.Contour.AddRange(qVertices.AsEnumerable().Reverse());
                            outputPolygons.Add(poly);

                            // Clear working polygon vertices
                            polygonBegin = null;
                            pVertices.Clear();
                            qVertices.Clear();
                        }
                    }
                }
                else {
                    // This flag can be cleared if there is no intersection this iteration
                    firstIntersectionFoundPreviousIteration = false;
                }

                if (QEdge.Cross(PEdge) >= 0) {
                    if (InHalfPlaneQ(P)) {
                        // Advance Q
                        if (qInside) {
                            qVertices.Add(Q);
                        }
                        ++qIndex;
                    } else {
                        // Advance P
                        if (qInside) {
                            pVertices.Add(P);
                        }
                        ++pIndex;
                    }
                } else {
                    if (InHalfPlaneP(Q)) {
                        // Advance P
                        if (qInside) {
                            pVertices.Add(P);
                        }
                        ++pIndex;
                    }
                    else {
                        // Advance Q
                        if (qInside) {
                            qVertices.Add(Q);
                        }
                        ++qIndex;
                    }
                }
            }

            // There were no intersections, so either one poly is entirely contained within the other, or there is no overlap at all
            if (outputPolygons.Count == 0) {
                if (polyP.Inside(polyQ)) {
                    // P is entirely within Q, so do nothing, the entire polygon has been subtracted!
                } else if (polyQ.Inside(polyP)) {
                    // Q is entirely within P, so output a copy of P, with Q (reversed) set as a hole
                    outputPolygons.Add(new DTPolygon(
                        new List<Vector2>(polyP.Contour),
                        new List<List<Vector2>>() {
                            polyQ.Contour.AsEnumerable().Reverse().ToList()
                        }));
                } else {
                    // There is no overlap at all, so output a copy of P
                    outputPolygons.Add(new DTPolygon(new List<Vector2>(polyP.Contour)));
                }
            }

            return outputPolygons;
        }
        
        public List<List<DTPolygon>> SubtractPolyGroup(IEnumerable<DTPolygon> inputPolyGroup, IEnumerable<DTPolygon> clippingPolygons) {
            List<List<DTPolygon>> outputPolyGroups = new List<List<DTPolygon>>(inputPolyGroup.Select(
                    poly => new List<DTPolygon>() { poly }));
            int i = 0;
            foreach (DTPolygon inputPoly in inputPolyGroup) {
                List<DTPolygon> workingPolyGroup = outputPolyGroups[i];
                foreach (DTPolygon clippingPoly in clippingPolygons) {
                    List<DTPolygon> newWorkingPolyGroup = new List<DTPolygon>();
                    foreach (DTPolygon workingPoly in workingPolyGroup) {
                        newWorkingPolyGroup.AddRange(Subtract(workingPoly, clippingPoly));
                    }
                    workingPolyGroup = newWorkingPolyGroup;
                }
                outputPolyGroups[i] = workingPolyGroup;
                ++i;
            }
            return outputPolyGroups;
        }

        public List<List<List<DTPolygon>>> SubtractBulk(IEnumerable<IEnumerable<DTPolygon>> inputPolyGroups, IEnumerable<DTPolygon> clippingPolygons) {
            List<List<List<DTPolygon>>> outputPolyGroupGroups = new List<List<List<DTPolygon>>>();
            foreach (IEnumerable<DTPolygon> inputPolyGroup in inputPolyGroups) {
                outputPolyGroupGroups.Add(SubtractPolyGroup(inputPolyGroup, clippingPolygons));
            }
            return outputPolyGroupGroups;
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

        private Vector2? PQIntersection() {
            SegmentSegmentIntersectionResult? result = SegmentSegmentIntersection(PPrev, P, QPrev, Q);
            // Prevent intersections in degenerate cases where a vertex from one polygon is contained in the edge of another,
            // but the areas do not intersect.
            if (result.HasValue && result.Value.at > 0 && result.Value.bt < 1) {
                return result.Value.position;
            }
            return null;
        }

        private SegmentSegmentIntersectionResult? SegmentSegmentIntersection(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1) {
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
                return new SegmentSegmentIntersectionResult(a0 + at * (a1 - a0), at, bt);
            }
            else {
                return null;
            }
        }

        int ModP(int pi) {
            return ((pi % polyP.Contour.Count) + polyP.Contour.Count) % polyP.Contour.Count;
        }

        int ModQ(int qi) {
            return ((qi % polyQ.Contour.Count) + polyQ.Contour.Count) % polyQ.Contour.Count;
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

        public static bool Inside(this DTPolygon polyA, DTPolygon polyB) {
            foreach (Vector2 a in polyA.Contour) {
                bool? aInside = a.Inside(polyB);
                if (aInside.HasValue) {
                    return aInside.Value;
                }
            }
            return false;
        }

        public static bool? Inside(this Vector2 point, DTPolygon poly) {
            for (int i = 0; i < poly.Contour.Count; i++) {
                float cross = (poly.V(i) - poly.V(i - 1)).Cross(point - poly.V(i - 1));
                if (cross < 0) {
                    return false;
                } else if (cross == 0) {
                    return null;
                }
            }
            return true;
        }
    }
}

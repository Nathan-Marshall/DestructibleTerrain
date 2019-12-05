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
    enum AnglePosition
    {
        Inside,
        Outside,
        FirstEdge,
        SecondEdge,
        Joint
    }

    enum Quadrant
    {
        Zero,
        SameDir,
        CCWForward,
        CCWPerp,
        CCWBackward,
        OppDir,
        CWBackward,
        CWPerp,
        CWForward
    }

    enum RegionType
    {
        None,
        Vertex,
        Edge,
        VoronoiEdge,
        VoronoiRegion
    }

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

    public sealed class YangSubtractor : IPolygonSubtractor
    {
        delegate bool BinarySearchDirectionCheck(int i);

        private static readonly Lazy<YangSubtractor> lazyInstance = new Lazy<YangSubtractor>(() => new YangSubtractor());

        // Singleton intance
        public static YangSubtractor Instance {
            get { return lazyInstance.Value; }
        }


        DTPolygon polyP;
        DTPolygon polyQ;
        int pIndex;
        float pt;
        int qIndex;
        RegionType regionType;

        Vector2 S {
            get {
                if (pt == 0) {
                    return polyP.V(pIndex);
                } else {
                    return polyP.V(pIndex) + pt * (polyP.V(pIndex + 1) - polyP.V(pIndex));
                }
            }
        }
        Vector2 Sn {
            get { return polyP.V(pIndex + 1); }
        }

        Vector2 Qp {
            get { return polyQ.V(qIndex - 1); }
        }
        Vector2 Q {
            get { return polyQ.V(qIndex); }
        }
        Vector2 Qn {
            get { return polyQ.V(qIndex + 1); }
        }
        Vector2 Qnn {
            get { return polyQ.V(qIndex + 2); }
        }


        private YangSubtractor() {}

        public List<DTPolygon> Subtract(DTPolygon subject, DTPolygon clippingPolygon) {
            polyP = subject;
            polyQ = clippingPolygon;
            FindInitialState();

            Process();

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

        private void Process() {
            switch (regionType) {
                case RegionType.Edge: // intentional fall-through to RegionType.VoronoiRegion
                case RegionType.VoronoiRegion:
                    if (SegmentContains(S, Sn, Q)) {
                        // VN1: Q is in S_Sn

                        SetPt(Q);
                        regionType = RegionType.Vertex;
                        break;
                    }
                    else if (SegmentContains(S, Sn, Qn)) {
                        // VN2: Qn is in S_Sn

                        SetPt(Qn);
                        regionType = RegionType.Vertex;
                        break;
                    }

                    Intersection? qIntersection = SegmentSegmentIntersection(S, Sn, Q, Qn);
                    if (qIntersection.HasValue) {
                        // VN3: S_Sn intersects Q_Qn

                        // This is the only case where we actually handle an intersection of the two polygons!

                        break;
                    }

                    Intersection? nextVEIntersection = SegmentRayIntersection(S, Sn, Qn, (Qnn - Qn).CWPerp());
                    if (nextVEIntersection.HasValue) {
                        // VN4: S_Sn intersects VE(Qn)

                        ++qIndex;
                        break;
                    }

                    Intersection? VEIntersection = SegmentRayIntersection(S, Sn, Q, (Qn - Q).CWPerp());
                    if (VEIntersection.HasValue) {
                        // VN5: S_Sn intersects VE(Q)

                        --qIndex;
                        break;
                    }
                    else {
                        // VN6: Otherwise

                        ++pIndex;
                        pt = 0;
                        break;
                    }

                case RegionType.Vertex:
                    Quadrant snQuad = Q.CalcQuadrant(Qn, Sn);
                    if (snQuad == Quadrant.SameDir || snQuad == Quadrant.CWForward) {
                        // V1: S_Sn overlaps with Q_Qn
                        // or V4: S_Sn is between Q_Qn and VE(Q)

                        regionType = RegionType.VoronoiRegion;
                        break;
                    }

                    AnglePosition snPrevAnglePosition = GetAnglePosition(Qp, Q, Q + (Qn - Q).CWPerp(), Sn);
                    if (snPrevAnglePosition == AnglePosition.FirstEdge || snPrevAnglePosition == AnglePosition.Outside) {
                        // V2: S_Sn overlaps with Qp_Q
                        // or V5: S_Sn is between Qp_Q and VE(Q)

                        --qIndex;
                        regionType = RegionType.VoronoiRegion;
                        break;
                    }
                    else if (snQuad == Quadrant.CWPerp) {
                        // V3: S_Sn overlaps with VE(Q)

                        regionType = RegionType.VoronoiEdge;
                        break;
                    }
                    else {
                        throw new Exception("Error: failed to handle case where S is on Q");
                    }

                case RegionType.VoronoiEdge:
                    if (SegmentContains(S, Sn, Q)) {
                        // VE1: S_Sn contains Q

                        SetPt(Q);
                        regionType = RegionType.Vertex;
                        break;
                    }

                    float cross = (Sn - S).Cross((Qn - Q).CWPerp());
                    if (cross < 0) {
                        // VE2: S_Sn x VE(Q) < 0

                        regionType = RegionType.VoronoiRegion;
                        break;
                    }
                    else if (cross > 0) {
                        // VE3: S_Sn x VE(Q) < 0

                        --qIndex;
                        regionType = RegionType.VoronoiRegion;
                        break;
                    }
                    else {
                        // VE4: otherwise

                        ++pIndex;
                        pt = 0;
                        break;
                    }

                default: // intentional fall-through to RegionType.None
                case RegionType.None:
                    throw new Exception("TODO: Implement case that polyQ is entirely within polyP.");
            }
        }

        private void SwapPQ() {
            DTPolygon tempPoly = polyP;
            polyP = polyQ;
            polyQ = tempPoly;

            int tempIndex = pIndex;
            pIndex = qIndex;
            qIndex = tempIndex;

            pt = 0;
        }

        private void SetPt(Vector2 newS) {
            Vector2 originalS = polyP.V(pIndex);
            pt = (newS - originalS).magnitude / (Sn - originalS).magnitude;
        }

        private void FindInitialState() {
            pIndex = RightmostVertex(polyP);
            qIndex = RightmostVertex(polyQ);
            pt = 0;

            if (S.x < Q.x) {
                SwapPQ();
            }

            SetInitialRegion();
        }

        private int BinarySearch(DTPolygon poly, BinarySearchDirectionCheck directionCheck) {
            int a = 0;
            int b = poly.Contour.Count - 1;
            return BinarySearch(directionCheck, a, b);
        }

        private int BinarySearch(BinarySearchDirectionCheck directionCheck, int a, int b) {
            while (true) {
                int m = a + (b - a) / 2;

                if (m == a) {
                    return m;
                }

                if (directionCheck(m)) {
                    a = m;
                }
                else {
                    b = m;
                }
            }
        }

        // Note: if there are multiple consecutive vertical edges along the right side, this may return any vertex
        // among those edges
        private int RightmostVertex(DTPolygon poly) {
            bool rightward(int i) {
                return poly.V(i + 1).x > poly.V(i).x || (poly.V(i + 1).x == poly.V(i).x && poly.V(i + 1).y > poly.V(i).y);
            }
            return BinarySearch(poly, rightward);
        }

        // Note: if there are multiple consecutive vertical edges along the top side, this may return any vertex
        // among those edges
        private int TopVertex(DTPolygon poly) {
            bool upward(int i) {
                return poly.V(i + 1).y > poly.V(i).y || (poly.V(i + 1).y == poly.V(i).y && poly.V(i + 1).x < poly.V(i).x);
            }
            return BinarySearch(poly, upward);
        }

        // Note: if there are multiple consecutive vertical edges along the bottom side, this may return any vertex
        // among those edges
        private int BottomVertex(DTPolygon poly) {
            bool downward(int i) {
                return poly.V(i + 1).y > poly.V(i).y || (poly.V(i + 1).y == poly.V(i).y && poly.V(i + 1).x > poly.V(i).x);
            }
            return BinarySearch(poly, downward);
        }

        private void SetInitialRegion() {
            bool aboveOrOnVoronoiEdge(int i) {
                return Vector2.Dot(polyQ.V(i + 1) - polyQ.V(i), S - polyQ.V(i)) >= 0;
            }

            int n = polyQ.Contour.Count;

            int a = BottomVertex(polyQ);
            int b = TopVertex(polyQ);

            if (b < a) {
                b += n;
            }

            qIndex = BinarySearch(aboveOrOnVoronoiEdge, a, b);

            regionType = GetRegionType(Q, Qn, Qnn, S);
        }

        private bool SegmentContains(Vector2 a0, Vector2 a1, Vector2 b) {
            return b == a0
                || b == a1
                || ((b - a0).Dot(a1 - a0) == 1 && (b - a0).sqrMagnitude <= (a1 - a0).sqrMagnitude);
        }

        private Intersection? SegmentSegmentIntersection(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1) {
            // Starting from the formula,
            // A0 + at*(A1 - A0) = B0 + bt*(B1 - B0)

            // We can get values t and u like so:
            // at = (B0 - Q0) x (B1 - B0) / (A1 - A0) x (B1 - B0)
            // bt = (B0 - Q0) x (A1 - A0) / (A1 - A0) x (B1 - B0)

            float diffCrossA = (b0 - a0).Cross(a1 - a0);
            float diffCrossB = (b0 - a0).Cross(b1 - b0);
            float aCrossB = (a1 - a0).Cross(b1 - b0);

            float at = diffCrossB / aCrossB;
            float bt = diffCrossA / aCrossB;

            if (0 <= at && at <= 1 && 0 <= bt && bt <= 1) {
                Vector2 intersectionPoint = a0 + at * (a1 - a0);
                return new Intersection(intersectionPoint, at, bt);
            } else {
                return null;
            }
        }

        private Intersection? SegmentRayIntersection(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 bDir) {
            // Starting from the formula,
            // A0 + at*(A1 - A0) = B0 + bt*(BDir)

            // We can get values t and u like so:
            // at = (B0 - A0) x (BDir) / (A1 - A0) x (BDir)
            // bt = (B0 - A0) x (A1 - A0) / (A1 - A0) x (BDir)

            float diffCrossA = (b0 - a0).Cross(a1 - a0);
            float diffCrossB = (b0 - a0).Cross(bDir);
            float aCrossB = (a1 - a0).Cross(bDir);

            float at = diffCrossB / aCrossB;
            float bt = diffCrossA / aCrossB;

            if (0 <= at && at <= 1 && 0 <= bt) {
                Vector2 intersectionPoint = a0 + at*(a1 - a0);
                return new Intersection(intersectionPoint, at, bt);
            } else {
                return null;
            }
        }

        private RegionType GetRegionType(Vector2 q, Vector2 qn, Vector2 qnn, Vector2 p) {
            if (q == p) {
                return RegionType.Vertex;
            }

            Quadrant quad = q.CalcQuadrant(qn, p);
            Quadrant quadNext = qn.CalcQuadrant(qnn, p);

            if (quad == Quadrant.CWPerp) {
                return RegionType.VoronoiEdge;
            } else if (quad == Quadrant.SameDir && (p - q).sqrMagnitude < (qn - q).sqrMagnitude) {
                return RegionType.Edge;
            } else if ((quad == Quadrant.CWForward && quadNext != Quadrant.CWForward && quadNext != Quadrant.CWPerp)
                    || (quad.Forward() && quadNext == Quadrant.CWBackward)) {
                return RegionType.VoronoiRegion;
            } else {
                return RegionType.None;
            }
        }

        private AnglePosition GetAnglePosition(Vector2 a0, Vector2 a1, Vector2 a2, Vector2 b) {
            float cross0 = (b - a0).Cross(a1 - a0);
            float cross1 = (b - a0).Cross(a2 - a1);

            if (b == a1) {
                return AnglePosition.Joint;
            }
            else if ((a1 - a0).Cross(a2 - a1) > 0) {
                // If angle wraps ccw (interior angle less than pi), then both crosses need to be positive to be inside
                if (cross0 > 0 && cross1 > 0) {
                    return AnglePosition.Inside;
                }
                else if (cross0 == 0 && cross1 > 0) {
                    return AnglePosition.FirstEdge;
                }
                else if (cross0 > 0 && cross1 == 0) {
                    return AnglePosition.SecondEdge;
                }
                else {
                    return AnglePosition.Outside;
                }
            }
            else {
                // If angle wraps cw (interior angle greater than pi), then only one cross needs to be positive to be inside
                if (cross0 > 0 || cross1 > 0) {
                    return AnglePosition.Inside;
                }
                else if (cross0 == 0 && cross1 < 0) {
                    return AnglePosition.FirstEdge;
                }
                else if (cross0 < 0 && cross1 == 0) {
                    return AnglePosition.SecondEdge;
                }
                else {
                    return AnglePosition.Outside;
                }
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

        public static Vector2 CCWPerp(this Vector2 v) {
            return new Vector2(-v.y, v.x);
        }

        public static Vector2 CWPerp(this Vector2 v) {
            return new Vector2(v.y, -v.x);
        }

        public static Vector2 V(this DTPolygon poly, int i) {
            return poly.Contour.GetCircular(i);
        }

        public static Quadrant QuadrantOf(this Vector2 a, Vector2 b) {
            if (a == b) {
                return Quadrant.Zero;
            }

            Vector2 aNorm = a.normalized;
            Vector2 bNorm = b.normalized;

            float dot = aNorm.Dot(bNorm);
            float cross = aNorm.Cross(bNorm);

            if (dot == 1) {
                return Quadrant.SameDir;
            }
            else if (dot == -1) {
                return Quadrant.OppDir;
            }
            else if (cross == 1) {
                return Quadrant.CCWPerp;
            }
            else if (cross == -1) {
                return Quadrant.CWPerp;
            }
            else if (dot > 0) {
                if (cross > 0) {
                    return Quadrant.CCWForward;
                }
                else {
                    return Quadrant.CWForward;
                }
            }
            else {
                if (cross > 0) {
                    return Quadrant.CCWBackward;
                }
                else {
                    return Quadrant.CWBackward;
                }
            }
        }

        public static Quadrant CalcQuadrant(this Vector2 src, Vector2 dstA, Vector2 dstB) {
            return QuadrantOf(dstA - src, dstB - src);
        }

        public static bool Parallel(this Quadrant quad, bool includeSamePoint = false) {
            return quad == Quadrant.SameDir || quad == Quadrant.OppDir || (includeSamePoint && quad == Quadrant.Zero);
        }

        public static bool Perpendicular(this Quadrant quad, bool includeSamePoint = false) {
            return quad == Quadrant.CCWPerp || quad == Quadrant.CWPerp || (includeSamePoint && quad == Quadrant.Zero);
        }

        public static bool Forward(this Quadrant quad, bool includePerpendicular = false, bool includeSamePoint = false) {
            return quad == Quadrant.CCWForward || quad == Quadrant.SameDir || quad == Quadrant.CWForward
                || (includePerpendicular && quad.Perpendicular(includeSamePoint));
        }

        public static bool Backward(this Quadrant quad, bool includePerpendicular = false, bool includeSamePoint = false) {
            return quad == Quadrant.CCWBackward || quad == Quadrant.OppDir || quad == Quadrant.CWBackward
                || (includePerpendicular && quad.Perpendicular(includeSamePoint));
        }

        public static bool CCW(this Quadrant quad, bool includeParallel = false, bool includeSamePoint = false) {
            return quad == Quadrant.CCWForward || quad == Quadrant.CCWPerp || quad == Quadrant.CCWBackward
                || (includeParallel && quad.Parallel(includeSamePoint));
        }

        public static bool CW(this Quadrant quad, bool includeParallel = false, bool includeSamePoint = false) {
            return quad == Quadrant.CWForward || quad == Quadrant.CWPerp || quad == Quadrant.CWBackward
                || (includeParallel && quad.Parallel(includeSamePoint));
        }
    }
}

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
    public sealed class YangSubtractor : IPolygonSubtractor
    {
        enum RegionType
        {
            None,
            Vertex,
            VoronoiEdge,
            VoronoiRegion
        }

        delegate bool BinarySearchDirectionCheck(int i);

        private static readonly Lazy<YangSubtractor> lazyInstance = new Lazy<YangSubtractor>(() => new YangSubtractor());

        // Singleton intance
        public static YangSubtractor Instance {
            get { return lazyInstance.Value; }
        }


        DTPolygon polyP;
        DTPolygon polyQ;
        int pIndex;
        int qIndex;
        RegionType regionType;

        Vector2 P_i {
            get { return polyP.V(pIndex); }
        }
        Vector2 P_i_next {
            get { return polyP.V(pIndex + 1); }
        }
        Vector2 Q_i_prev {
            get { return polyQ.V(qIndex - 1); }
        }
        Vector2 Q_i {
            get { return polyQ.V(qIndex); }
        }
        Vector2 Q_i_next {
            get { return polyQ.V(qIndex + 1); }
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
            if (Cross(v0, v1) < 0) {
                return false;
            }

            return true;
        }

        private void Process() {
            regionType = GetRegionType(pIndex, qIndex);

            if (regionType == RegionType.None) {
                throw new Exception("TODO: Implement case that polyQ is entirely within polyP.");
            }

            Vector2 pEdge = P_i_next - P_i;
            Vector2 pEdgeNorm = pEdge.normalized;
            Vector2 qEdgePrev = Q_i - Q_i_prev;
            Vector2 qEdgePrevNorm = qEdgePrev.normalized;
            Vector2 qEdge = Q_i_next - Q_i;
            Vector2 qEdgeNorm = qEdge.normalized;

            switch (regionType) {
                case RegionType.None:
                    break;

                case RegionType.Vertex:
                    RegionType regionTypePNext = GetRegionType(pIndex + 1, qIndex);

                    /////////////////////////////////////////////////////////////////////////////////// Use GetRegionType() instead!!!!!!!!
                    float dot = Vector2.Dot(pEdgeNorm, qEdgeNorm);
                    float prevDot = Vector2.Dot(pEdgeNorm, qEdgePrevNorm);
                    float cross = Cross(pEdgeNorm, qEdgeNorm);

                    if (dot == 1) {

                    } else if (prevDot == -1) {

                    } else if (cross == 1) {

                    }
                    break;

                case RegionType.VoronoiEdge:

                    break;

                case RegionType.VoronoiRegion:

                    break;

                default:
                    break;
            }
        }

        private void SwapPQ() {
            DTPolygon tempPoly = polyP;
            polyP = polyQ;
            polyQ = tempPoly;

            int tempIndex = pIndex;
            pIndex = qIndex;
            qIndex = tempIndex;
        }

        private void FindInitialState() {
            pIndex = RightmostVertex(polyP);
            qIndex = RightmostVertex(polyQ);

            if (P_i.x < Q_i.x) {
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
                return Vector2.Dot(polyQ.V(i + 1) - polyQ.V(i), P_i - polyQ.V(i)) >= 0;
            }

            int n = polyQ.Contour.Count;

            int a = BottomVertex(polyQ);
            int b = TopVertex(polyQ);

            if (b < a) {
                b += n;
            }

            qIndex = BinarySearch(aboveOrOnVoronoiEdge, a, b);
        }

        private RegionType GetRegionType(int consideredPIndex, int consideredQIndex) {
            Vector2 s = polyP.V(consideredPIndex);
            Vector2 q_j = polyQ.V(consideredQIndex);
            Vector2 q_j_next = polyQ.V(consideredQIndex + 1);
            Vector2 q_j_next_next = polyQ.V(consideredQIndex + 1);

            if (s == q_j) {
                return RegionType.Vertex;
            }

            float cross_j = Cross(q_j_next - q_j, s - q_j);
            float dot_j = Vector2.Dot(q_j_next - q_j, s - q_j);
            float dot_j_next = Vector2.Dot(q_j_next_next - q_j_next, s - q_j_next);

            if (dot_j == 0 && cross_j <= 0) {
                return RegionType.VoronoiEdge;
            } else if (dot_j > 0 && cross_j <= 0 && dot_j_next < 0) {
                return RegionType.VoronoiRegion;
            } else {
                return RegionType.None;
            }
        }

        private static float Cross(Vector2 a, Vector2 b) {
            return (a.x * b.y) - (a.y * b.x);
        }
    }

    static class ExtensionsForYangSubtractor
    {
        public static Vector2 V(this DTPolygon poly, int i) {
            return poly.Contour.GetCircular(i);
        }
    }
}

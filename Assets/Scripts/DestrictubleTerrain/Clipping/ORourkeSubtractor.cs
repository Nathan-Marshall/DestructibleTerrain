﻿using ClipperLib;
using DestructibleTerrain.Triangulation;
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

        List<Vector2> polyP;
        List<Vector2> polyQ;
        int pIndex;
        int qIndex;

        Vector2 PPrev {
            get { return polyP.GetCircular(pIndex - 1); }
        }
        Vector2 P {
            get {
                return polyP.GetCircular(pIndex);
            }
        }
        Vector2 PEdge {
            get {
                return P - PPrev;
            }
        }

        Vector2 QPrev {
            get { return polyQ.GetCircular(qIndex - 1); }
        }
        Vector2 Q {
            get { return polyQ.GetCircular(qIndex); }
        }
        Vector2 QEdge {
            get {
                return Q - QPrev;
            }
        }

        // Working output polygon first vertex
        Vector2? polygonBegin;

        private ORourkeSubtractor() {}

        public List<DTPolygon> Subtract(DTPolygon subject, DTPolygon clippingPolygon) {
            SubtractInternal(subject.Contour, clippingPolygon.Contour, out List<DTPolygon> outputPolygons);
            return outputPolygons;
        }

        // Returns true if the subject polygon was modified at all
        private bool SubtractInternal(List<Vector2> subject, List<Vector2> clippingPolygon, out List<DTPolygon> outputPolygons) {
            if (!DTUtility.BoundsCheck(subject, clippingPolygon)) {
                // There is no overlap at all, so output a copy of the subject polygon
                outputPolygons = new List<DTPolygon>() {
                    new DTPolygon(new List<Vector2>(subject))
                };
                return false;
            }

            polyP = subject;
            polyQ = clippingPolygon;

            pIndex = 0;
            qIndex = 0;

            // The first entry intersection point of the first output polgon. If we return to this point, break the loop
            bool firstIntersectionFound = false;
            int? firstIntersectionPIndex = null;
            int? firstIntersectionQIndex = null;

            // List of disjoint output polygons after subtraction
            outputPolygons = new List<DTPolygon>();

            // Working output polygon vertices
            polygonBegin = null;
            List<Vector2> pVertices = new List<Vector2>();
            List<Vector2> qVertices = new List<Vector2>();

            for (int i = 0; i <= 2*(polyP.Count + polyQ.Count); ++i) {
                if (polygonBegin.HasValue) {
                    Vector2? exitIntersection = ExitIntersection();
                    if (exitIntersection.HasValue) {
                        // Exit intersection point for output polygon: construct output polygon
                        DTPolygon poly = new DTPolygon();
                        poly.Contour.Add(polygonBegin.Value);
                        poly.Contour.AddRange(pVertices);
                        poly.Contour.Add(exitIntersection.Value);
                        poly.Contour.AddRange(qVertices.AsEnumerable().Reverse());

                        // Simplify polygon
                        poly = poly.Simplify();
                        if (poly != null) {
                            outputPolygons.Add(poly);
                        }

                        // Clear working polygon vertices
                        polygonBegin = null;
                        pVertices.Clear();
                        qVertices.Clear();
                    }
                }
                else {
                    Vector2? entranceIntersection = EntranceIntersection();
                    if (entranceIntersection.HasValue) {
                        // Loop exit condition: revisiting first intersection
                        if (firstIntersectionFound
                                && ModP(pIndex) == ModP(firstIntersectionPIndex.Value)
                                && ModQ(qIndex) == ModQ(firstIntersectionQIndex.Value)) {
                            break;
                        }

                        // Entry intersection point for output polygon
                        polygonBegin = entranceIntersection.Value;
                        pVertices.Clear();
                        qVertices.Clear();

                        // Keep track of this point if it is the entry intersection for the 1st output polygon
                        if (!firstIntersectionFound) {
                            firstIntersectionFound = true;
                            firstIntersectionPIndex = pIndex;
                            firstIntersectionQIndex = qIndex;
                        }
                    }
                }

                void advanceP() {
                    if (polygonBegin.HasValue) {
                        pVertices.Add(P);
                    }
                    ++pIndex;
                }

                void advanceQ() {
                    if (polygonBegin.HasValue) {
                        qVertices.Add(Q);
                    }
                    ++qIndex;
                }
                
                float pSide = (P - QPrev).Cross(QEdge);
                float qSide = (Q - PPrev).Cross(PEdge);
                float cross = QEdge.Cross(PEdge);

                if (cross <= 0) {
                    // QEdge heading inward

                    if (qSide < 0) {
                        // Q inside P's half-plane, heading away from PEdge
                        advanceP();
                    } else {
                        // Q outside P's half-plane or on P's line, heading toward PEdge
                        advanceQ();
                    }
                }
                else {
                    // QEdge heading outward

                    if (pSide < 0) {
                        // P inside Q's half-plane, heading away from QEdge
                        advanceQ();
                    } else {
                        // P outside Q's half-plane or on Q's line, heading toward QEdge
                        advanceP();
                    }
                }
            }

            // There were no intersections, so either one poly is entirely contained within the other, or there is no overlap at all
            if (outputPolygons.Count == 0) {
                if (polyP.Inside(polyQ)) {
                    // P is entirely within Q, so do nothing. The entire polygon has been subtracted
                    return true;
                } else if (polyQ.Inside(polyP)) {
                    // Q is entirely within P, so output a copy of P, with Q (reversed) set as a hole
                    outputPolygons.Add(new DTPolygon(
                        new List<Vector2>(polyP),
                        new List<List<Vector2>>() {
                            polyQ.AsEnumerable().Reverse().ToList()
                        }));
                    return true;
                } else {
                    if (polyP.Simplify() == polyQ.Simplify()) {
                        // The polygons are equal, so do nothing. The entire polygon has been subtracted
                        return true;
                    } else {
                        // There is no overlap at all, so output a copy of P
                        outputPolygons.Add(new DTPolygon(new List<Vector2>(polyP)));
                        return false;
                    }
                }
            }

            return true;
        }
        
        public List<List<DTPolygon>> SubtractPolygroup(IEnumerable<DTPolygon> inputPolygroup, IEnumerable<DTPolygon> clippingPolygons) {
            // Clip all input polygons by all clipping polygons
            List<DTPolygon> workingPolygons = new List<DTPolygon>(inputPolygroup);
            foreach (DTPolygon clippingPoly in clippingPolygons) {
                List<DTPolygon> newWorkingPolygons = new List<DTPolygon>();
                foreach (DTPolygon workingPoly in workingPolygons) {
                    // The working polygons should have partitioning applied since they may not be convex, but
                    // we won't worry about that for now, since our explosions only have one polygon
                    newWorkingPolygons.AddRange(Subtract(workingPoly, clippingPoly));
                }
                workingPolygons = newWorkingPolygons;
            }

            return workingPolygons.CreatePolygroups();
        }

        public List<PolygroupModifier> AdvancedSubtractPolygroup(PolygroupModifier subjectPolygroup, List<Vector2> clippingPolygon) {
            // The polygons to keep from the subject polygroup, by index
            List<int> keptIndices = new List<int>();

            // The new triangles added from the triangulated clipped polygons
            List<List<Vector2>> newTriangles = new List<List<Vector2>>();

            foreach (int i in subjectPolygroup.keptIndices) {
                List<Vector2> originalPolygon = subjectPolygroup.originalPolygroup[i];
                if (SubtractInternal(originalPolygon, clippingPolygon, out List<DTPolygon> subOutput)) {
                    DTProfilerMarkers.Triangulation.Begin();
                    List<List<Vector2>> triangles = DTUtility.TriangulateAll(subOutput, TriangleNetTriangulator.Instance);
                    DTProfilerMarkers.Triangulation.End();
                    newTriangles.AddRange(triangles);
                } else {
                    keptIndices.Add(i);
                }
            }

            return Polygrouper.AdvancedCreatePolygroups(subjectPolygroup.originalPolygroup, keptIndices, newTriangles);
        }

        public List<List<List<DTPolygon>>> SubtractBulk(IEnumerable<IEnumerable<DTPolygon>> inputPolygroups, IEnumerable<DTPolygon> clippingPolygons) {
            List<List<List<DTPolygon>>> outputPolygroupGroups = new List<List<List<DTPolygon>>>();
            foreach (IEnumerable<DTPolygon> inputPolygroup in inputPolygroups) {
                outputPolygroupGroups.Add(SubtractPolygroup(inputPolygroup, clippingPolygons));
            }
            return outputPolygroupGroups;
        }

        private Vector2? EntranceIntersection() {
            if (PEdge.Cross(QEdge) <= 0) {
                return null;
            }

            SegmentSegmentIntersectionResult? result = SegmentSegmentIntersection(PPrev, P, QPrev, Q);
            if (result.HasValue && result.Value.at < 1 && result.Value.bt < 1) {
                return result.Value.position;
            }

            return null;
        }

        private Vector2? ExitIntersection() {
            if (!polygonBegin.HasValue || PEdge.Cross(QEdge) >= 0) {
                return null;
            }

            SegmentSegmentIntersectionResult? result = SegmentSegmentIntersection(PPrev, P, QPrev, Q);
            if (result.HasValue) {
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
            return ((pi % polyP.Count) + polyP.Count) % polyP.Count;
        }

        int ModQ(int qi) {
            return ((qi % polyQ.Count) + polyQ.Count) % polyQ.Count;
        }
    }

    static class ExtensionsForORourkeSubtractor
    {
        public static bool Inside(this List<Vector2> polyA, List<Vector2> polyB) {
            foreach (Vector2 a in polyA) {
                bool? aInside = a.Inside(polyB);
                if (aInside.HasValue) {
                    return aInside.Value;
                }
            }
            return false;
        }

        public static bool? Inside(this Vector2 point, List<Vector2> poly) {
            for (int i = 0; i < poly.Count; i++) {
                float cross = (poly.GetCircular(i) - poly.GetCircular(i - 1)).Cross(point - poly.GetCircular(i - 1));
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

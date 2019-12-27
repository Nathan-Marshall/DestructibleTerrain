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
    public sealed class ClipperSubtractor : IPolygonSubtractor
    {
        private static readonly Lazy<ClipperSubtractor> lazyInstance = new Lazy<ClipperSubtractor>(() => new ClipperSubtractor());

        // Singleton intance
        public static ClipperSubtractor Instance {
            get { return lazyInstance.Value; }
        }

        private readonly Clipper clipper;

        private ClipperSubtractor() {
            clipper = new Clipper();
        }

        public List<DTPolygon> Subtract(DTPolygon subject, DTPolygon clippingPolygon) {
            if (!DTUtility.BoundsCheck(subject, clippingPolygon)) {
                // There is no overlap at all, so output a copy of the subject polygon
                return new List<DTPolygon>() {
                    new DTPolygon(new List<Vector2>(subject.Contour))
                };
            }

            clipper.Clear();

            // Add subject polygon paths
            clipper.AddPath(subject.Contour.ToIntPointList(), PolyType.ptSubject, true);
            foreach (var hole in subject.Holes) {
                clipper.AddPath(hole.ToIntPointList(), PolyType.ptSubject, true);
            }

            // Add clipping polygon paths
            clipper.AddPath(clippingPolygon.Contour.ToIntPointList(), PolyType.ptClip, true);
            foreach (var hole in clippingPolygon.Holes) {
                clipper.AddPath(hole.ToIntPointList(), PolyType.ptClip, true);
            }

            // Execute subtraction and store result in a PolyTree so that we can easily identify holes
            PolyTree clipperOutput = new PolyTree();
            clipper.Execute(ClipType.ctDifference, clipperOutput, PolyFillType.pftEvenOdd, PolyFillType.pftPositive);

            // Convert Polytree into list of DTPolygons
            List<DTPolygon> output = new List<DTPolygon>();
            foreach (var poly in clipperOutput.Childs) {
                List<Vector2> contour = poly.Contour.ToVector2List();
                List<List<Vector2>> holes = poly.Childs.Select(hole => hole.Contour.ToVector2List()).ToList();
                output.Add(new DTPolygon(contour, holes));
            }

            return output;
        }
        
        public List<List<DTPolygon>> SubtractPolyGroup(IEnumerable<DTPolygon> inputPolyGroup, IEnumerable<DTPolygon> clippingPolygons) {
            // No bounds check. We could do a bounds check to return now if the polygroups are entirely
            // disjoint, but we do that in the explosion executor anyway

            clipper.Clear();

            // Add subject polygon paths
            foreach (DTPolygon poly in inputPolyGroup) {
                // Convert the points to IntPoint and add that path to Clipper
                List<IntPoint> contourPath = poly.Contour.ToIntPointList();
                clipper.AddPath(contourPath, PolyType.ptSubject, true);

                foreach (var hole in poly.Holes) {
                    // Convert the points to IntPoint and add that path to Clipper
                    List<IntPoint> holePath = hole.ToIntPointList();
                    clipper.AddPath(holePath, PolyType.ptSubject, true);
                }
            }

            // Add clipping polygon paths
            foreach (DTPolygon poly in clippingPolygons) {
                clipper.AddPath(poly.Contour.ToIntPointList(), PolyType.ptClip, true);

                foreach (var hole in poly.Holes) {
                    clipper.AddPath(hole.ToIntPointList(), PolyType.ptClip, true);
                }
            }

            // Execute subtraction and store result in a PolyTree so that we can easily identify holes
            PolyTree clipperOutput = new PolyTree();
            clipper.Execute(ClipType.ctDifference, clipperOutput, PolyFillType.pftEvenOdd, PolyFillType.pftNegative);

            // Construct a list of point sets to identify unique groups of connected output polygons
            List<HashSet<IntPoint>> outputPointGroups = new List<HashSet<IntPoint>>();
            foreach (var poly in clipperOutput.Childs) {
                poly.MergeIntoPointGroups(outputPointGroups);
            }

            // Convert Polytree into list of DTPolygons
            List<List<DTPolygon>> output = new List<List<DTPolygon>>(outputPointGroups.Count);
            for (int i = 0; i < outputPointGroups.Count; ++i) {
                output.Add(new List<DTPolygon>());
            }
            foreach (var poly in clipperOutput.Childs) {
                // Convert the polygon to a DTPolygon and add it to the output
                List<Vector2> contour = poly.Contour.ToVector2List();
                List<List<Vector2>> holes = poly.Childs.Select(hole => hole.Contour.ToVector2List()).ToList();
                var dtPoly = new DTPolygon(contour, holes);

                // Find the correct place to put this polygon in the output structure
                int outputGroupIndex = poly.GetFirstPointGroupIndex(outputPointGroups);
                if (outputGroupIndex >= 0) {
                    // Matched output group
                    output[outputGroupIndex].Add(dtPoly);
                } else {
                    // New output group
                    output.Add(new List<DTPolygon>() { dtPoly });
                }
            }

            return output;
        }

        // WARNING: This implementation is currently very volatile and can cause multiple input polygon groups to fuse
        // together if a vertex from one is very close to the vertex of another.
        public List<List<List<DTPolygon>>> SubtractBulk(IEnumerable<IEnumerable<DTPolygon>> inputPolyGroups, IEnumerable<DTPolygon> clippingPolygons) {
            // No bounds check. We could do a bounds check to return early if polygroups were entirely
            // disjoint, but we do that in the explosion executor anyway

            clipper.Clear();

            // Map the points of each polygon to the index of the polygon group to which the polygon belongs
            Dictionary<IntPoint, int> inputPointToPolyGroup = new Dictionary<IntPoint, int>();

            // Add subject polygon paths
            {
                int inputGroupIndex = 0;
                foreach (IEnumerable<DTPolygon> inputPolygons in inputPolyGroups) {
                    foreach (DTPolygon poly in inputPolygons) {
                        // Convert the points to IntPoint and add that path to Clipper
                        List<IntPoint> contourPath = poly.Contour.ToIntPointList();
                        clipper.AddPath(contourPath, PolyType.ptSubject, true);

                        // Map the points to the subject group index
                        foreach (IntPoint point in contourPath) {
                            inputPointToPolyGroup[point] = inputGroupIndex;
                        }

                        foreach (var hole in poly.Holes) {
                            // Convert the points to IntPoint and add that path to Clipper
                            List<IntPoint> holePath = hole.ToIntPointList();
                            clipper.AddPath(holePath, PolyType.ptSubject, true);

                            // Don't bother putting hole points into inputPointGroups
                        }
                    }

                    ++inputGroupIndex;
                }
            }

            // Add clipping polygon paths
            foreach (DTPolygon poly in clippingPolygons) {
                clipper.AddPath(poly.Contour.ToIntPointList(), PolyType.ptClip, true);

                foreach (var hole in poly.Holes) {
                    clipper.AddPath(hole.ToIntPointList(), PolyType.ptClip, true);
                }
            }

            // Execute subtraction and store result in a PolyTree so that we can easily identify holes
            PolyTree clipperOutput = new PolyTree();
            clipper.Execute(ClipType.ctDifference, clipperOutput, PolyFillType.pftEvenOdd, PolyFillType.pftNegative);

            // Construct a list of point sets to identify unique groups of connected output polygons
            List<HashSet<IntPoint>> outputPointGroups = new List<HashSet<IntPoint>>();
            foreach (var poly in clipperOutput.Childs) {
                poly.MergeIntoPointGroups(outputPointGroups);
            }

            // Map all output groups to an input group index.
            int numInputGroups = inputPolyGroups.Count();
            List<List<HashSet<IntPoint>>> inputOutputGroupMappings = new List<List<HashSet<IntPoint>>>(numInputGroups);
            foreach (var s in inputPolyGroups) {
                inputOutputGroupMappings.Add(new List<HashSet<IntPoint>>());
            }
            // Output groups that could not be mapped are in their own list.
            List<HashSet<IntPoint>> unmappedOutputGroups = new List<HashSet<IntPoint>>();
            foreach (var points in outputPointGroups) {
                int inputGroupIndex = points.GetFirstPointGroupIndex(inputPointToPolyGroup);
                if (inputGroupIndex >= 0) {
                    inputOutputGroupMappings[inputGroupIndex].Add(points);
                } else {
                    unmappedOutputGroups.Add(points);
                }
            }

            // Convert Polytree into list of DTPolygons
            List<List<List<DTPolygon>>> output = new List<List<List<DTPolygon>>>(numInputGroups + unmappedOutputGroups.Count);
            for (int i = 0; i < numInputGroups + unmappedOutputGroups.Count; ++i) {
                output.Add(new List<List<DTPolygon>>());
                if (i < numInputGroups) {
                    for (int j = 0; j < inputOutputGroupMappings[i].Count; ++j) {
                        output[i].Add(new List<DTPolygon>());
                    }
                }
            }
            foreach (var poly in clipperOutput.Childs) {
                // Convert the polygon to a DTPolygon and add it to the output
                List<Vector2> contour = poly.Contour.ToVector2List();
                List<List<Vector2>> holes = poly.Childs.Select(hole => hole.Contour.ToVector2List()).ToList();
                var dtPoly = new DTPolygon(contour, holes);

                // Find the correct place to put this polygon in the output structure
                int inputGroupIndex = poly.GetFirstPointGroupIndex(inputPointToPolyGroup);
                if (inputGroupIndex >= 0) {
                    int outputGroupIndex = poly.GetFirstPointGroupIndex(inputOutputGroupMappings[inputGroupIndex]);
                    if (outputGroupIndex >= 0) {
                        // Matched input group and output group
                        output[inputGroupIndex][outputGroupIndex].Add(dtPoly);
                    } else {
                        // Matched input group, new output group
                        output[inputGroupIndex].Add(new List<DTPolygon>() { dtPoly });
                    }
                } else {
                    int outputGroupIndex = poly.GetFirstPointGroupIndex(unmappedOutputGroups);
                    if (outputGroupIndex >= 0) {
                        // No input group, matched output group
                        output[inputOutputGroupMappings.Count + outputGroupIndex].Add(new List<DTPolygon>() { dtPoly });
                    } else {
                        // No input group, new output group
                        output.Add(new List<List<DTPolygon>>() { new List<DTPolygon>() { dtPoly } });
                    }
                }
            }

            return output;
        }
    }

    static class ExtensionsForClipperSubtractor
    {
        public static IntPoint ToIntPoint(this Vector2 p) {
            return new IntPoint(p.x * DTUtility.FixedPointConversion, p.y * DTUtility.FixedPointConversion);
        }

        public static Vector2 ToVector2(this IntPoint p) {
            return new Vector2(p.X / (float)DTUtility.FixedPointConversion, p.Y / (float)DTUtility.FixedPointConversion);
        }

        public static List<IntPoint> ToIntPointList(this IEnumerable<Vector2> vectors) {
            return vectors.Select(ToIntPoint).ToList();
        }

        public static List<Vector2> ToVector2List(this IEnumerable<IntPoint> points) {
            return points.Select(ToVector2).ToList();
        }


        // Returns true given group shares a point with this polygon.
        public static bool BelongsToPointGroup(this PolyNode poly, ISet<IntPoint> points) {
            foreach (var point in poly.Contour) {
                if (points.Contains(point)) {
                    return true;
                }
            }

            // Don't bother checking hole points
            return false;
        }

        // Returns the index of the first group that shares a point with this polygon.
        public static int GetFirstPointGroupIndex(this PolyNode poly, IEnumerable<ISet<IntPoint>> pointGroups) {
            // Don't bother checking hole points
            return poly.Contour.GetFirstPointGroupIndex(pointGroups);
        }

        // Returns the index of the first group that shares a point with this pointGroup.
        public static int GetFirstPointGroupIndex(this IEnumerable<IntPoint> pointGroup, IEnumerable<ISet<IntPoint>> pointGroups) {
            foreach (var point in pointGroup) {
                int groupIndex = 0;
                foreach (var points in pointGroups) {
                    if (points.Contains(point)) {
                        return groupIndex;
                    }
                    ++groupIndex;
                }
            }
            return -1;
        }

        // Returns the index of the first group that shares a point with this polygon.
        public static int GetFirstPointGroupIndex(this PolyNode poly, IDictionary<IntPoint, int> pointToGroup) {
            // Don't bother checking hole points
            return poly.Contour.GetFirstPointGroupIndex(pointToGroup);
        }

        // Returns the index of the first group that shares a point with this pointGroup.
        public static int GetFirstPointGroupIndex(this IEnumerable<IntPoint> pointGroup, IDictionary<IntPoint, int> pointToGroup) {
            foreach (var point in pointGroup) {
                if (pointToGroup.ContainsKey(point)) {
                    return pointToGroup[point];
                }
            }
            return -1;
        }

        // Adds the polygon's points to the first point group that shares a point with the polygon.
        // If any other groups share a point with the polygon, those groups are merged into the first group as well,
        // and are removed from their previous positions in the list.
        // Returns the index of the merged (or new) group.
        public static int MergeIntoPointGroups(this PolyNode poly, List<HashSet<IntPoint>> pointGroups) {
            // Find connected groups
            List<int> connectedGroupIndices = new List<int>();
            for (int i = 0; i < pointGroups.Count; ++i) {
                if (poly.BelongsToPointGroup(pointGroups[i])) {
                    connectedGroupIndices.Add(i);
                }
            }

            if (connectedGroupIndices.Count == 0) {
                // If this polygon is not connected to any existing point group, make a new one
                HashSet<IntPoint> points = new HashSet<IntPoint>();
                foreach (IntPoint point in poly.Contour) {
                    points.Add(point);
                }
                // Don't bother checking hole points

                pointGroups.Add(points);
                return pointGroups.Count - 1;
            } else {
                // Add the polygon's points to the first connected group
                var firstGroup = pointGroups[connectedGroupIndices[0]];
                foreach (IntPoint point in poly.Contour) {
                    firstGroup.Add(point);
                }
                // Don't bother checking hole points

                // If this polygon is connected to any other groups, merge them in too
                int numRemovals = 0;
                for (int i = 1; i < connectedGroupIndices.Count; ++i) {
                    int connectedGroupIndex = connectedGroupIndices[i] - numRemovals;

                    firstGroup.UnionWith(pointGroups[connectedGroupIndex]);
                    pointGroups.RemoveAt(connectedGroupIndex);
                    ++numRemovals;
                }

                return connectedGroupIndices[0];
            }
        }
    }
}

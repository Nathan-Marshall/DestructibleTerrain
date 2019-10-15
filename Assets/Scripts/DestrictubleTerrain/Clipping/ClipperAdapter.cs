using ClipperLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;

namespace DestrictubleTerrain.Clipping
{
    public sealed class ClipperAdapter : IPolygonSubtractor {
        private static readonly Lazy<ClipperAdapter> lazyInstance = new Lazy<ClipperAdapter>(() => new ClipperAdapter());

        // Singleton intance
        public static ClipperAdapter Instance {
            get { return lazyInstance.Value; }
        }

        private readonly Clipper clipper;

        private ClipperAdapter() {
            clipper = new Clipper();
        }

        public IList<DTPolygon> Subtract(DTPolygon subject, DTPolygon clippingPolygon) {
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
            clipper.Execute(ClipType.ctDifference, clipperOutput, PolyFillType.pftEvenOdd, PolyFillType.pftNegative);

            // Convert Polytree into list of DTPolygons
            List<DTPolygon> output = new List<DTPolygon>();
            foreach (var poly in clipperOutput.Childs) {
                List<Vector2> contour = poly.Contour.ToVector2List();
                List<List<Vector2>> holes = poly.Childs.Select(hole => hole.Contour.ToVector2List()).ToList();
                output.Add(new DTPolygon(contour, holes));
            }

            return output;
        }

        public IList<List<List<DTPolygon>>> Subtract(IEnumerable<IEnumerable<DTPolygon>> subjectGroups, IEnumerable<DTPolygon> clippingPolygons) {
            clipper.Clear();

            // Keep a list of point sets that correspond to each input group, to identify output polygons with respect to input polygon groups
            List<HashSet<IntPoint>> inputPointGroups = new List<HashSet<IntPoint>>();

            // Add subject polygon paths
            foreach (IEnumerable<DTPolygon> subjects in subjectGroups) {
                HashSet<IntPoint> points = new HashSet<IntPoint>();

                foreach (DTPolygon poly in subjects) {
                    // Convert the points to IntPoint and add that path to Clipper
                    List<IntPoint> contourPath = poly.Contour.ToIntPointList();
                    clipper.AddPath(contourPath, PolyType.ptSubject, true);

                    // Add unique points to a hash set that we will use later to identify output groups
                    foreach (IntPoint point in contourPath) {
                        points.Add(point);
                    }

                    foreach (var hole in poly.Holes) {
                        // Convert the points to IntPoint and add that path to Clipper
                        List<IntPoint> holePath = hole.ToIntPointList();
                        clipper.AddPath(holePath, PolyType.ptSubject, true);

                        // Don't bother putting hole points into inputPointGroups
                    }
                }

                // Add this group of points to the list of groups
                inputPointGroups.Add(points);
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
            List<List<HashSet<IntPoint>>> inputOutputGroupMappings = new List<List<HashSet<IntPoint>>>(inputPointGroups.Count);
            for (int i = 0; i < inputPointGroups.Count; ++i) {
                inputOutputGroupMappings.Add(new List<HashSet<IntPoint>>());
            }
            // Output groups that could not be mapped are in their own list.
            List<HashSet<IntPoint>> unmappedOutputGroups = new List<HashSet<IntPoint>>();
            foreach (var points in outputPointGroups) {
                int index = points.GetFirstPointGroupIndex(inputPointGroups);
                if (index >= 0) {
                    inputOutputGroupMappings[index].Add(points);
                } else {
                    unmappedOutputGroups.Add(points);
                }
            }

            // Convert Polytree into list of DTPolygons
            List<List<List<DTPolygon>>> output = new List<List<List<DTPolygon>>>(inputPointGroups.Count + unmappedOutputGroups.Count);
            for (int i = 0; i < inputPointGroups.Count + unmappedOutputGroups.Count; ++i) {
                output.Add(new List<List<DTPolygon>>());
                if (i < inputPointGroups.Count) {
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
                int inputGroupIndex = poly.GetFirstPointGroupIndex(inputPointGroups);
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

    static class ExtensionsForClipperAdapter
    {
        private const long FixedDecimalConversion = 10000000;

        public static IntPoint ToIntPoint(Vector2 p) {
            return new IntPoint(p.x * FixedDecimalConversion, p.y * FixedDecimalConversion);
        }

        public static Vector2 ToVector2(IntPoint p) {
            return new Vector2(p.X / (float)FixedDecimalConversion, p.Y / (float)FixedDecimalConversion);
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

        // Adds the polygon's points to the first point group that shares a point with the polygon.
        // If any other groups share a point with the polygon, those groups are merged into the first group as well,
        // and are removed from their previous positions in the list.
        // Returns the index of the merged (or new) group.
        public static int MergeIntoPointGroups(this PolyNode poly, IList<HashSet<IntPoint>> pointGroups) {
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

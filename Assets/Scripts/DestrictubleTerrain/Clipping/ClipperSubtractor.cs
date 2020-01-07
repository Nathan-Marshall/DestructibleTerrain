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
            clipper.Execute(ClipType.ctDifference, clipperOutput, PolyFillType.pftEvenOdd, PolyFillType.pftNonZero);

            // Convert Polytree into list of DTPolygons
            return clipperOutput.ToDTPolygons();
        }
        
        public List<List<DTPolygon>> SubtractPolygroup(IEnumerable<DTPolygon> inputPolygroup, IEnumerable<DTPolygon> clippingPolygons) {
            // No bounds check. We could do a bounds check to return now if the polygroups are entirely
            // disjoint, but we do that in the explosion executor anyway

            clipper.Clear();

            // Add subject polygon paths
            foreach (DTPolygon poly in inputPolygroup) {
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
            clipper.Execute(ClipType.ctDifference, clipperOutput, PolyFillType.pftEvenOdd, PolyFillType.pftNonZero);

            // Convert Polytree into list of DTPolygons
            List<DTPolygon> clipperOutputDT = clipperOutput.ToDTPolygons();

            // Group the polygons into polygroups based on shared points
            return clipperOutputDT.CreatePolygroups();
        }

        // WARNING: This implementation is currently very volatile and can cause multiple input polygon groups to fuse
        // together if a vertex from one is very close to the vertex of another.
        public List<List<List<DTPolygon>>> SubtractBulk(IEnumerable<IEnumerable<DTPolygon>> inputPolygroups, IEnumerable<DTPolygon> clippingPolygons) {
            // No bounds check. We could do a bounds check to return early if polygroups were entirely
            // disjoint, but we do that in the explosion executor anyway

            clipper.Clear();

            // Map the points of each polygon to the index of the polygon group to which the polygon belongs
            Dictionary<Vector2, int> inputPointToPolygroup = new Dictionary<Vector2, int>(new DTUtility.ApproximateVector2Comparer());

            // Add subject polygon paths
            {
                int inputGroupIndex = 0;
                foreach (IEnumerable<DTPolygon> inputPolygons in inputPolygroups) {
                    foreach (DTPolygon poly in inputPolygons) {
                        // Convert the points to IntPoint and add that path to Clipper
                        List<IntPoint> contourPath = poly.Contour.ToIntPointList();
                        clipper.AddPath(contourPath, PolyType.ptSubject, true);

                        // Map the points to the subject group index
                        foreach (Vector2 point in poly.Contour) {
                            inputPointToPolygroup[point] = inputGroupIndex;
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
            clipper.Execute(ClipType.ctDifference, clipperOutput, PolyFillType.pftEvenOdd, PolyFillType.pftNonZero);

            List<DTPolygon> clipperOutputDT = clipperOutput.ToDTPolygons();

            // Construct a list of point sets to identify unique groups of connected output polygons
            List<HashSet<Vector2>> outputPointGroups = new List<HashSet<Vector2>>();
            foreach (var poly in clipperOutputDT) {
                poly.MergeIntoPointGroups(outputPointGroups);
            }

            // Map all output groups to an input group index.
            int numInputGroups = inputPolygroups.Count();
            List<List<HashSet<Vector2>>> inputOutputGroupMappings = new List<List<HashSet<Vector2>>>(numInputGroups);
            foreach (var s in inputPolygroups) {
                inputOutputGroupMappings.Add(new List<HashSet<Vector2>>());
            }
            // Output groups that could not be mapped are in their own list.
            List<HashSet<Vector2>> unmappedOutputGroups = new List<HashSet<Vector2>>();
            foreach (var points in outputPointGroups) {
                int inputGroupIndex = points.GetFirstPointGroupIndex(inputPointToPolygroup);
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
            foreach (var poly in clipperOutputDT) {
                // Find the correct place to put this polygon in the output structure
                int inputGroupIndex = poly.GetFirstPointGroupIndex(inputPointToPolygroup);
                if (inputGroupIndex >= 0) {
                    int outputGroupIndex = poly.GetFirstPointGroupIndex(inputOutputGroupMappings[inputGroupIndex]);
                    if (outputGroupIndex >= 0) {
                        // Matched input group and output group
                        output[inputGroupIndex][outputGroupIndex].Add(poly);
                    } else {
                        // Matched input group, new output group
                        output[inputGroupIndex].Add(new List<DTPolygon>() { poly });
                    }
                } else {
                    int outputGroupIndex = poly.GetFirstPointGroupIndex(unmappedOutputGroups);
                    if (outputGroupIndex >= 0) {
                        // No input group, matched output group
                        output[inputOutputGroupMappings.Count + outputGroupIndex].Add(new List<DTPolygon>() { poly });
                    } else {
                        // No input group, new output group
                        output.Add(new List<List<DTPolygon>>() { new List<DTPolygon>() { poly } });
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

        public static List<DTPolygon> ToDTPolygons(this PolyTree tree) {
            List<DTPolygon> output = new List<DTPolygon>();
            foreach (var poly in tree.Childs) {
                List<Vector2> contour = poly.Contour.ToVector2List();
                List<List<Vector2>> holes = poly.Childs.Select(hole => hole.Contour.ToVector2List()).ToList();
                output.Add(new DTPolygon(contour, holes));
            }
            return output;
        }
    }
}

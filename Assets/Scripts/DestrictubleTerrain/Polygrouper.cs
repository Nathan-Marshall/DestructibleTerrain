using ClipperLib;
using DestructibleTerrain;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TriangleNet.Geometry;
using UnityEngine;

namespace DestructibleTerrain
{

    public class PolygroupModifier
    {
        public DTConvexPolygroup originalPolygroup;
        public List<int> keptIndices;
        public List<List<Vector2>> newPolygons;

        public DTConvexPolygroup KeptPolygons {
            get { return new DTConvexPolygroup(keptIndices.Select(i => originalPolygroup[i])); }
        }

        public PolygroupModifier(DTConvexPolygroup originalPolygroup) {
            this.originalPolygroup = originalPolygroup;
            keptIndices = new List<int>();
            newPolygons = new List<List<Vector2>>();
        }

        public PolygroupModifier(DTConvexPolygroup originalPolygroup, List<int> keptIndices, List<List<Vector2>> newPolygons) {
            this.originalPolygroup = originalPolygroup;
            this.keptIndices = keptIndices;
            this.newPolygons = newPolygons;
        }
    }

    public static class Polygrouper
    {
        public static List<List<DTPolygon>> CreatePolygroups(this List<DTPolygon> polygons) {
            if (polygons.Count == 1) {
                return new List<List<DTPolygon>>() { polygons };
            }

            DTProfilerMarkers.Polygrouper.Begin();

            // Construct a list of point sets to identify unique groups of connected output polygons
            List<HashSet<Vector2>> outputPointGroups = new List<HashSet<Vector2>>();
            foreach (var poly in polygons) {
                poly.MergeIntoPointGroups(outputPointGroups);
            }

            // Use the point sets to create polygroups
            List<List<DTPolygon>> polygroups = new List<List<DTPolygon>>(outputPointGroups.Count);
            for (int i = 0; i < outputPointGroups.Count; ++i) {
                polygroups.Add(new List<DTPolygon>());
            }
            foreach (var poly in polygons) {
                // Find the correct place to put this polygon in the output structure
                int outputGroupIndex = poly.GetFirstPointGroupIndex(outputPointGroups);
                if (outputGroupIndex >= 0) {
                    // Matched output group
                    polygroups[outputGroupIndex].Add(poly);
                }
                else {
                    // New output group
                    polygroups.Add(new List<DTPolygon>() { poly });
                }
            }

            DTProfilerMarkers.Polygrouper.End();

            return polygroups;
        }

        public static List<PolygroupModifier> AdvancedCreatePolygroups(DTConvexPolygroup originalPolygroup, List<int> keptIndices, List<List<Vector2>> newPolygons) {
            DTProfilerMarkers.Polygrouper.Begin();

            // Construct a list of point sets to identify unique groups of connected output polygons
            List<HashSet<Vector2>> outputPointGroups = new List<HashSet<Vector2>>();
            foreach (int i in keptIndices) {
                originalPolygroup[i].MergeIntoPointGroups(outputPointGroups);
            }
            foreach (var poly in newPolygons) {
                poly.MergeIntoPointGroups(outputPointGroups);
            }

            // Use the point sets to create polygroups
            List<PolygroupModifier> outputPolygroups = new List<PolygroupModifier>(outputPointGroups.Count);
            // Initialize the polygroups
            for (int i = 0; i < outputPointGroups.Count; ++i) {
                outputPolygroups.Add(new PolygroupModifier(originalPolygroup));
            }
            // Add each existing polygon to the first polygroup it matches
            foreach (int i in keptIndices) {
                // Find the correct place to put this polygon in the output structure
                int outputGroupIndex = originalPolygroup[i].GetFirstPointGroupIndex(outputPointGroups);
                if (outputGroupIndex >= 0) {
                    outputPolygroups[outputGroupIndex].keptIndices.Add(i);
                }
                else {
                    throw new Exception("Polygon failed to match a point group");
                }
            }
            // Add each new polygon to the first polygroup it matches
            foreach (var poly in newPolygons) {
                // Find the correct place to put this polygon in the output structure
                int outputGroupIndex = poly.GetFirstPointGroupIndex(outputPointGroups);
                if (outputGroupIndex >= 0) {
                    outputPolygroups[outputGroupIndex].newPolygons.Add(poly);
                }
                else {
                    throw new Exception("Polygon failed to match a point group");
                }
            }

            DTProfilerMarkers.Polygrouper.End();

            return outputPolygroups;
        }

        // Returns the index of the first group that shares a point with this pointGroup.
        public static int GetFirstPointGroupIndex(this IEnumerable<Vector2> pointGroup, IEnumerable<ISet<Vector2>> pointGroups) {
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
        public static int GetFirstPointGroupIndex(this DTPolygon poly, IEnumerable<ISet<Vector2>> pointGroups) {
            // Don't bother checking hole points
            return poly.Contour.GetFirstPointGroupIndex(pointGroups);
        }

        // Returns the index of the first group that shares a point with this pointGroup.
        public static int GetFirstPointGroupIndex(this IEnumerable<Vector2> pointGroup, IDictionary<Vector2, int> pointToGroup) {
            foreach (var point in pointGroup) {
                if (pointToGroup.ContainsKey(point)) {
                    return pointToGroup[point];
                }
            }
            return -1;
        }

        // Returns the index of the first group that shares a point with this polygon.
        public static int GetFirstPointGroupIndex(this DTPolygon poly, IDictionary<Vector2, int> pointToGroup) {
            // Don't bother checking hole points
            return poly.Contour.GetFirstPointGroupIndex(pointToGroup);
        }

        // Returns true given group shares a point with this polygon.
        public static bool BelongsToPointGroup(this List<Vector2> contour, HashSet<Vector2> points) {
            foreach (var point in contour) {
                if (points.Contains(point)) {
                    return true;
                }
            }
            return false;
        }

        // Returns true given group shares a point with this polygon.
        public static bool BelongsToPointGroup(this DTPolygon poly, HashSet<Vector2> points) {
            // Don't bother checking holes
            return poly.Contour.BelongsToPointGroup(points);
        }

        // Adds the polygon's points to the first point group that shares a point with the polygon.
        // If any other groups share a point with the polygon, those groups are merged into the first group as well,
        // and are removed from their previous positions in the list.
        // Returns the index of the merged (or new) group.
        public static int MergeIntoPointGroups(this List<Vector2> contour, List<HashSet<Vector2>> pointGroups) {
            // Find connected groups
            List<int> connectedGroupIndices = new List<int>();
            for (int i = 0; i < pointGroups.Count; ++i) {
                if (contour.BelongsToPointGroup(pointGroups[i])) {
                    connectedGroupIndices.Add(i);
                }
            }

            if (connectedGroupIndices.Count == 0) {
                // If this polygon is not connected to any existing point group, make a new one
                HashSet<Vector2> points = new HashSet<Vector2>(new DTUtility.ApproximateVector2Comparer());
                foreach (Vector2 point in contour) {
                    points.Add(point);
                }

                pointGroups.Add(points);
                return pointGroups.Count - 1;
            }
            else {
                // Add the polygon's points to the first connected group
                var firstGroup = pointGroups[connectedGroupIndices[0]];
                foreach (Vector2 point in contour) {
                    firstGroup.Add(point);
                }

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

        // Adds the polygon's points to the first point group that shares a point with the polygon.
        // If any other groups share a point with the polygon, those groups are merged into the first group as well,
        // and are removed from their previous positions in the list.
        // Returns the index of the merged (or new) group.
        public static int MergeIntoPointGroups(this DTPolygon poly, List<HashSet<Vector2>> pointGroups) {
            // Don't bother checking holes
            return poly.Contour.MergeIntoPointGroups(pointGroups);
        }
    }

}
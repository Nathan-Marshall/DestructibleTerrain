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

        public IEnumerable<DTPolygon> Subtract(IEnumerable<DTPolygon> subjects, IEnumerable<DTPolygon> clippingPolygons) {
            clipper.Clear();

            // Add subject polygon paths
            foreach (DTPolygon poly in subjects) {
                clipper.AddPath(poly.Contour.ToIntPointList(), PolyType.ptSubject, true);

                foreach (var hole in poly.Holes) {
                    clipper.AddPath(hole.ToIntPointList(), PolyType.ptSubject, true);
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
            clipper.Execute(ClipType.ctDifference, clipperOutput);

            // Convert Polytree into list of DTPolygons
            List<DTPolygon> output = new List<DTPolygon>();
            foreach (var poly in clipperOutput.Childs) {
                List<Vector2> contour = poly.Contour.ToVector2List();
                List<List<Vector2>> holes = poly.Childs.Select(hole => hole.Contour.ToVector2List()).ToList();
                output.Add(new DTPolygon(contour, holes));
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
    }
}

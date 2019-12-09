using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DestructibleTerrain
{
    public class DTPolygon : IEquatable<DTPolygon>
    {
        public List<Vector2> Contour { get; set; }
        public List<List<Vector2>> Holes { get; set; }

        public DTPolygon(List<Vector2> contour = null, List<List<Vector2>> holes = null) {
            Contour = contour ?? new List<Vector2>();
            Holes = holes ?? new List<List<Vector2>>();
        }

        public static bool operator ==(DTPolygon a, DTPolygon b) {
            if (ReferenceEquals(a, null) && ReferenceEquals(b, null)) {
                return true;
            }
            else if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) {
                return false;
            }

            // Check for same number of holes then check for equal contours
            if (a.Holes.Count != b.Holes.Count || !ContoursEqual(a.Contour, b.Contour)) {
                return false;
            }

            for (int i = 0; i < a.Holes.Count; ++i) {
                // Return false if these holes are not equal
                if (!ContoursEqual(a.Holes[i], b.Holes[i])) {
                    return false;
                }
            }

            // Return true if the contours and holes were all equal
            return true;
        }
        public static bool operator !=(DTPolygon a, DTPolygon b) {
            return !(a == b);
        }
        public override bool Equals(object obj) {
            if (ReferenceEquals(obj, null) || !GetType().Equals(obj.GetType())) {
                return false;
            } else {
                return this == (DTPolygon)obj;
            }
        }
        public bool Equals(DTPolygon obj) {
            return this == obj;
        }
        public override int GetHashCode() {
            int hash = 0;
            foreach (Vector2 p in Contour) {
                hash ^= p.FixedPointHashCode();
            }
            foreach (var hole in Holes) {
                foreach (Vector2 p in hole) {
                    hash ^= p.FixedPointHashCode();
                }
            }
            return hash;
        }

        private static bool ContoursEqual(List<Vector2> a, List<Vector2> b) {
            // Basic check for number of vertices
            if (a.Count != b.Count) {
                return false;
            }

            if (a.Count == 0) {
                return true;
            }
            
            for (int i = 0; i < a.Count; ++i) {
                // Find all appearances of b[0] in a
                if (a[i].Approximately(b[0])) {
                    // Check if all vertices are the same, starting at offset index i on polygon a
                    bool success = true;
                    for (int j = 1; j < b.Count; ++j) {
                        // If these vertices are not equal, then the polygons are not equal with this offset index i
                        if (!a.GetCircular(i + j).Approximately(b[j])) {
                            success = false;
                        }
                    }
                    if (success) {
                        // All vertices are the same if we offset polygon a by index i, so the polygon contours are the same
                        return true;
                    }
                }
            }

            // The vertices were not all the same for any offset index i
            return false;
        }
    }
}

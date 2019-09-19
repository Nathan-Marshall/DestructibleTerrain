using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DestrictubleTerrain
{
    public class DTPolygon
    {
        public List<Vector2> Contour { get; set; }
        public List<List<Vector2>> Holes { get; set; }

        public DTPolygon() {
            Contour = new List<Vector2>();
            Holes = new List<List<Vector2>>();
        }

        public DTPolygon(List<Vector2> contour) {
            Contour = contour;
            Holes = new List<List<Vector2>>();
        }

        public DTPolygon(List<Vector2> contour, List<List<Vector2>> holes) {
            Contour = contour;
            Holes = holes;
        }
    }
}

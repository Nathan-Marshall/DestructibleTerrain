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

        public DTPolygon(List<Vector2> contour = null, List<List<Vector2>> holes = null) {
            Contour = contour ?? new List<Vector2>();
            Holes = holes ?? new List<List<Vector2>>();
        }
    }
}

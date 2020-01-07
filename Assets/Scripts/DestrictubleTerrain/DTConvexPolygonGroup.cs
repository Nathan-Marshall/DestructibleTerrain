using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DestructibleTerrain
{
    public class DTConvexPolygroup : List<List<Vector2>>
    {
        public DTConvexPolygroup()
            : base() { }

        public DTConvexPolygroup(List<List<Vector2>> polygons)
            : base(polygons) { }

        public DTConvexPolygroup(List<DTPolygon> polygons)
            : base(polygons.Select(poly => poly.Contour).ToList()) { }

        public List<DTPolygon> ToPolygonList() {
            return this.Select(poly => new DTPolygon(poly)).ToList();
        }
    }
}

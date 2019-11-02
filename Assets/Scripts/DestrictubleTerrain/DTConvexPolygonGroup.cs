using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DestructibleTerrain
{
    public class DTConvexPolygonGroup : List<List<Vector2>>
    {
        public DTConvexPolygonGroup()
            : base() { }

        public DTConvexPolygonGroup(List<List<Vector2>> polygons)
            : base(polygons) { }

        public DTConvexPolygonGroup(List<DTPolygon> polygons)
            : base(polygons.Select(poly => poly.Contour).ToList()) { }

        public List<DTPolygon> ToPolygonList() {
            return this.Select(poly => new DTPolygon(poly)).ToList();
        }
    }
}

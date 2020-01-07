using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DestructibleTerrain.Triangulation
{
    public interface ITriangulator
    {
        DTMesh PolygonToMesh(DTPolygon subject);

        DTConvexPolygroup PolygonToTriangleList(DTPolygon subject);
    }
}

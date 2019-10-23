using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DestrictubleTerrain.Triangulation
{
    public interface ITriangulator
    {
        DTMesh PolygonToMesh(DTPolygon subject);

        List<DTPolygon> PolygonToTriangleList(DTPolygon subject);
    }
}

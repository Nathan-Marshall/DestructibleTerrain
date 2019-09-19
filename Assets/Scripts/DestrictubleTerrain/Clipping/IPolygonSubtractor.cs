using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DestrictubleTerrain.Clipping
{
    public interface IPolygonSubtractor
    {
        IEnumerable<DTPolygon> Subtract(IEnumerable<DTPolygon> subjects, IEnumerable<DTPolygon> clippingPolygons);
    }
}

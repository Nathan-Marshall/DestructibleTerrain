﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DestrictubleTerrain.Clipping
{
    public interface IPolygonSubtractor
    {
        // Takes a subject polygon and subtracts a clipping polygon from it.
        // Returns one or more polygons, depending on whether any part of the subject gets completely divided from the rest.
        IList<DTPolygon> Subtract(DTPolygon subject, DTPolygon clippingPolygon);

        // Takes a list of subject polygon groups and subtracts clipping polygons from it.
        // Returns a list of lists of modified subject polygon groups. Each list of polygon groups corresponds with one polygon group from the subjectGroups input,
        // and contains one or more polygon groups depending on whether any part the original polygon group gets completely divided from the rest.
        IList<List<List<DTPolygon>>> Subtract(IEnumerable<IEnumerable<DTPolygon>> subjectGroups, IEnumerable<DTPolygon> clippingPolygons);
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DestructibleTerrain.HertelMehlhorn
{
    public interface IHertelMehlhorn
    {
        DTMesh Execute(DTMesh input);

        DTConvexPolygonGroup Execute(DTConvexPolygonGroup input);
    }
}

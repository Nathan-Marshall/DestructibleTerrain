using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DestructibleTerrain.HertelMehlhorn
{
    public interface IHertelMehlhorn
    {
        DTMesh ExecuteToMesh(DTMesh input);

        DTMesh ExecuteToMesh(DTConvexPolygonGroup input);

        DTConvexPolygonGroup ExecuteToPolyGroup(DTMesh input);

        DTConvexPolygonGroup ExecuteToPolyGroup(DTConvexPolygonGroup input);
    }
}

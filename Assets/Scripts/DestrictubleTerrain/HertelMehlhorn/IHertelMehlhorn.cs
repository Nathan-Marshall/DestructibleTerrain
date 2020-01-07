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

        DTMesh ExecuteToMesh(DTConvexPolygroup input);

        DTConvexPolygroup ExecuteToPolygroup(DTMesh input);

        DTConvexPolygroup ExecuteToPolygroup(DTConvexPolygroup input);
    }
}

using DestructibleTerrain;
using DestructibleTerrain.Clipping;
using DestructibleTerrain.Destructible;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DestructibleTerrain.ExplosionExecution
{
    public interface IExplosionExecutor
    {
        void ExecuteExplosions(IEnumerable<Explosion> explosions, IEnumerable<DestructibleObject> dtObjects, IPolygonSubtractor subtractor);
    }
}

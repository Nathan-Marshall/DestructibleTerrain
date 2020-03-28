using ClipperLib;
using DestructibleTerrain;
using DestructibleTerrain.Clipping;
using DestructibleTerrain.Triangulation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TriangleNet.Geometry;
using UnityEngine;

namespace DestructibleTerrain.Destructible
{
    // DestructibleObject with custom Hertel-Mehlhorn partitioned clipping, custom Hertel-Mehlhorn partitioned collider, Triangle.Net triangulator
    public class DestructibleObjectCustomHMClippingCustomHMCollider_TriangleNetTriangulator : DestructibleObjectCustomHMClippingCustomHMCollider
    {
        protected override ITriangulator GetTriangulator() {
            return TriangleNetTriangulator.Instance;
        }
    }
}
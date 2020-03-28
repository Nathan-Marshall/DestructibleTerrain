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
    // DestructibleObject with polygon clipping, triangulated collider, Triangle.Net triangulator
    public class DestructibleObjectPolygonClippingTriangulatedCollider_TriangleNetTriangulator : DestructibleObjectPolygonClippingTriangulatedCollider
    {
        protected override ITriangulator GetTriangulator() {
            return TriangleNetTriangulator.Instance;
        }
    }
}
﻿using ClipperLib;
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
    public class DestructibleObjectPolygonClippingCustomHMCollider_PolyPartitionEarClippingTriangulator : DestructibleObjectPolygonClippingCustomHMCollider
    {
        protected override ITriangulator GetTriangulator() {
            return PolyPartitionEarClippingTriangulator.Instance;
        }
    }
}
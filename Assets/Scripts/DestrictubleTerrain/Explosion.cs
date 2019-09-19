using ClipperLib;
using DestrictubleTerrain;
using DestrictubleTerrain.Clipping;
using DestrictubleTerrain.Triangulation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TriangleNet.Geometry;
using UnityEngine;

public class Explosion
{
    public Vector2 Position { get; private set; }
    public float Radius { get; private set; }
    public DTPolygon DTPolygon { get; private set; }
    public DTMesh DTMesh { get; private set; }

    public Explosion(float x, float y, float radius, int numPoints) {
        Position = new Vector2(x, y);
        Radius = radius;

        List<Vector2> circle = new List<Vector2>();
        float step = 2 * Mathf.PI / numPoints;
        for (int i = 0; i < numPoints; ++i) {
            float angle = i * step;
            circle.Add(new Vector2(x + radius * Mathf.Cos(angle), y - radius * Mathf.Sin(angle)));
        }

        DTPolygon = new DTPolygon(circle);
        //DTMesh = TriangleNetAdapter.Instance.Triangulate(DTPolygon);
    }
}

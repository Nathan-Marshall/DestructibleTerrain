using DestrictubleTerrain;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class DTUtility
{
    // -1 means the object bounds are completely outside the explosion.
    // 0 means the bounds intersect.
    // 1 means the object bounds are completely inside the explosion.
    public static int BoundsCheck(DestructibleObject dtObj, Explosion exp) {
        Bounds oBounds = dtObj.GetComponent<Collider2D>().bounds;
        Vector3 ePos = new Vector3(exp.Position.x, exp.Position.y);
        float radSq = exp.Radius * exp.Radius;

        // Completely outside
        if ((oBounds.ClosestPoint(ePos) - ePos).sqrMagnitude >= radSq) {
            return -1;
        }

        // Compute furthest point
        Vector3 furthestPoint = new Vector3(oBounds.min.x, oBounds.min.y, 0);
        if (oBounds.center.x > ePos.x) {
            furthestPoint.x = oBounds.max.x;
        }
        if (oBounds.center.y > ePos.y) {
            furthestPoint.y = oBounds.max.y;
        }

        // Completely inside
        if ((furthestPoint - ePos).sqrMagnitude <= radSq) {
            return 1;
        }

        // Bounds intersect
        return 0;
    }

    public static IList<DTPolygon> MeshToPolygonList(DTMesh mesh) {
        return mesh.Partitions.Select(part => new DTPolygon(part.Select(i => mesh.Vertices[i]).ToList())).ToList();
    }
}

using DestrictubleTerrain;
using DestrictubleTerrain.Clipping;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ExplosionExecutor
{
    public static void ExecuteExplosions(IEnumerable<Explosion> explosions, IEnumerable<DestructibleObject> objects, IPolygonSubtractor subtractor) {
        // Do we make new DestructibleObjects out of the returned polygons or do we run this iteratively and check if more than one polygon is returned?
        /*
        IEnumerable<DTPolygon> result1 = ClipperAdapter.Instance.Subtract(objects.Select(o => o.GetTransformedPolygon()), explosions.Select(e => e.DTPolygon));
        int resultCount = result1.Count();
        List<DestructibleObject> objectList = objects.ToList();
        while (objectList.Count > resultCount) {
            DestructibleObject o = objectList[objectList.Count - 1];
            objectList.RemoveAt(objectList.Count - 1);
            Destroy(o.gameObject);
        }
        while (objectList.Count < resultCount) {
            GameObject go = new GameObject();
            DestructibleObject dto = go.AddComponent<DestructibleObject>();
            objectList.Add(dto);
        }
        int objectIndex = 0;
        foreach (DTPolygon poly in result1) {
            objectList[objectIndex].ApplyTransformedPolygon(poly);
            ++objectIndex;
        }
        */


        // Store destructible objects in a new list, since we may add or remove some during processing
        List<DestructibleObject> objectList = objects.ToList();
        // Add new destructible objects to this list instead of objectList until finished processing the current explosion
        List<DestructibleObject> pendingAdditions = new List<DestructibleObject>();

        // Process all objects for all explosions
        foreach (var e in explosions) {
            Vector3 ePos = new Vector3(e.Position.x, e.Position.y);

            for (int i = 0; i < objectList.Count; i++) {
                DestructibleObject o = objectList[i];

                // Do basic AABB-circle check to see whether we can skip processing this object with this explosion
                Bounds oBounds = o.GetComponent<Collider2D>().bounds;
                if ((oBounds.ClosestPoint(ePos) - ePos).sqrMagnitude >= e.Radius*e.Radius) {
                    continue;
                }

                // Subtract explosion polygon from destructible object polygon
                IEnumerable<DTPolygon> result = subtractor.Subtract(new DTPolygon[] { o.GetTransformedPolygon() }, new DTPolygon[] { e.DTPolygon });

                int count = result.Count();
                if (count == 0) {
                    // If no output polygons, remove the current destrucible object
                    objectList.RemoveAt(i--);
                    Object.Destroy(o.gameObject);
                } else {
                    // Otherwise apply the output polygons (fragments) to GameObjects (new or reused)
                    foreach (DTPolygon poly in result) {
                        if (poly != result.Last()) {
                            // Duplicate the GameObject that was clipped by the explosion, so that we maintain properties such as velocity
                            GameObject go = Object.Instantiate(o.gameObject, o.transform.parent);
                            DestructibleObject newObj = go.GetComponent<DestructibleObject>();

                            // Apply the new clipped polygon
                            newObj.ApplyTransformedPolygon(poly);

                            // Add it to the objectList, but not until after finished processing this explosion
                            pendingAdditions.Add(newObj);
                        } else {
                            // Reuse the existing GameObject by applying the new clipped polygon to it
                            o.ApplyTransformedPolygon(poly);
                        }
                    }
                }
            }
            // Add pendingAdditions elements to objectList so that they are included when processing the next explosion in explosions
            objectList.AddRange(pendingAdditions);
            pendingAdditions.Clear();
        }
    }
}

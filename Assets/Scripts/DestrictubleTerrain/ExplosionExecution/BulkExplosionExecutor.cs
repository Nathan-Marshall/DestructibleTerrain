using DestrictubleTerrain;
using DestrictubleTerrain.Clipping;
using DestrictubleTerrain.Triangulation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DestrictubleTerrain.ExplosionExecution
{
    // This class iterates explosions still (so that it can do bounds checking) but takes advantage of
    // IPolygonSubtractor's SubtractBulk to handle multiple polygons at once.
    public sealed class BulkExplosionExecutor : IExplosionExecutor
    {
        private static readonly Lazy<BulkExplosionExecutor> lazyInstance =
            new Lazy<BulkExplosionExecutor>(() => new BulkExplosionExecutor());

        // Singleton intance
        public static BulkExplosionExecutor Instance {
            get { return lazyInstance.Value; }
        }

        private BulkExplosionExecutor() { }

        public void ExecuteExplosions(IEnumerable<Explosion> explosions, IEnumerable<DestructibleObject> dtObjects, IPolygonSubtractor subtractor) {
            // Store destructible objects in a new list, since we may add or remove some during processing
            List<DestructibleObject> dtObjectList = dtObjects.ToList();
            // Add new destructible objects to this list instead of objectList until finished processing the current explosion
            List<DestructibleObject> pendingAdditions = new List<DestructibleObject>();

            // Process all objects for all explosions
            foreach (var exp in explosions) {
                List<List<DTPolygon>> relevantObjectPolygons = new List<List<DTPolygon>>();
                List<int> relevantObjectIndices = new List<int>();
                for (int i = 0; i < dtObjectList.Count; ++i) {
                    var dtObj = dtObjectList[i];

                    // Do basic AABB-circle check to see whether we can skip processing this destructible object with this explosion
                    int bc = BoundsCheck(dtObj, exp);
                    if (bc == -1) {
                        // Object is not affected by explosion
                        continue;
                    } else if (bc == 1) {
                        // Object is completely removed by explosion
                        dtObjectList[i] = null;
                        UnityEngine.Object.Destroy(dtObj.gameObject);
                        continue;
                    } else {
                        // Add object to the input list for the subtractor
                        relevantObjectPolygons.Add(new List<DTPolygon> { dtObj.GetTransformedPolygon() });
                        relevantObjectIndices.Add(i);
                        continue;
                    }
                }

                var result = subtractor.SubtractBulk(relevantObjectPolygons, new DTPolygon[] { exp.DTPolygon });

                // Iterate results corresponding to each input polygon group
                for (int i = 0; i < result.Count; i++) {
                    // Add new destructible objects for any output polygons that could not be matched with an input polygon
                    if (i >= relevantObjectPolygons.Count) {
                        GameObject go = new GameObject();
                        DestructibleObject newObj = go.AddComponent<DestructibleObject>();
                        IList<DTPolygon> polyGroup = result[i][0];
                        // Use only index 0 because we are dealing with single-polygon destructible objects
                        newObj.ApplyTransformedPolygon(polyGroup[0]);
                        pendingAdditions.Add(newObj);

                        continue;
                    }

                    // We know that these output polygons correspond to one or more pieces of this existing destructible object
                    DestructibleObject dtObj = dtObjectList[relevantObjectIndices[i]];

                    if (result[i].Count == 0) {
                        // If no output polygons, remove the current destrucible object
                        dtObjectList[relevantObjectIndices[i]] = null;
                        UnityEngine.Object.Destroy(dtObj.gameObject);
                        continue;
                    } else {
                        // Otherwise apply the output polygons (fragments) to GameObjects (new or reused)
                        foreach (List<DTPolygon> polyGroup in result[i]) {
                            if (polyGroup != result[i].Last()) {
                                // Duplicate the GameObject that was clipped by the explosion, so that we maintain properties such as velocity
                                GameObject go = UnityEngine.Object.Instantiate(dtObj.gameObject, dtObj.transform.parent);
                                DestructibleObject newObj = go.GetComponent<DestructibleObject>();

                                // Apply the new clipped polygon
                                // Use only index 0 because we are dealing with single-polygon destructible objects
                                newObj.ApplyTransformedPolygon(polyGroup[0]);

                                // Add it to the objectList, but not until after finished processing this explosion
                                pendingAdditions.Add(newObj);
                                continue;
                            } else {
                                // Reuse the existing GameObject by applying the new clipped polygon to it
                                // Use only index 0 because we are dealing with single-polygon destructible objects
                                dtObj.ApplyTransformedPolygon(polyGroup[0]);
                                continue;
                            }
                        }
                    }
                }

                // Delete any entries that were set to null
                for (int i = 0; i < dtObjectList.Count; ++i) {
                    if (dtObjectList[i] == null) {
                        dtObjectList.RemoveAt(i--);
                    }
                }

                // Add pendingAdditions elements to objectList so that they are included when processing the next explosion in explosions
                dtObjectList.AddRange(pendingAdditions);
                pendingAdditions.Clear();
            }
        }

        // -1 means the object bounds are completely outside the explosion.
        // 0 means the bounds intersect.
        // 1 means the object bounds are completely inside the explosion.
        private static int BoundsCheck(DestructibleObject dtObj, Explosion exp) {
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
    }
}
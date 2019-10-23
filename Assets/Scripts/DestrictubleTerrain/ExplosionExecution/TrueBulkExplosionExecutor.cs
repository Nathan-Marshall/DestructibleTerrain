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
    // This sends all explosions and polygons to the subtractor at once, but cannot perform bounds checking.
    public sealed class TrueBulkExplosionExecutor : IExplosionExecutor
    {
        private static readonly Lazy<TrueBulkExplosionExecutor> lazyInstance =
            new Lazy<TrueBulkExplosionExecutor>(() => new TrueBulkExplosionExecutor());

        // Singleton intance
        public static TrueBulkExplosionExecutor Instance {
            get { return lazyInstance.Value; }
        }

        private TrueBulkExplosionExecutor() { }

        public void ExecuteExplosions(IEnumerable<Explosion> explosions, IEnumerable<DestructibleObject> dtObjects, IPolygonSubtractor subtractor) {
            // Store destructible objects in a new list, since we may add or remove some during processing
            List<DestructibleObject> dtObjectList = dtObjects.ToList();
            // Add new destructible objects to this list instead of objectList until finished processing the current explosion
            List<DestructibleObject> pendingAdditions = new List<DestructibleObject>();

            // Process all objects for all explosions
            var explosionPolygons = explosions.Select(e => e.DTPolygon);
            var dtObjectPolygons = dtObjectList.Select(o => o.GetTransformedPolygonList());
            int numInputPolygonGroups = dtObjectPolygons.Count();

            var result = subtractor.SubtractBulk(dtObjectPolygons, explosionPolygons);

            // Iterate results corresponding to each input polygon group
            for (int i = 0; i < result.Count; i++) {
                // Add new destructible objects for any output polygon groups that could not be matched with an input polygon group
                if (i >= numInputPolygonGroups) {
                    GameObject go = new GameObject();
                    DestructibleObject newObj = go.AddComponent<DestructibleObject>();
                    List<DTPolygon> polyGroup = result[i][0];
                    newObj.ApplyTransformedPolygonList(polyGroup);
                    pendingAdditions.Add(newObj);

                    continue;
                }

                // We know that these output polygons correspond to one or more pieces of this existing destructible object
                DestructibleObject dtObj = dtObjectList[i];

                if (result[i].Count == 0) {
                    // If no output polygons, remove the current destrucible object
                    dtObjectList[i] = null;
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
                            newObj.ApplyTransformedPolygonList(polyGroup);

                            // Add it to the objectList, but not until after finished processing this explosion
                            pendingAdditions.Add(newObj);
                            continue;
                        } else {
                            // Reuse the existing GameObject by applying the new clipped polygon to it
                            dtObj.ApplyTransformedPolygonList(polyGroup);
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
}
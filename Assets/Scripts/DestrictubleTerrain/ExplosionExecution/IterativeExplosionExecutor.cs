﻿using DestrictubleTerrain;
using DestrictubleTerrain.Clipping;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DestrictubleTerrain.ExplosionExecution
{
    public sealed class IterativeExplosionExecutor : IExplosionExecutor
    {
        private static readonly Lazy<IterativeExplosionExecutor> lazyInstance = new Lazy<IterativeExplosionExecutor>(() => new IterativeExplosionExecutor());

        // Singleton intance
        public static IterativeExplosionExecutor Instance {
            get { return lazyInstance.Value; }
        }

        private IterativeExplosionExecutor() { }

        public void ExecuteExplosions(IEnumerable<Explosion> explosions, IEnumerable<DestructibleObject> dtObjects, IPolygonSubtractor subtractor) {
            // Store destructible objects in a new list, since we may add or remove some during processing
            List<DestructibleObject> dtObjectList = dtObjects.ToList();
            // Add new destructible objects to this list instead of objectList until finished processing the current explosion
            List<DestructibleObject> pendingAdditions = new List<DestructibleObject>();

            // Process all objects for all explosions
            foreach (var exp in explosions) {
                for (int i = 0; i < dtObjectList.Count; i++) {
                    DestructibleObject dtObj = dtObjectList[i];

                    // Do basic AABB-circle check to see whether we can skip processing this destructible object with this explosion
                    if (!BoundsCheck(dtObj, exp)) {
                        continue;
                    }

                    // Subtract explosion polygon from destructible object polygon
                    IEnumerable<DTPolygon> result = subtractor.Subtract(dtObj.GetTransformedPolygon(), exp.DTPolygon);

                    int count = result.Count();
                    if (count == 0) {
                        // If no output polygons, remove the current destrucible object
                        dtObjectList.RemoveAt(i--);
                        UnityEngine.Object.Destroy(dtObj.gameObject);
                    } else {
                        // Otherwise apply the output polygons (fragments) to GameObjects (new or reused)
                        foreach (DTPolygon poly in result) {
                            if (poly != result.Last()) {
                                // Duplicate the GameObject that was clipped by the explosion, so that we maintain properties such as velocity
                                GameObject go = UnityEngine.Object.Instantiate(dtObj.gameObject, dtObj.transform.parent);
                                DestructibleObject newObj = go.GetComponent<DestructibleObject>();

                                // Apply the new clipped polygon
                                newObj.ApplyTransformedPolygon(poly);

                                // Add it to the objectList, but not until after finished processing this explosion
                                pendingAdditions.Add(newObj);
                            } else {
                                // Reuse the existing GameObject by applying the new clipped polygon to it
                                dtObj.ApplyTransformedPolygon(poly);
                            }
                        }
                    }
                }
                // Add pendingAdditions elements to objectList so that they are included when processing the next explosion in explosions
                dtObjectList.AddRange(pendingAdditions);
                pendingAdditions.Clear();
            }
        }

        private static bool BoundsCheck(DestructibleObject dtObj, Explosion exp) {
            Bounds oBounds = dtObj.GetComponent<Collider2D>().bounds;
            Vector3 ePos = new Vector3(exp.Position.x, exp.Position.y);
            return (oBounds.ClosestPoint(ePos) - ePos).sqrMagnitude < exp.Radius * exp.Radius;
        }
    }
}
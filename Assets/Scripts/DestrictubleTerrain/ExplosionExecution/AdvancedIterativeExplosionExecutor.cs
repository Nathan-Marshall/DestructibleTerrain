using DestructibleTerrain;
using DestructibleTerrain.Clipping;
using DestructibleTerrain.Destructible;
using DestructibleTerrain.Triangulation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DestructibleTerrain.ExplosionExecution
{
    // This class simply iterates all explosions and all polygons in O(n^2) time subtracting a single explosion from a
    // single polygon at a time, but does bounds checking to skip processing most polygons.
    public sealed class AdvancedIterativeExplosionExecutor : IExplosionExecutor
    {
        private static readonly Lazy<AdvancedIterativeExplosionExecutor> lazyInstance =
            new Lazy<AdvancedIterativeExplosionExecutor>(() => new AdvancedIterativeExplosionExecutor());

        // Singleton intance
        public static AdvancedIterativeExplosionExecutor Instance {
            get { return lazyInstance.Value; }
        }

        private AdvancedIterativeExplosionExecutor() { }

        public void ExecuteExplosions(IEnumerable<Explosion> explosions, IEnumerable<DestructibleObject> dtObjects, IPolygonSubtractor subtractor) {
            ORourkeSubtractor oRourkeSub = (ORourkeSubtractor)subtractor;
            if (oRourkeSub == null) {
                throw new NotSupportedException("This explosion executor only supports ORourkeSubtractor");
            }
            
            TriangleNetTriangulator.Instance.callCount = 0;

            // Store destructible objects in a new list, since we may add or remove some during processing
            List<DestructibleObject> dtObjectList = dtObjects.ToList();
            // Add new destructible objects to this list instead of objectList until finished processing the current explosion
            List<DestructibleObject> pendingAdditions = new List<DestructibleObject>();

            // Process all objects for all explosions
            foreach (var exp in explosions) {
                for (int i = 0; i < dtObjectList.Count; i++) {
                    // Remove this object from the list if it has been destroyed
                    if (dtObjectList[i] == null) {
                        dtObjectList.RemoveAt(i--);
                        continue;
                    }
                    // Cast this DO to an advanced DO
                    var dtObj = (DO_Advanced_Triangle_Clip_Collide)dtObjectList[i];
                    if (dtObj == null) {
                        throw new NotSupportedException("This explosion executor only supports DO_Advanced_Triangle_Clip_Collide");
                    }

                    // Do basic AABB-circle check to see whether we can skip processing this destructible object with this explosion
                    int bc = DTUtility.BoundsCheck(dtObj, exp);
                    if (bc == -1) {
                        // Object is not affected by explosion
                        continue;
                    } else if (bc == 1) {
                        // Object is completely removed by explosion
                        dtObjectList.RemoveAt(i--);
                        UnityEngine.Object.Destroy(dtObj.gameObject);
                        continue;
                    }

                    // Leave the polygroup in local coordinates and transform the explosion instead to the DO's space.
                    // Note that this is stored in the subject DO and will be referenced by all PolygroupModifiers,
                    // so changes to the original DO will affect all PolygroupModifiers. This shouldn't cause any
                    // problems since we only need the indices anyway.
                    PolygroupModifier inputPolygroup = dtObj.GetPolygroup();
                    List<Vector2> transformedExplosion = dtObj.InverseTransformPoints(exp.DTPolygon.Contour);

                    // Subtract explosion polygon from destructible object polygon group
                    DTProfilerMarkers.SubtractPolygroup.Begin();
                    List<PolygroupModifier> result = oRourkeSub.AdvancedSubtractPolygroup(inputPolygroup, transformedExplosion);
                    DTProfilerMarkers.SubtractPolygroup.End();

                    int count = result.Count();
                    if (count == 0) {
                        // If no output polygons, remove the current destrucible object
                        dtObjectList.RemoveAt(i--);
                        UnityEngine.Object.Destroy(dtObj.gameObject);
                        continue;
                    } else {
                        // Otherwise apply the output polygons (fragments) to GameObjects (new or reused)
                        for (int j = 0; j < result.Count; j++) {
                            if (j < result.Count - 1) {
                                // Duplicate the GameObject that was clipped by the explosion, so that we maintain properties such as velocity and also maintain the same collider + mesh
                                GameObject go = UnityEngine.Object.Instantiate(dtObj.gameObject, dtObj.transform.parent);
                                var newObj = go.GetComponent<DO_Advanced_Triangle_Clip_Collide>();
                                newObj.SetPolygroup(new PolygroupModifier(new DTConvexPolygroup(inputPolygroup.originalPolygroup), inputPolygroup.keptIndices, null));

                                // Apply the new clipped polygon list
                                newObj.ApplyPolygroupModifier(result[j]);

                                // Add it to the objectList, but not until after finished processing this explosion
                                pendingAdditions.Add(newObj);
                                continue;
                            } else {
                                // Reuse the existing GameObject by applying the new clipped polygon to it
                                dtObj.ApplyPolygroupModifier(result[j]);
                                continue;
                            }
                        }
                    }
                }
                // Add pendingAdditions elements to objectList so that they are included when processing the next explosion in explosions
                dtObjectList.AddRange(pendingAdditions);
                pendingAdditions.Clear();
            }

            Debug.Log("# Objects:" + dtObjectList.Count);
            Debug.Log("# Polygons:" + dtObjectList.Sum(obj => obj.GetTransformedPolygonList().Count));
            Debug.Log("# Triangulation Calls:" + TriangleNetTriangulator.Instance.callCount);
        }
    }
}
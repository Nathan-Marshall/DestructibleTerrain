using ClipperLib;
using DestrictubleTerrain;
using DestrictubleTerrain.Clipping;
using DestrictubleTerrain.Triangulation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TriangleNet.Geometry;
using UnityEngine;

namespace DestrictubleTerrain.Destructible
{
    public class DestructibleObjectPolygonClippingHMCollider : DestructibleObject
    {
        private DTPolygon dtPolygon;

        protected override void Start() {
            base.Start();

            // Assign default polygon when this component is attached in the editor
            if (dtPolygon == null && Application.isEditor) {
                ApplyPolygonList(new List<DTPolygon>() {
                    new DTPolygon(
                        new List<Vector2> {
                            new Vector2(-1, -1),
                            new Vector2(-1,  1),
                            new Vector2( 1,  1),
                            new Vector2( 1, -1)
                        },
                        new List<List<Vector2>> {
                            new List<Vector2> {
                                new Vector2(-0.75f, -0.75f),
                                new Vector2( 0.75f, -0.75f),
                                new Vector2( 0.75f,  0.75f),
                                new Vector2(-0.75f,  0.75f)
                            }
                        })
                });
            }
        }

        public override List<DTPolygon> GetTransformedPolygonList() {
            return new List<DTPolygon>() {
                new DTPolygon(
                    dtPolygon.Contour.Select(TransformPoint).ToList(),
                    dtPolygon.Holes.Select(hole => hole.Select(TransformPoint).ToList()).ToList())
            };
        }

        public override void ApplyPolygonList(List<DTPolygon> dtPolygonList) {
            if (dtPolygon == dtPolygonList[0]) {
                return;
            }
            dtPolygon = dtPolygonList[0];

            DTProfileMarkers.Triangulation.Begin();
            DTMesh dtMesh = TriangleNetAdapter.Instance.PolygonToMesh(dtPolygon);
            DTProfileMarkers.Triangulation.End();
            //PolyPartitionHM.ConvexPartition_HM(dtPolygon.ToTPPLPolyList(), out List<PolyPartitionHM.TPPLPoly> hmOutput);
            //DTMesh dtHMMesh = TriangleNetAdapter.Instance.PolygonToHMMesh(dtPolygon);

            // Collider from polygon
            DTProfileMarkers.HertelMehlhorn.Begin();
            DTMesh hmMesh = HertelMehlhorn.Execute(dtMesh);
            DTProfileMarkers.HertelMehlhorn.End();

            ApplyCollider(hmMesh);

            // Create mesh from triangulated polygon
            ApplyRenderMesh(dtMesh);
        }

        public override void ApplyTransformedPolygonList(List<DTPolygon> transformedPolygonList) {
            List<DTPolygon> dtPolygonList = new List<DTPolygon>() {
                new DTPolygon(
                    transformedPolygonList[0].Contour.Select(InverseTransformPoint).ToList(),
                    transformedPolygonList[0].Holes.Select(hole => hole.Select(InverseTransformPoint).ToList()).ToList())
            };

            ApplyPolygonList(dtPolygonList);
        }
    }
}
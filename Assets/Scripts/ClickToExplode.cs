using DestructibleTerrain;
using DestructibleTerrain.Clipping;
using DestructibleTerrain.Destructible;
using DestructibleTerrain.ExplosionExecution;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ClickToExplode : MonoBehaviour
{
    public Camera Camera;

    void Update() {
        if (Input.GetMouseButtonDown(0)) {
            Vector3 worldPoint = Camera.ScreenToWorldPoint(Input.mousePosition);

            List<Explosion> explosions = new List<Explosion> {
                new Explosion(worldPoint.x, worldPoint.y, 2.0f, 24),
            };

            IEnumerable<DestructibleObject> destructibleObjects = DestructibleObject.FindAll();

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Restart();

            AdvancedIterativeExplosionExecutor.Instance.ExecuteExplosions(explosions, destructibleObjects, ORourkeSubtractor.Instance);

            stopwatch.Stop();
            //stopwatch.LogTime("Execute Explosions");
        }
    }
}

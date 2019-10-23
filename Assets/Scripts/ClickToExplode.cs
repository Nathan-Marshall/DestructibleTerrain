using DestrictubleTerrain;
using DestrictubleTerrain.Clipping;
using DestrictubleTerrain.Destructible;
using DestrictubleTerrain.ExplosionExecution;
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
                new Explosion(worldPoint.x - 0.3f, worldPoint.y, 0.5f, 24),
                new Explosion(worldPoint.x, worldPoint.y, 0.5f, 24),
                new Explosion(worldPoint.x + 0.3f, worldPoint.y, 0.5f, 24)
            };

            IEnumerable<DestructibleObject> destructibleObjects = DestructibleObject.FindAll();

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Restart();

            IterativeExplosionExecutor.Instance.ExecuteExplosions(explosions, destructibleObjects, ClipperAdapter.Instance);

            stopwatch.Stop();
            stopwatch.LogTime("Execute Explosions");
        }
    }
}

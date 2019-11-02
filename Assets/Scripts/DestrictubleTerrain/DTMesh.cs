using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DestructibleTerrain
{
    public class DTMesh
    {
        public List<Vector2> Vertices { get; set; }
        public List<List<int>> Partitions { get; set; }

        public DTMesh() {}

        public DTMesh(List<Vector2> vertices, List<List<int>> partitions) {
            Vertices = vertices;
            Partitions = partitions;
        }
    }
}

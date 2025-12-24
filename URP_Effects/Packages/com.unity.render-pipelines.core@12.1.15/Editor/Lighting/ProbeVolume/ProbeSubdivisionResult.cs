using System.Collections.Generic;
using Unity.Collections;
using System;
using UnityEditor;
using Brick = UnityEngine.Experimental.Rendering.ProbeBrickIndex.Brick;
using UnityEngine.SceneManagement;

namespace UnityEngine.Experimental.Rendering
{
    class ProbeSubdivisionResult
    {
        // 记录每个cell的位置；cell内brick的列表；cell关联的场景
        public List<Vector3Int> cellPositions = new List<Vector3Int>();
        public Dictionary<Vector3Int, List<Brick>> bricksPerCells = new Dictionary<Vector3Int, List<Brick>>();
        public Dictionary<Vector3Int, HashSet<Scene>> scenesPerCells = new Dictionary<Vector3Int, HashSet<Scene>>();
    }
}

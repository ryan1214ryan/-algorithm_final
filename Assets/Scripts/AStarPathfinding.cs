using System.Collections.Generic;
using UnityEngine;

public class AStarPathfinding : MonoBehaviour
{
    private PathfindingGrid pathGrid;

    void Start()
    {
        pathGrid = GetComponent<PathfindingGrid>();
    }

    public List<Vector3> FindAStarPath(Vector3Int startGrid, Vector3Int targetGrid)
    {
        if (pathGrid == null || !pathGrid.gridNodes.ContainsKey(startGrid) || !pathGrid.gridNodes.ContainsKey(targetGrid))
            return null;

        List<Node> openList = new List<Node>();
        HashSet<Node> closedList = new HashSet<Node>();

        Node startNode = pathGrid.gridNodes[startGrid];
        Node targetNode = pathGrid.gridNodes[targetGrid];

        startNode.gCost = 0;
        startNode.hCost = GetDiagonalDistance(startGrid, targetGrid); // 改用對角線距離估算
        startNode.parent = null;
        openList.Add(startNode);

        while (openList.Count > 0)
        {
            // 修正版：預設先拿清單的第一個格子 (第 0 項) 當作基準點
            Node currentNode = openList[0];

            for (int i = 1; i < openList.Count; i++)
            {
                // 比較誰的 fCost 總代價比較低
                if (openList[i].fCost < currentNode.fCost ||
                    (openList[i].fCost == currentNode.fCost && openList[i].hCost < currentNode.hCost))
                {
                    currentNode = openList[i];
                }
            }


            openList.Remove(currentNode);
            closedList.Add(currentNode);

            if (currentNode == targetNode)
            {
                return RetracePath(startNode, targetNode);
            }

            List<Node> neighbors = pathGrid.GetNeighbors(currentNode);
            foreach (Node neighbor in neighbors)
            {
                if (neighbor.isWall || closedList.Contains(neighbor)) continue;

                // 技術優化：防止怪物斜切穿牆
                Vector3Int direction = neighbor.gridPos - currentNode.gridPos;
                if (direction.x != 0 && direction.y != 0)
                {
                    Vector3Int sideX = currentNode.gridPos + new Vector3Int(direction.x, 0, 0);
                    Vector3Int sideY = currentNode.gridPos + new Vector3Int(0, direction.y, 0);
                    if (pathGrid.gridNodes.ContainsKey(sideX) && pathGrid.gridNodes[sideX].isWall) continue;
                    if (pathGrid.gridNodes.ContainsKey(sideY) && pathGrid.gridNodes[sideY].isWall) continue;
                }

                // 計算移動成本：直走 10，斜走 14
                int movementCostToNeighbor = (direction.x != 0 && direction.y != 0) ? 14 : 10;
                int newGCost = currentNode.gCost + movementCostToNeighbor;

                if (newGCost < neighbor.gCost || !openList.Contains(neighbor))
                {
                    neighbor.gCost = newGCost;
                    neighbor.hCost = GetDiagonalDistance(neighbor.gridPos, targetGrid);
                    neighbor.parent = currentNode;

                    if (!openList.Contains(neighbor))
                    {
                        openList.Add(neighbor);
                    }
                }
            }
        }
        return null;
    }

    // 💡 數學優化：對角線距離公式（Chebyshev/Octile Distance 放大 10 倍整數版）
    int GetDiagonalDistance(Vector3Int posA, Vector3Int posB)
    {
        int dstX = Mathf.Abs(posA.x - posB.x);
        int dstY = Mathf.Abs(posA.y - posB.y);

        if (dstX > dstY)
            return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }

    List<Vector3> RetracePath(Node startNode, Node endNode)
    {
        List<Vector3> path = new List<Vector3>();
        Node curr = endNode;
        while (curr != startNode)
        {
            path.Add(curr.worldPos);
            curr = curr.parent;
        }
        path.Reverse();
        return path;
    }
}

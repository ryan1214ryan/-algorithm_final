using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PathfindingGrid : MonoBehaviour
{
    private MapGenerator mapGen;   // 用來讀取地圖尺寸
    private Tilemap tilemap;       // 用來偵測格子上的 Tile 種類

    // 使用 Dictionary 儲存所有網格節點，比固定 2D 陣列更能完美應對中心點擴散的負數座標
    public Dictionary<Vector3Int, Node> gridNodes = new Dictionary<Vector3Int, Node>();

    void Start()
    {
        mapGen = FindObjectOfType<MapGenerator>();
        tilemap = mapGen.tilemap;

        // 初始化網格資料結構
        InitializeSearchGrid();
    }

    // 核心方法：掃描整個 Tilemap，將格子資訊轉化為演算法認得的 Node
    public void InitializeSearchGrid()
    {
        gridNodes.Clear();

        int halfWidth = mapGen.width / 2;
        int halfHeight = mapGen.height / 2;

        // 對應你的中心點擴散生成範圍
        for (int x = -halfWidth; x <= halfWidth; x++)
        {
            for (int y = -halfHeight; y <= halfHeight; y++)
            {
                Vector3Int currentGridPos = new Vector3Int(x, y, 0);

                // 取得這個格子在 Unity 2D 世界中的中心座標
                Vector3 worldPos = tilemap.CellToWorld(currentGridPos) + tilemap.cellSize * 0.5f;

                // 偵測這個格子目前放置的是哪一種 Tile
                TileBase currentTile = tilemap.GetTile(currentGridPos);

                // 如果格子是空的，或者上面的 Tile 名字叫做 WallTile，就認定它是牆壁
                bool isWall = (currentTile == null || currentTile == mapGen.wallTile);

                // 建立節點並存入字典中
                Node newNode = new Node(currentGridPos, worldPos, isWall);
                gridNodes.Add(currentGridPos, newNode);
            }
        }
    }

    // 提供給外部演算法（A* 或流場）快速取得周邊 4 個鄰居節點的工具方法
    // 升級版：提供周邊 8 個鄰居（含斜對角）
    public List<Node> GetNeighbors(Node node)
    {
        List<Node> neighbors = new List<Node>();

        // 包含斜角的 8 個方向向量
        Vector3Int[] directions = {
            Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right,
            new Vector3Int(1, 1, 0),   // 右上
            new Vector3Int(-1, 1, 0),  // 左上
            new Vector3Int(1, -1, 0),  // 右下
            new Vector3Int(-1, -1, 0)  // 左下
        };

        foreach (Vector3Int dir in directions)
        {
            Vector3Int neighborPos = node.gridPos + dir;
            if (gridNodes.ContainsKey(neighborPos))
            {
                neighbors.Add(gridNodes[neighborPos]);
            }
        }
        return neighbors;
    }

}

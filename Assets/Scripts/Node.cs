using UnityEngine;

public class Node
{
    public Vector3Int gridPos;   // 這格在 Tilemap 的網格座標 (X, Y, 0)
    public Vector3 worldPos;     // 這格在 Unity 中的世界座標
    public bool isWall;          // 這格是否為黑色牆壁 (不可通行)

    // --- 【全域流場】專用資料 ---
    public int integrationCost;   // 積分值（距離終點的累積成本）
    public Vector3 flowDirection; // 流場方向向量（箭頭朝向）

    // --- 【精確 A* 搜尋】專用資料 ---
    public int gCost;            // 從起點到這格的實際成本
    public int hCost;            // 這格到終點的預估成本（曼哈頓距離）
    public Node parent;          // 父節點（用來倒推回溯最完美路徑）

    // 計算 f = g + h 的快速屬性
    public int fCost => gCost + hCost;

    // 建構子：初始化這格的狀態
    public Node(Vector3Int _gridPos, Vector3 _worldPos, bool _isWall)
    {
        gridPos = _gridPos;
        worldPos = _worldPos;
        isWall = _isWall;

        // 初始流場資料
        integrationCost = 65535; // 預設給一個極大值代表還沒算過
        flowDirection = Vector3.zero;
    }
}

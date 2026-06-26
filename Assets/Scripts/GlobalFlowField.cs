using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class GlobalFlowField : MonoBehaviour
{
    private PathfindingGrid pathGrid;
    private MapGenerator mapGen;

    [Header("Game 畫面顯示設定")]
    public bool showVisualsInGame = false;
    public KeyCode toggleKey = KeyCode.G;

    private List<LineRenderer> arrowLines = new List<LineRenderer>();
    private LineRenderer greenBoxLine;
    private GameObject visualContainer;
    private Vector3Int lastTrackedPlayerPos;

    void Start()
    {
        pathGrid = GetComponent<PathfindingGrid>();
        mapGen = GetComponent<MapGenerator>();

        visualContainer = new GameObject("GameFlowFieldVisuals");
        visualContainer.transform.SetParent(this.transform);

        GameObject boxObj = new GameObject("GameGreenBox");
        boxObj.transform.SetParent(visualContainer.transform);
        greenBoxLine = boxObj.AddComponent<LineRenderer>();
        ConfigureLine(greenBoxLine, Color.green, 0.08f);
        greenBoxLine.positionCount = 5;

        visualContainer.SetActive(showVisualsInGame);
    }

    void Update()
    {
        if (mapGen != null && mapGen.currentPathMode == MapGenerator.PathfindingMode.PureAStar)
        {
            visualContainer.SetActive(false);
        }
        else
        {
            visualContainer.SetActive(showVisualsInGame);
        }
        if (Input.GetKeyDown(toggleKey))
        {
            showVisualsInGame = !showVisualsInGame;
            visualContainer.SetActive(showVisualsInGame);

            if (showVisualsInGame)
            {
                CreateOrUpdateArrowLines();
            }
            UnityEngine.Debug.Log("【鍵盤切換】Game 視窗輔助線條顯示狀態: " + showVisualsInGame);
        }

        if (showVisualsInGame)
        {
            UpdateGameVisuals();

            // 如果流場中心點變更了，同步更新所有箭頭的位置與朝向
            if (mapGen != null)
            {
                // 我們需要去 MapGenerator 偷看他記錄的流場中心點座標
                // 這裡利用一個小技巧去取得 MapGenerator 的私有/內部欄位（我們用反射或直接改讀他的lastPlayerGridPos）
                // 為了不弄複雜，我們在下方直接用 mapGen 傳遞過來的流場觸發點判定
            }
        }
    }

    // 透過外部直接連動：當 MapGenerator 觸發流場重算時，同步叫這裡更新箭頭
    public void NotifyFlowFieldUpdated()
    {
        if (showVisualsInGame)
        {
            CreateOrUpdateArrowLines();
        }
    }

    // 核心：手寫流場計算（在此函式最後加上連動通知）
    // 🚀 終極修正版：八向真實代價流場，徹底解決斜角卡牆與死路陷阱
    // 🚀 修正版：八向真實代價流場，徹底解決斜角卡牆、變數命名衝突與死路陷阱
    public void CalculateFlowField(Vector3Int playerGridPos)
    {
        // 1. 安全分流過濾：如果當前已經被鍵盤切換成「純 A* 模式」，背景直接跳出不計算流場！
        if (mapGen != null && mapGen.currentPathMode == MapGenerator.PathfindingMode.PureAStar)
        {
            PerformanceTracker perfTracker = GetComponent<PerformanceTracker>();
            if (perfTracker != null) perfTracker.frameFlowFieldTimeMs = 0f;
            return; // 結束方法，不往下跑 Dijkstra 運算
        }

        if (pathGrid.gridNodes.Count == 0) return;

        // 2. 啟動高精度秒錶，掐出全域流場建構的 CPU 真實物理毫秒開銷
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        // 初始化所有網格的積分（改用大整數，方便計算 10 與 14 的物理權重代價）
        foreach (var kvp in pathGrid.gridNodes)
        {
            kvp.Value.integrationCost = kvp.Value.isWall ? 99999 : 65535;
            kvp.Value.flowDirection = Vector3.zero;
        }

        Queue<Node> queue = new Queue<Node>();

        // 設定 5x5 的核心區（引力終點）成本為 0
        for (int x = -2; x <= 2; x++)
        {
            for (int y = -2; y <= 2; y++)
            {
                Vector3Int targetPos = playerGridPos + new Vector3Int(x, y, 0);
                if (pathGrid.gridNodes.ContainsKey(targetPos))
                {
                    Node centerNode = pathGrid.gridNodes[targetPos];
                    if (!centerNode.isWall)
                    {
                        centerNode.integrationCost = 0;
                        queue.Enqueue(centerNode);
                    }
                }
            }
        }

        // 3. Dijkstra 廣度優先：使用 8 向真實物理距離權重擴散
        while (queue.Count > 0)
        {
            Node current = queue.Dequeue();
            List<Node> neighbors = pathGrid.GetNeighbors(current);

            foreach (Node neighbor in neighbors)
            {
                if (neighbor.isWall) continue;

                Vector3Int dir = neighbor.gridPos - current.gridPos;

                // 核心防穿牆：如果是斜對角移動，檢查交叉兩側是否有實體黑牆
                if (dir.x != 0 && dir.y != 0)
                {
                    Vector3Int sideX = current.gridPos + new Vector3Int(dir.x, 0, 0);
                    Vector3Int sideY = current.gridPos + new Vector3Int(0, dir.y, 0);
                    if (pathGrid.gridNodes.ContainsKey(sideX) && pathGrid.gridNodes[sideX].isWall) continue;
                    if (pathGrid.gridNodes.ContainsKey(sideY) && pathGrid.gridNodes[sideY].isWall) continue;
                }

                // 【物理距離代價優化】：直走成本加 10，斜走成本加 14
                int moveCost = (dir.x != 0 && dir.y != 0) ? 14 : 10;
                int newCost = current.integrationCost + moveCost;

                if (newCost < neighbor.integrationCost)
                {
                    neighbor.integrationCost = newCost;
                    queue.Enqueue(neighbor);
                }
            }
        }

        // 4. 根據「最低真實代價」生成完美的避障藍色指向性箭頭
        foreach (var kvp in pathGrid.gridNodes)
        {
            Node current = kvp.Value;
            if (current.isWall || current.integrationCost == 0) continue;

            int minCost = current.integrationCost;
            Vector3 bestDir = Vector3.zero;

            List<Node> neighbors = pathGrid.GetNeighbors(current);
            foreach (Node neighbor in neighbors)
            {
                if (neighbor.isWall) continue;

                Vector3Int dir = neighbor.gridPos - current.gridPos;

                // 向量生成也加上斜向穿牆過濾
                if (dir.x != 0 && dir.y != 0)
                {
                    Vector3Int sideX = current.gridPos + new Vector3Int(dir.x, 0, 0);
                    Vector3Int sideY = current.gridPos + new Vector3Int(0, dir.y, 0);
                    if (pathGrid.gridNodes.ContainsKey(sideX) && pathGrid.gridNodes[sideX].isWall) continue;
                    if (pathGrid.gridNodes.ContainsKey(sideY) && pathGrid.gridNodes[sideY].isWall) continue;
                }

                if (neighbor.integrationCost < minCost)
                {
                    minCost = neighbor.integrationCost;
                    bestDir = new Vector3(dir.x, dir.y, 0);
                }
            }
            current.flowDirection = bestDir.normalized;
        }

        // 在 Game 視窗同步繪製或更新實體箭頭線條
        CreateOrUpdateArrowLines();

        // 5. 停止秒錶計時，並把真實物理消耗的毫秒數傳給全域效能追蹤器
        sw.Stop();
        PerformanceTracker tracker = GetComponent<PerformanceTracker>();
        if (tracker != null)
        {
            tracker.frameFlowFieldTimeMs = (float)sw.Elapsed.TotalMilliseconds;
        }
    }



    // 修正版：讓綠色框框不再死黏著玩家，而是鎖定在流場的核心錨點上！
    void UpdateGameVisuals()
    {
        if (mapGen == null || mapGen.tilemap == null || mapGen.playerTrans == null) return;

        // 【關鍵優化】：我們改去讀取 MapGenerator 正在使用的 lastPlayerGridPos（也就是他扣在場上的那個 5x5 流場中心原點）
        // 為了讓這兩個腳本順暢溝通，我們等一下需要去幫 MapGenerator 的 lastPlayerGridPos 改成公開變數
        Vector3Int centerGridPos = mapGen.GetLastPlayerGridPos();

        Vector3 centerWorldPos = mapGen.tilemap.GetCellCenterWorld(centerGridPos);
        float hx = 2.5f * mapGen.tilemap.cellSize.x;
        float hy = 2.5f * mapGen.tilemap.cellSize.y;

        greenBoxLine.SetPosition(0, centerWorldPos + new Vector3(-hx, -hy, 0));
        greenBoxLine.SetPosition(1, centerWorldPos + new Vector3(hx, -hy, 0));
        greenBoxLine.SetPosition(2, centerWorldPos + new Vector3(hx, hy, 0));
        greenBoxLine.SetPosition(3, centerWorldPos + new Vector3(-hx, hy, 0));
        greenBoxLine.SetPosition(4, centerWorldPos + new Vector3(-hx, -hy, 0));
    }

    // 修正版：手寫 3 頂點折返演算法，在 2D 平面畫出真正的指向性箭頭
    void CreateOrUpdateArrowLines()
    {
        int arrowIndex = 0;

        foreach (var kvp in pathGrid.gridNodes)
        {
            Node currentNode = kvp.Value;
            // 牆壁，或是 5x5 的核心內部，不畫箭頭
            if (currentNode.isWall || currentNode.integrationCost == 0) continue;
            if (currentNode.flowDirection == Vector3.zero) continue;

            Vector3 cellCenter = mapGen.tilemap.GetCellCenterWorld(currentNode.gridPos);

            if (arrowIndex >= arrowLines.Count)
            {
                GameObject arrowObj = new GameObject("GameArrow_" + arrowIndex);
                arrowObj.transform.SetParent(visualContainer.transform);
                LineRenderer lr = arrowObj.AddComponent<LineRenderer>();
                ConfigureLine(lr, Color.blue, 0.04f);

                // 【箭頭進化】：改為 3 個頂點，用來畫出折返的箭頭尖端
                lr.positionCount = 3;
                arrowLines.Add(lr);
            }

            LineRenderer currLine = arrowLines[arrowIndex];
            currLine.gameObject.SetActive(true);

            // 算箭頭的起點與終點
            Vector3 startPos = cellCenter - currentNode.flowDirection * 0.15f;
            Vector3 endPos = cellCenter + currentNode.flowDirection * 0.2f;

            // 計算箭頭尖端兩側的斜翼向量 (旋轉 135 度與 -135 度)
            Vector3 dir = currentNode.flowDirection;
            Vector3 rightWing = Quaternion.Euler(0, 0, 135) * dir * 0.12f;

            // 頂點配置：起點 -> 終點（尖端） -> 折返到某一側的翅膀
            // 這樣在畫面上看起來就會是一個非常清晰的帶柄單邊箭頭！
            currLine.SetPosition(0, startPos);
            currLine.SetPosition(1, endPos);
            currLine.SetPosition(2, endPos + rightWing);

            arrowIndex++;
        }

        for (int i = arrowIndex; i < arrowLines.Count; i++)
        {
            arrowLines[i].gameObject.SetActive(false);
        }
    }

    void ConfigureLine(LineRenderer lr, Color color, float width)
    {
        lr.useWorldSpace = true;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.startColor = color;
        lr.endColor = color;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.sortingOrder = 5;
    }
}

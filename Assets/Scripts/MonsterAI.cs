using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Diagnostics; // 🚀 新增：引入高精度計時器命名空間

public class MonsterAI : MonoBehaviour
{
    public float moveSpeed = 3f;

    private MapGenerator mapGen;
    private Tilemap tilemap;
    private AStarPathfinding aStar;
    private LineRenderer lineRenderer;
    private bool isCurrentlyInAStar = false;

    [Header("A* 路徑記憶優化")]
    public int pathMemoryFrames = 20;      // 成功觸發 A* 後，強制維持 A* 的最小幀數 (約0.3秒)
    private int currentMemoryTimer = 0;    // 記憶倒數計時器

    void Start()
    {
        mapGen = FindObjectOfType<MapGenerator>();
        tilemap = mapGen.tilemap;
        aStar = FindObjectOfType<AStarPathfinding>();

        // 自動動態配置與初始化 Line Renderer，用來在 Game 畫面畫出 A* 黃色軌跡線
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = 0.08f;
        lineRenderer.endWidth = 0.08f;
        lineRenderer.startColor = Color.yellow;
        lineRenderer.endColor = Color.yellow;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.sortingOrder = 6; // 確保黃線比藍色箭頭更高層
    }

    void Update()
    {
        // 1. 安全檢查與狀態連動
        if (mapGen.currentState != MapGenerator.GameState.Play || mapGen.playerTrans == null)
        {
            if (lineRenderer.positionCount > 0) lineRenderer.positionCount = 0;
            return;
        }

        // 🚀 核心優化：如果當前已經被鍵盤切換成「純流場模式」，每幀直接強制清空這隻怪物的 A* 黃色折線，杜絕視覺殘留
        if (mapGen.currentPathMode == MapGenerator.PathfindingMode.PureFlowField)
        {
            if (lineRenderer.positionCount > 0)
            {
                lineRenderer.positionCount = 0;
            }
        }


        Vector3Int myGridPos = tilemap.WorldToCell(transform.position);
        Vector3Int centerGridPos = mapGen.GetLastPlayerGridPos();
        Vector3Int playerGridPos = tilemap.WorldToCell(mapGen.playerTrans.position);

        int deltaX = Mathf.Abs(myGridPos.x - centerGridPos.x);
        int deltaY = Mathf.Abs(myGridPos.y - centerGridPos.y);

        // 2. 演算法模式分流機制
        bool shouldBeInAStar = false;

        if (mapGen.currentPathMode == MapGenerator.PathfindingMode.PureAStar)
        {
            shouldBeInAStar = true;
        }
        else if (mapGen.currentPathMode == MapGenerator.PathfindingMode.PureFlowField)
        {
            shouldBeInAStar = false;
        }
        else
        {
            if (currentMemoryTimer > 0) currentMemoryTimer--;

            if (isCurrentlyInAStar)
            {
                if (currentMemoryTimer > 0) shouldBeInAStar = true;
                else if (deltaX <= 3 && deltaY <= 3) shouldBeInAStar = true;
            }
            else
            {
                if (deltaX <= 2 && deltaY <= 2)
                {
                    shouldBeInAStar = true;
                    currentMemoryTimer = pathMemoryFrames;
                }
            }
        }

        isCurrentlyInAStar = shouldBeInAStar;

        // 3. 依據分流結果，計算尋路前前進方向
        Vector3 moveDirection = Vector3.zero;

        if (!shouldBeInAStar)
        {
            // 【流場模式】：強制將 LineRenderer 頂點數清空
            if (lineRenderer.positionCount > 0)
            {
                lineRenderer.positionCount = 0;
            }
            moveDirection = GetSmoothedFlowDirection();
        }
        else
        {
            // 🚀 【A* 精確模式】：啟動高精度秒錶掐出這隻怪物這幀搜尋的真實 CPU 時間
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            List<Vector3> path = aStar.FindAStarPath(myGridPos, playerGridPos);

            sw.Stop();

            // 將真實時間回報給全域追蹤器
            PerformanceTracker tracker = mapGen.GetComponent<PerformanceTracker>();
            if (tracker != null)
            {
                tracker.frameAStarTimeMs += (float)sw.Elapsed.TotalMilliseconds;
            }

            if (path != null && path.Count > 0)
            {
                moveDirection = (path[0] - transform.position).normalized;

                // 即時繪製軌跡
                lineRenderer.positionCount = path.Count + 1;
                lineRenderer.SetPosition(0, transform.position);
                for (int i = 0; i < path.Count; i++)
                {
                    lineRenderer.SetPosition(i + 1, path[i]);
                }

                // 框外繞牆續航保護
                if (deltaX > 2 || deltaY > 2)
                {
                    currentMemoryTimer = Mathf.Max(currentMemoryTimer, 10);
                }
            }
            else
            {
                if (lineRenderer.positionCount > 0) lineRenderer.positionCount = 0;
                isCurrentlyInAStar = false;
                currentMemoryTimer = 0;
                moveDirection = GetSmoothedFlowDirection();
            }
        }

        // 4. 疊加手寫局部避障力（閃避同伴與黑牆雷達）
        moveDirection += CalculateSeparationAndWallAvoidance(myGridPos);

        // 5. 預測性夾鉗防護（徹底杜絕高密度推擠穿牆 Bug）
        if (moveDirection != Vector3.zero)
        {
            Vector3 normalizedDir = moveDirection.normalized;
            Vector3 deltaMove = normalizedDir * moveSpeed * Time.deltaTime;
            Vector3 predictedWorldPos = transform.position + deltaMove;

            Vector3Int predictedGridX = tilemap.WorldToCell(new Vector3(predictedWorldPos.x, transform.position.y, 0));
            Vector3Int predictedGridY = tilemap.WorldToCell(new Vector3(transform.position.x, predictedWorldPos.y, 0));

            PathfindingGrid pathGrid = mapGen.GetComponent<PathfindingGrid>();

            if (pathGrid.gridNodes.ContainsKey(predictedGridX) && pathGrid.gridNodes[predictedGridX].isWall)
            {
                normalizedDir.x = 0;
            }
            if (pathGrid.gridNodes.ContainsKey(predictedGridY) && pathGrid.gridNodes[predictedGridY].isWall)
            {
                normalizedDir.y = 0;
            }

            if (normalizedDir != Vector3.zero)
            {
                transform.Translate(normalizedDir.normalized * moveSpeed * Time.deltaTime, Space.World);
            }
        }
    }

    // 雙線性網格向量混合平滑化演算法
    Vector3 GetSmoothedFlowDirection()
    {
        PathfindingGrid pathGrid = mapGen.GetComponent<PathfindingGrid>();
        Vector3 cellCenter = tilemap.GetCellCenterWorld(tilemap.WorldToCell(transform.position));
        float localX = transform.position.x - cellCenter.x;
        float localY = transform.position.y - cellCenter.y;

        Vector3Int cell00 = tilemap.WorldToCell(transform.position);
        Vector3Int cell10 = cell00 + new Vector3Int(localX > 0 ? 1 : -1, 0, 0);
        Vector3Int cell01 = cell00 + new Vector3Int(0, localY > 0 ? 1 : -1, 0);
        Vector3Int cell11 = cell00 + new Vector3Int(localX > 0 ? 1 : -1, localY > 0 ? 1 : -1, 0);

        Vector3 v00 = pathGrid.gridNodes.ContainsKey(cell00) ? pathGrid.gridNodes[cell00].flowDirection : Vector3.zero;
        Vector3 v10 = pathGrid.gridNodes.ContainsKey(cell10) ? pathGrid.gridNodes[cell10].flowDirection : Vector3.zero;
        Vector3 v01 = pathGrid.gridNodes.ContainsKey(cell01) ? pathGrid.gridNodes[cell01].flowDirection : Vector3.zero;
        Vector3 v11 = pathGrid.gridNodes.ContainsKey(cell11) ? pathGrid.gridNodes[cell11].flowDirection : Vector3.zero;

        float tX = Mathf.Abs(localX) / tilemap.cellSize.x;
        float tY = Mathf.Abs(localY) / tilemap.cellSize.y;

        Vector3 vX1 = Vector3.Lerp(v00, v10, tX);
        Vector3 vX2 = Vector3.Lerp(v01, v11, tX);
        Vector3 blendedVector = Vector3.Lerp(vX1, vX2, tY);

        if (blendedVector.magnitude < 0.1f) return v00;
        return blendedVector.normalized;
    }

    // 局部避障：閃避同伴與黑牆轉角
    Vector3 CalculateSeparationAndWallAvoidance(Vector3Int myGridPos)
    {
        Vector3 avoidance = Vector3.zero;

        Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, 0.45f);
        foreach (var col in nearby)
        {
            if (col.gameObject != this.gameObject && col.CompareTag("Monster"))
            {
                avoidance += (transform.position - col.transform.position).normalized * 0.5f;
            }
        }

        PathfindingGrid pathGrid = mapGen.GetComponent<PathfindingGrid>();
        Vector3Int[] checkDirs = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right,
                                   new Vector3Int(1,1,0), new Vector3Int(-1,1,0), new Vector3Int(1,-1,0), new Vector3Int(-1,-1,0) };

        foreach (var dir in checkDirs)
        {
            Vector3Int neighborGrid = myGridPos + dir;
            if (pathGrid.gridNodes.ContainsKey(neighborGrid) && pathGrid.gridNodes[neighborGrid].isWall)
            {
                Vector3 wallWorldPos = pathGrid.gridNodes[neighborGrid].worldPos;
                float distToWall = Vector3.Distance(transform.position, wallWorldPos);

                if (distToWall < 0.7f)
                {
                    avoidance += (transform.position - wallWorldPos).normalized * (0.7f - distToWall) * 1.5f;
                }
            }
        }

        return avoidance;
    }
}

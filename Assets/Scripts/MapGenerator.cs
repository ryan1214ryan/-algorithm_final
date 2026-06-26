using UnityEngine;
using UnityEngine.Tilemaps;

public class MapGenerator : MonoBehaviour
{
    public enum GameState { Play, EditWall, SpawnMonster, DeleteMonster, PerformanceView }
    public enum PathfindingMode { HybridLOD, PureAStar, PureFlowField }

    [Header("地圖與狀態設定")]
    public int width = 47;
    public int height = 20;
    public GameState currentState = GameState.Play;
    public PathfindingMode currentPathMode = PathfindingMode.HybridLOD;

    [Header("組件與資源")]
    public Tilemap tilemap;
    public TileBase floorTile;
    public TileBase wallTile;
    public GameObject monsterPrefab;
    public Transform playerTrans;

    [Header("效能觀察鏡頭設定")]
    public Camera mainCamera;
    public Vector3 playCameraPos = new Vector3(0, 0, -10);
    public Vector3 perfCameraPos = new Vector3(60, 0, -10); // 鏡頭移到右邊看圖表

    private PathfindingGrid pathGrid;
    private Vector3Int lastPlayerGridPos;
    private PerformanceTracker perfTracker;

    void Start()
    {
        if (tilemap == null) tilemap = FindObjectOfType<Tilemap>();
        if (mainCamera == null) mainCamera = Camera.main;
        pathGrid = GetComponent<PathfindingGrid>();
        perfTracker = GetComponent<PerformanceTracker>();

        GenerateBaseMap();
        pathGrid.InitializeSearchGrid();

        if (playerTrans != null)
        {
            lastPlayerGridPos = tilemap.WorldToCell(playerTrans.position);
        }
        ForceTriggerFlowField();
    }

    void Update()
    {
        HandleModeSwitching();

        // 根據不同狀態執行滑鼠操作
        if (currentState == GameState.EditWall)
        {
            HandleMapEditing();
        }
        else if (currentState == GameState.SpawnMonster)
        {
            HandleMonsterSpawning();
        }
        else if (currentState == GameState.DeleteMonster)
        {
            HandleMonsterDeletion();
        }
        else if (currentState == GameState.Play)
        {
            CheckPlayerRegion();
        }
    }

    void HandleModeSwitching()
    {
        // 1. 基礎運行與編輯模式切換
        if (Input.GetKeyDown(KeyCode.Alpha1)) // 1: 遊玩
        {
            currentState = GameState.Play;
            Time.timeScale = 1f;
            mainCamera.transform.position = playCameraPos;
            pathGrid.InitializeSearchGrid();
            ForceTriggerFlowField();
            Debug.Log("【模式】回到遊玩模式");
        }
        if (Input.GetKeyDown(KeyCode.Alpha2)) // 2: 畫牆
        {
            currentState = GameState.EditWall;
            Time.timeScale = 0f;
            mainCamera.transform.position = playCameraPos;
            Debug.Log("【模式】繪製牆壁模式（左鍵牆，右鍵地板）");
        }
        if (Input.GetKeyDown(KeyCode.Alpha3)) // 3: 放怪
        {
            currentState = GameState.SpawnMonster;
            Time.timeScale = 0f;
            mainCamera.transform.position = playCameraPos;
            Debug.Log("【模式】放置怪物模式（左鍵生成）");
        }
        if (Input.GetKeyDown(KeyCode.Alpha4)) // 4: 刪怪
        {
            currentState = GameState.DeleteMonster;
            Time.timeScale = 0f;
            mainCamera.transform.position = playCameraPos;
            Debug.Log("【模式】刪除怪物模式（左鍵點擊怪物刪除）");
        }

        // 2. P 鍵：切換到鏡頭外的效能統計圖表區（暫停運行，保持圖表）
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (currentState != GameState.PerformanceView)
            {
                currentState = GameState.PerformanceView;
                Time.timeScale = 0f; // 暫停遊戲
                mainCamera.transform.position = perfCameraPos; // 鏡頭切過去
                Debug.Log("【模式】切換至效能圖表觀測區（遊戲已暫停）");
            }
            else
            {
                // 再按一次 P 鍵自動回到遊玩模式
                currentState = GameState.Play;
                Time.timeScale = 1f;
                mainCamera.transform.position = playCameraPos;
            }
        }

        // 3. TAB 鍵：實時切換「演算法模式」（不暫停，實時切換循環）
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            int nextMode = ((int)currentPathMode + 1) % 3;
            currentPathMode = (PathfindingMode)nextMode;

            // 強制讓所有怪物和流場依據新演算法刷新一次
            ForceTriggerFlowField();
            if (perfTracker != null) perfTracker.ResetModeText();

            Debug.Log("【演算法切換】當前導航核心： " + currentPathMode);
        }
    }

    void CheckPlayerRegion()
    {
        if (playerTrans == null) return;
        // 如果是純 A* 模式，不需要因為玩家出框而重算流場
        if (currentPathMode == PathfindingMode.PureAStar) return;

        Vector3Int currentPlayerGridPos = tilemap.WorldToCell(playerTrans.position);
        int deltaX = Mathf.Abs(currentPlayerGridPos.x - lastPlayerGridPos.x);
        int deltaY = Mathf.Abs(currentPlayerGridPos.y - lastPlayerGridPos.y);

        if (deltaX > 2 || deltaY > 2)
        {
            lastPlayerGridPos = currentPlayerGridPos;
            GetComponent<GlobalFlowField>().CalculateFlowField(currentPlayerGridPos);
        }
    }

    public void ForceTriggerFlowField()
    {
        if (playerTrans == null) return;
        Vector3Int currentPlayerGridPos = tilemap.WorldToCell(playerTrans.position);
        lastPlayerGridPos = currentPlayerGridPos;
        GetComponent<GlobalFlowField>().CalculateFlowField(currentPlayerGridPos);
        GetComponent<GlobalFlowField>().NotifyFlowFieldUpdated();
    }

    public Vector3Int GetLastPlayerGridPos() { return lastPlayerGridPos; }

    void HandleMapEditing()
    {
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector3Int gridPos = tilemap.WorldToCell(mouseWorldPos);
        int halfWidth = width / 2; int halfHeight = height / 2;
        if (gridPos.x > -halfWidth && gridPos.x < halfWidth && gridPos.y > -halfHeight && gridPos.y < halfHeight)
        {
            if (Input.GetMouseButton(0)) tilemap.SetTile(gridPos, wallTile);
            if (Input.GetMouseButton(1)) tilemap.SetTile(gridPos, floorTile);
        }
    }

    void HandleMonsterSpawning()
    {
        if (Input.GetMouseButtonDown(0) && monsterPrefab != null)
        {
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0f;
            GameObject newMonster = Instantiate(monsterPrefab, mouseWorldPos, Quaternion.identity);
            newMonster.tag = "Monster";
        }
    }

    // 新增：滑鼠點擊刪除敵人
    void HandleMonsterDeletion()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mousePos2D = new Vector2(mouseWorldPos.x, mouseWorldPos.y);

            // 發射一條 2D 射線探測點擊到的怪物
            RaycastHit2D hit = Physics2D.Raycast(mousePos2D, Vector2.zero);
            if (hit.collider != null && hit.collider.CompareTag("Monster"))
            {
                Destroy(hit.collider.gameObject);
                Debug.Log("【刪除】成功移除一隻敵人");
            }
        }
    }

    void GenerateBaseMap()
    {
        if (tilemap == null || floorTile == null || wallTile == null) return;
        tilemap.ClearAllTiles();
        int halfWidth = width / 2; int halfHeight = height / 2;
        for (int x = -halfWidth; x <= halfWidth; x++)
        {
            for (int y = -halfHeight; y <= halfHeight; y++)
            {
                if (x == -halfWidth || x == halfWidth || y == -halfHeight || y == halfHeight)
                    tilemap.SetTile(new Vector3Int(x, y, 0), wallTile);
                else
                    tilemap.SetTile(new Vector3Int(x, y, 0), floorTile);
            }
        }
    }
}

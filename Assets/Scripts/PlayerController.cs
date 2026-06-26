using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Tilemap tilemap;
    private MapGenerator mapGen;
    private PathfindingGrid pathGrid;

    private bool isMoving = false;
    private Vector3 targetPos;

    void Start()
    {
        mapGen = FindObjectOfType<MapGenerator>();
        tilemap = mapGen.tilemap;
        pathGrid = FindObjectOfType<PathfindingGrid>();

        Vector3Int currentGridPos = tilemap.WorldToCell(transform.position);
        transform.position = tilemap.GetCellCenterWorld(currentGridPos);
    }

    void Update()
    {
        if (mapGen.currentState != MapGenerator.GameState.Play) return;

        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            if (transform.position == targetPos)
            {
                isMoving = false;
            }
            return;
        }

        // 讀取八向鍵盤輸入
        int inputX = 0;
        int inputY = 0;

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inputX = 1;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) inputX = -1;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) inputY = 1;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) inputY = -1;

        // 只要有任何方向輸入，就嘗試移動
        if (inputX != 0 || inputY != 0)
        {
            TryMove(new Vector3Int(inputX, inputY, 0));
        }
    }

    void TryMove(Vector3Int direction)
    {
        Vector3Int currentGridPos = tilemap.WorldToCell(transform.position);
        Vector3Int nextGridPos = currentGridPos + direction;

        // 1. 基本檢查：下一格不能超出地圖字典或直接是牆壁
        if (!pathGrid.gridNodes.ContainsKey(nextGridPos) || pathGrid.gridNodes[nextGridPos].isWall) return;

        // 2. 核心技術：斜角穿牆防護
        // 當走斜角（如右上X=1, Y=1）時，必須確保正上和正右不能同時是牆，否則會產生「卡進轉角」的 Bug
        if (direction.x != 0 && direction.y != 0)
        {
            Vector3Int sideGridX = currentGridPos + new Vector3Int(direction.x, 0, 0);
            Vector3Int sideGridY = currentGridPos + new Vector3Int(0, direction.y, 0);

            // 如果兩側都是牆，或者是任何一側阻擋，禁止切斜角過來
            if (pathGrid.gridNodes.ContainsKey(sideGridX) && pathGrid.gridNodes[sideGridX].isWall) return;
            if (pathGrid.gridNodes.ContainsKey(sideGridY) && pathGrid.gridNodes[sideGridY].isWall) return;
        }

        // 條件通過，執行移動
        targetPos = tilemap.GetCellCenterWorld(nextGridPos);
        isMoving = true;
    }
}

using System.Collections.Generic;
using UnityEngine;

public class PerformanceTracker : MonoBehaviour
{
    private MapGenerator mapGen;

    // 用於背景收集每幀真實數據的暫存變數
    [HideInInspector] public float frameAStarTimeMs = 0f;
    [HideInInspector] public float frameFlowFieldTimeMs = 0f;

    [Header("平滑化設定")]
    public int averageWindowSize = 10; // 計算最近 10 幀的平均值

    // 歷史數據緩衝佇列
    private Queue<float> aStarHistory = new Queue<float>();
    private Queue<float> flowFieldHistory = new Queue<float>();

    // 最終顯示在 GUI 面板上的平滑平均值
    private float displayAStarAvg = 0f;
    private float displayFlowFieldAvg = 0f;
    private float displayTotalAvg = 0f;

    private string modeName = "混合層級巡路 (LOD)";

    void Start()
    {
        mapGen = GetComponent<MapGenerator>();
        ResetModeText();
    }

    void Update()
    {
        if (mapGen.currentState == MapGenerator.GameState.Play)
        {
            // 1. 將本幀收集到的真實物理開銷推入歷史紀錄
            aStarHistory.Enqueue(frameAStarTimeMs);
            flowFieldHistory.Enqueue(frameFlowFieldTimeMs);

            // 2. 如果維持的歷史資料超過 10 幀，把最舊的剔除
            if (aStarHistory.Count > averageWindowSize) aStarHistory.Dequeue();
            if (flowFieldHistory.Count > averageWindowSize) flowFieldHistory.Dequeue();

            // 3. 計算 10 幀內 A* 的移動平均值
            float sumAStar = 0f;
            foreach (float val in aStarHistory) sumAStar += val;
            displayAStarAvg = sumAStar / aStarHistory.Count;

            // 4. 計算 10 幀內流場的移動平均值
            float sumFlowField = 0f;
            foreach (float val in flowFieldHistory) sumFlowField += val;
            displayFlowFieldAvg = sumFlowField / flowFieldHistory.Count;

            // 5. 總開銷加總
            displayTotalAvg = displayAStarAvg + displayFlowFieldAvg;

            // 【重置計時器】每幀結束時清空單幀暫存，等待下一幀怪物與流場重新回報
            frameAStarTimeMs = 0f;
        }
    }

    public void ResetModeText()
    {
        if (mapGen == null) return;
        if (mapGen.currentPathMode == MapGenerator.PathfindingMode.HybridLOD) modeName = "混合層級巡路 (Hybrid LOD)";
        if (mapGen.currentPathMode == MapGenerator.PathfindingMode.PureAStar) modeName = "傳統純 A* 巡路 (Pure A*)";
        if (mapGen.currentPathMode == MapGenerator.PathfindingMode.PureFlowField) modeName = "全域純流場巡路 (Pure Flow Field)";
    }

    // 當按下 P 鍵時，在鏡頭中央渲染「10幀移動平均」的真實效能圖表
    void OnGUI()
    {
        if (mapGen != null && mapGen.currentState == MapGenerator.GameState.PerformanceView)
        {
            int totalMonsters = GameObject.FindGameObjectsWithTag("Monster").Length;

            GUI.Box(new Rect(Screen.width / 2 - 250, Screen.height / 2 - 200, 500, 400), "📊 尋路演算法 實時平滑物理效能面板");

            GUI.Label(new Rect(Screen.width / 2 - 220, Screen.height / 2 - 160, 400, 30), "<b>當前導航核心：</b> " + modeName);
            GUI.Label(new Rect(Screen.width / 2 - 220, Screen.height / 2 - 130, 400, 30), "<b>場景當前敵方總數 (N)：</b> " + totalMonsters + " 隻");
            GUI.Label(new Rect(Screen.width / 2 - 220, Screen.height / 2 - 100, 400, 30), "<b>CPU 平均尋路總耗時 (10幀平均)：</b> " + displayTotalAvg.ToString("F4") + " ms / 幀");

            // 長條圖 1：A* 平均開銷 (放大 50 倍)
            GUI.Label(new Rect(Screen.width / 2 - 220, Screen.height / 2 - 40, 150, 30), "A* 節點展開平均能耗:");
            GUI.color = Color.yellow;
            GUI.HorizontalSlider(new Rect(Screen.width / 2 - 70, Screen.height / 2 - 35, Mathf.Min(displayAStarAvg * 50f, 280), 30), displayAStarAvg, 0, 10);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width / 2 + 220, Screen.height / 2 - 40, 80, 30), displayAStarAvg.ToString("F4") + "ms");

            // 長條圖 2：流場平均開銷
            GUI.Label(new Rect(Screen.width / 2 - 220, Screen.height / 2, 150, 30), "流場全域建構平均能耗:");
            GUI.color = Color.cyan;
            GUI.HorizontalSlider(new Rect(Screen.width / 2 - 70, Screen.height / 2 + 5, Mathf.Min(displayFlowFieldAvg * 50f, 280), 30), displayFlowFieldAvg, 0, 10);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width / 2 + 220, Screen.height / 2 + 0, 80, 30), displayFlowFieldAvg.ToString("F4") + "ms");

            // 總開銷平均警示條
            GUI.Label(new Rect(Screen.width / 2 - 220, Screen.height / 2 + 60, 150, 30), "<b>CPU 總體平均物理能耗:</b>");
            GUI.color = displayTotalAvg > 4f ? Color.red : Color.green;
            GUI.HorizontalSlider(new Rect(Screen.width / 2 - 70, Screen.height / 2 + 65, Mathf.Min(displayTotalAvg * 50f, 280), 30), displayTotalAvg, 0, 10);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width / 2 + 220, Screen.height / 2 + 60, 80, 30), displayTotalAvg.ToString("F4") + "ms");

            GUI.Label(new Rect(Screen.width / 2 - 200, Screen.height / 2 + 140, 400, 30), "<i>提示：在主畫面按住 [TAB] 鍵可實時切換核心，再按一次 [P] 鍵可返回戰局。</i>");
        }
    }
}

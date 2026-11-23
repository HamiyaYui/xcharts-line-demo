using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using XCharts.Runtime;

public class LineChartController : MonoBehaviour
{
    [Header("数据文件（位于 StreamingAssets 目录）")]
    public string dataFileName = "LineData.json";

    [Header("界面参考分辨率")]
    public Vector2 referenceResolution = new Vector2(1600, 900);

    [Header("图表卡片外边距（像素）")]
    public Vector2 chartMargin = new Vector2(40, 40);

    [Header("实时更新")]
    [Tooltip("是否启用模拟实时更新（也可通过 URL 参数 ?autoupdate=0 关闭）")]
    public bool enableRealtimeUpdate = true;
    [Tooltip("实时更新间隔（秒）")]
    public float realtimeInterval = 2f;

    private const string Serie1Name = "销售额";
    private const string Serie2Name = "目标值";

    private static readonly Color32 Serie1Color = new Color32(59, 130, 246, 255);
    private static readonly Color32 Serie2Color = new Color32(245, 158, 11, 255);
    private static readonly Color32 TextPrimary = new Color32(17, 24, 39, 255);
    private static readonly Color32 TextSecondary = new Color32(107, 114, 128, 255);

    private LineChart chart;
    private Text statusText;
    private Font uiFont;

    private float[] baseY1 = new float[0];
    private float[] baseY2 = new float[0];
    private float[] currentY1 = new float[0];
    private float[] currentY2 = new float[0];
    private int updateTick;
    private bool realtimeEnabled;

    private void Start()
    {
        ParseUrlOptions();
        BuildUI();
        StartCoroutine(LoadAndRender());
    }

    private void Update()
    {

        if (Input.GetKeyDown(KeyCode.R))
        {
            Reload();
        }
    }

    public void Reload()
    {
        StopAllCoroutines();
        updateTick = 0;
        StartCoroutine(LoadAndRender());
    }

    private void ParseUrlOptions()
    {

        realtimeEnabled = enableRealtimeUpdate;
        string url = Application.absoluteURL;
        if (!string.IsNullOrEmpty(url) && url.Contains("autoupdate=0"))
        {
            realtimeEnabled = false;
        }
    }

    #region UI 构建

    private void BuildUI()
    {
        uiFont = Resources.Load<Font>("Fonts/NotoSansSC");
        if (uiFont == null)
        {
            Debug.LogWarning("[LineChart] 未找到 Resources/Fonts/NotoSansSC 字体，中文可能无法显示。");
        }

        EnsureEventSystem();
        EnsureCamera();

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var chartGo = new GameObject("LineChart", typeof(RectTransform));
        chartGo.transform.SetParent(canvasGo.transform, false);
        var chartRect = chartGo.GetComponent<RectTransform>();

        chartRect.anchorMin = new Vector2(0f, 0f);
        chartRect.anchorMax = new Vector2(0f, 0f);
        chartRect.pivot = new Vector2(0f, 0f);
        chartRect.sizeDelta = new Vector2(
            referenceResolution.x - chartMargin.x * 2f,
            referenceResolution.y - chartMargin.y * 2f);
        chartRect.anchoredPosition = new Vector2(chartMargin.x, chartMargin.y);

        chart = chartGo.AddComponent<LineChart>();
        Canvas.ForceUpdateCanvases();
        chart.Init();
        ConfigureChart();

        CreateRefreshButton(chartRect);
        CreateStatusText(canvasGo.transform);

        SetStatus("正在读取 StreamingAssets/" + dataFileName + " …");
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        go.transform.SetParent(transform, false);
    }

    private void EnsureCamera()
    {
        if (Camera.main != null) return;

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(244, 246, 251, 255);
        cam.orthographic = true;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        camGo.transform.SetParent(transform, false);
    }

    private void CreateRefreshButton(RectTransform chartRect)
    {
        var btnGo = new GameObject("RefreshButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(chartRect, false);

        var rect = btnGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(112f, 34f);
        rect.anchoredPosition = new Vector2(-24f, -14f);

        var image = btnGo.GetComponent<Image>();
        image.color = new Color32(37, 99, 235, 255);

        var button = btnGo.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(Reload);

        var label = CreateText(btnGo.transform, "Text", "刷新数据", 15, Color.white, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform);
    }

    private void CreateStatusText(Transform parent)
    {
        statusText = CreateText(parent, "Status", string.Empty, 14, TextSecondary, TextAnchor.MiddleLeft);
        var rect = statusText.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.offsetMin = new Vector2(42f, 10f);
        rect.offsetMax = new Vector2(-42f, 34f);
    }

    private Text CreateText(Transform parent, string name, string content, int fontSize, Color color, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var text = go.GetComponent<Text>();
        text.font = uiFont;
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    #endregion

    #region XCharts 配置

    private void ConfigureChart()
    {

        if (uiFont != null && chart.theme != null && chart.theme.sharedTheme != null)
        {
            chart.theme.sharedTheme.font = uiFont;
        }

        var title = chart.EnsureChartComponent<Title>();
        title.show = true;
        title.text = "销售额与目标值趋势（1—6月）";
        title.labelStyle.textStyle.fontSize = 26;
        title.labelStyle.textStyle.fontStyle = FontStyle.Bold;
        title.labelStyle.textStyle.color = TextPrimary;
        title.location.align = Location.Align.TopLeft;
        title.location.left = 28f;
        title.location.top = 12f;

        var legend = chart.EnsureChartComponent<Legend>();
        legend.show = true;
        legend.location.align = Location.Align.TopCenter;
        legend.location.top = 62f;
        legend.itemWidth = 22f;
        legend.itemHeight = 12f;
        legend.itemGap = 36f;
        legend.labelStyle.textStyle.fontSize = 16;
        legend.labelStyle.textStyle.color = new Color32(55, 65, 81, 255);

        var tooltip = chart.EnsureChartComponent<Tooltip>();
        tooltip.show = true;
        tooltip.trigger = Tooltip.Trigger.Axis;
        tooltip.triggerOn = Tooltip.TriggerOn.MouseMove;
        tooltip.numericFormatter = "0.0";

        var grid = chart.EnsureChartComponent<GridCoord>();
        grid.left = 72f;
        grid.right = 48f;
        grid.top = 108f;
        grid.bottom = 58f;

        var xAxis = chart.EnsureChartComponent<XAxis>();
        xAxis.show = true;
        xAxis.boundaryGap = false;
        xAxis.axisName.show = true;
        xAxis.axisName.name = "月份";
        xAxis.axisName.labelStyle.textStyle.fontSize = 16;
        xAxis.axisLabel.show = true;
        xAxis.axisLabel.interval = 0;
        xAxis.axisLabel.textStyle.fontSize = 16;
        xAxis.axisLabel.textStyle.color = new Color32(75, 85, 99, 255);
        xAxis.splitLine.show = false;

        var yAxis = chart.EnsureChartComponent<YAxis>();
        yAxis.show = true;
        yAxis.minMaxType = Axis.AxisMinMaxType.MinMaxAuto;
        yAxis.axisName.show = true;
        yAxis.axisName.name = "数值";
        yAxis.axisName.labelStyle.textStyle.fontSize = 16;
        yAxis.axisLabel.show = true;
        yAxis.axisLabel.textStyle.fontSize = 16;
        yAxis.axisLabel.textStyle.color = new Color32(75, 85, 99, 255);
        yAxis.splitLine.show = true;
        yAxis.splitLine.lineStyle.color = new Color32(229, 231, 235, 255);

        chart.RemoveAllSerie();
        var x = chart.GetChartComponent<XAxis>();
        if (x != null) x.data.Clear();
    }

    #endregion

    #region 数据读取与渲染

    private IEnumerator LoadAndRender()
    {
        string path = Path.Combine(Application.streamingAssetsPath, dataFileName);
        using (var request = UnityWebRequest.Get(path))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Fail("读取失败：" + request.error);
                yield break;
            }

            LineDataModel data = null;
            try
            {
                data = JsonUtility.FromJson<LineDataModel>(request.downloadHandler.text);
            }
            catch (Exception e)
            {
                Fail("JSON 解析失败：" + e.Message);
                yield break;
            }

            if (!Validate(data, out string error))
            {
                Fail("数据格式错误：" + error);
                yield break;
            }

            RenderChart(data);

            if (realtimeEnabled)
            {
                SetStatus("已加载 " + data.XAxis.Length + " 个月 × 2 条折线 · 实时更新已启动（每 " + realtimeInterval + " 秒）");
                StartCoroutine(RealtimeUpdateLoop());
            }
            else
            {
                SetStatus("已加载 " + data.XAxis.Length + " 个月 × 2 条折线 · 静态数据（autoupdate=0）");
            }

            Debug.Log("[LineChart] DATA_OK x=" + data.XAxis.Length + " y1=" + data.YValues.Length + " y2=" + data.YValues2.Length);
        }
    }

    private static bool Validate(LineDataModel data, out string error)
    {
        if (data == null) { error = "JSON 内容为空"; return false; }
        if (data.XAxis == null || data.YValues == null || data.YValues2 == null)
        {
            error = "缺少 XAxis / YValues / YValues2 字段";
            return false;
        }
        if (data.XAxis.Length == 0) { error = "XAxis 为空"; return false; }
        if (data.XAxis.Length != data.YValues.Length || data.XAxis.Length != data.YValues2.Length)
        {
            error = "XAxis、YValues、YValues2 长度不一致";
            return false;
        }
        error = null;
        return true;
    }

    private void RenderChart(LineDataModel data)
    {

        chart.RemoveAllSerie();

        var xAxis = chart.GetChartComponent<XAxis>();
        if (xAxis != null) xAxis.data.Clear();

        foreach (var category in data.XAxis)
        {
            chart.AddXAxisData(category);
        }

        var serie1 = chart.AddSerie<Line>(Serie1Name);
        ApplySerieStyle(serie1, Serie1Color);
        foreach (var value in data.YValues) chart.AddData(0, value);

        var serie2 = chart.AddSerie<Line>(Serie2Name);
        ApplySerieStyle(serie2, Serie2Color);
        foreach (var value in data.YValues2) chart.AddData(1, value);

        baseY1 = (float[])data.YValues.Clone();
        baseY2 = (float[])data.YValues2.Clone();
        currentY1 = (float[])data.YValues.Clone();
        currentY2 = (float[])data.YValues2.Clone();
        updateTick = 0;

        chart.RefreshChart();
    }

    private static void ApplySerieStyle(Serie serie, Color32 color)
    {
        if (serie == null) return;

        serie.lineType = LineType.Normal;
        serie.lineStyle.width = 3f;
        serie.lineStyle.color = color;

        serie.itemStyle.color = color;
        serie.itemStyle.borderColor = Color.white;
        serie.itemStyle.borderWidth = 2f;

        serie.symbol.show = true;
        serie.symbol.type = SymbolType.Circle;
        serie.symbol.size = 9f;
    }

    #endregion

    #region 模拟实时更新

    private IEnumerator RealtimeUpdateLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(realtimeInterval);
            if (chart == null || currentY1.Length == 0) continue;
            SimulateRealtimeUpdate();
        }
    }

    private void SimulateRealtimeUpdate()
    {
        updateTick++;

        int index1 = currentY1.Length - 2;
        int index2 = currentY1.Length - 1;

        float phase = updateTick * 0.8f;

        currentY1[index1] = Mathf.Clamp(baseY1[index1] + Mathf.Sin(phase) * 1.4f, 8f, 35f);
        currentY1[index2] = Mathf.Clamp(baseY1[index2] + Mathf.Cos(phase) * 1.4f, 8f, 35f);

        currentY2[index1] = Mathf.Clamp(baseY2[index1] + Mathf.Sin(phase + 1.2f) * 1.1f, 8f, 35f);
        currentY2[index2] = Mathf.Clamp(baseY2[index2] + Mathf.Cos(phase + 1.2f) * 1.1f, 8f, 35f);

        chart.UpdateData(0, index1, currentY1[index1]);
        chart.UpdateData(0, index2, currentY1[index2]);
        chart.UpdateData(1, index1, currentY2[index1]);
        chart.UpdateData(1, index2, currentY2[index2]);

        chart.RefreshChart();

        SetStatus("实时更新中 · 第 " + updateTick + " 次（每 " + realtimeInterval + " 秒同时刷新两条折线）");
        Debug.Log("[LineChart] TICK " + updateTick + " s1_5=" + currentY1[index1].ToString("0.0") + " s1_6=" + currentY1[index2].ToString("0.0")
            + " s2_5=" + currentY2[index1].ToString("0.0") + " s2_6=" + currentY2[index2].ToString("0.0"));
    }

    #endregion

    private void Fail(string message)
    {
        SetStatus(message);
        Debug.LogError("[LineChart] " + message);
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }
}

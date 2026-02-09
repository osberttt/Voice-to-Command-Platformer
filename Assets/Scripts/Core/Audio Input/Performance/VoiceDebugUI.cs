using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Optimized runtime debug UI with latency visualization
/// </summary>
public class VoiceDebugUIOptimized : MonoBehaviour
{
    public VoiceRecognizerOptimized recognizer;
    public AudioInputOptimized audioInput;

    [Header("UI Settings")]
    public bool showVisualFeedback = true;
    public bool showPerformanceStats = true;
    public bool showLatencyGraph = true;
    public float updateRate = 0.05f;

    private Canvas canvas;
    private GameObject debugPanel;
    private TextMeshProUGUI statsText;
    private TextMeshProUGUI stateText;
    private Image visualIndicator;
    private Image energyBar;
    private RawImage latencyGraph;

    private float lastUpdateTime;
    private Color idleColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    private Color activeColor = new Color(0.2f, 0.8f, 0.2f, 0.8f);
    private Color detectedColor = new Color(1f, 0.8f, 0.2f, 1f);
    private Color fastColor = new Color(0.2f, 1f, 0.2f, 1f); // <200ms
    private Color slowColor = new Color(1f, 0.4f, 0.2f, 1f); // >500ms

    private float visualFeedbackTimer;
    private float lastEnergy;
    private float lastLatency;

    // Latency graph
    private Texture2D graphTexture;
    private List<float> latencyHistory = new List<float>();
    private const int GRAPH_WIDTH = 300;
    private const int GRAPH_HEIGHT = 100;
    private const int MAX_LATENCY_SAMPLES = 50;

    // Performance tracking
    private float minLatency = float.MaxValue;
    private float maxLatency = 0f;
    private int fastDetections = 0; // <200ms
    private int totalDetections = 0;

    void Start()
    {
        CreateDebugUI();

        if (recognizer != null)
        {
            recognizer.OnJumpMetrics += OnDetection;
            recognizer.OnTurnMetrics += OnDetection;
        }

        if (audioInput != null)
        {
            audioInput.OnVoiceActivity += OnVoiceActivity;
        }

        if (showLatencyGraph)
        {
            CreateLatencyGraph();
        }
    }

    void OnDisable()
    {
        if (recognizer != null)
        {
            recognizer.OnJumpMetrics -= OnDetection;
            recognizer.OnTurnMetrics -= OnDetection;
        }

        if (audioInput != null)
        {
            audioInput.OnVoiceActivity -= OnVoiceActivity;
        }

        if (graphTexture != null)
        {
            Destroy(graphTexture);
        }
    }

    void CreateDebugUI()
    {
        // Create canvas
        GameObject canvasObj = new GameObject("VoiceDebugCanvas_Optimized");
        canvasObj.transform.SetParent(transform);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        // Create debug panel
        debugPanel = new GameObject("DebugPanel");
        debugPanel.transform.SetParent(canvasObj.transform, false);

        RectTransform panelRect = debugPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(10, -10);
        panelRect.sizeDelta = new Vector2(450, 400);

        Image panelBg = debugPanel.AddComponent<Image>();
        panelBg.color = new Color(0, 0, 0, 0.8f);

        // Stats text
        if (showPerformanceStats)
        {
            GameObject statsObj = new GameObject("StatsText");
            statsObj.transform.SetParent(debugPanel.transform, false);

            RectTransform statsRect = statsObj.AddComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(0, 1);
            statsRect.anchorMax = new Vector2(1, 1);
            statsRect.pivot = new Vector2(0, 1);
            statsRect.anchoredPosition = new Vector2(10, -10);
            statsRect.sizeDelta = new Vector2(-20, 180);

            statsText = statsObj.AddComponent<TextMeshProUGUI>();
            statsText.fontSize = 13;
            statsText.color = Color.white;
            statsText.alignment = TextAlignmentOptions.TopLeft;
            statsText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        // State text
        GameObject stateObj = new GameObject("StateText");
        stateObj.transform.SetParent(debugPanel.transform, false);

        RectTransform stateRect = stateObj.AddComponent<RectTransform>();
        stateRect.anchorMin = new Vector2(0, 0);
        stateRect.anchorMax = new Vector2(1, 0);
        stateRect.pivot = new Vector2(0, 0);
        stateRect.anchoredPosition = new Vector2(10, 10);
        stateRect.sizeDelta = new Vector2(-20, 80);

        stateText = stateObj.AddComponent<TextMeshProUGUI>();
        stateText.fontSize = 11;
        stateText.color = Color.cyan;
        stateText.alignment = TextAlignmentOptions.BottomLeft;
        stateText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        // Visual indicator
        if (showVisualFeedback)
        {
            GameObject indicatorObj = new GameObject("VisualIndicator");
            indicatorObj.transform.SetParent(canvasObj.transform, false);

            RectTransform indicatorRect = indicatorObj.AddComponent<RectTransform>();
            indicatorRect.anchorMin = new Vector2(1, 1);
            indicatorRect.anchorMax = new Vector2(1, 1);
            indicatorRect.pivot = new Vector2(1, 1);
            indicatorRect.anchoredPosition = new Vector2(-10, -10);
            indicatorRect.sizeDelta = new Vector2(60, 60);

            visualIndicator = indicatorObj.AddComponent<Image>();
            visualIndicator.color = idleColor;

            // Energy bar
            GameObject energyObj = new GameObject("EnergyBar");
            energyObj.transform.SetParent(canvasObj.transform, false);

            RectTransform energyRect = energyObj.AddComponent<RectTransform>();
            energyRect.anchorMin = new Vector2(1, 1);
            energyRect.anchorMax = new Vector2(1, 1);
            energyRect.pivot = new Vector2(1, 1);
            energyRect.anchoredPosition = new Vector2(-10, -80);
            energyRect.sizeDelta = new Vector2(60, 150);

            Image energyBg = energyObj.AddComponent<Image>();
            energyBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            GameObject energyFillObj = new GameObject("Fill");
            energyFillObj.transform.SetParent(energyObj.transform, false);

            RectTransform fillRect = energyFillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(1, 0);
            fillRect.pivot = new Vector2(0.5f, 0);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;

            energyBar = energyFillObj.AddComponent<Image>();
            energyBar.color = Color.green;
            energyBar.type = Image.Type.Filled;
            energyBar.fillMethod = Image.FillMethod.Vertical;
            energyBar.fillAmount = 0;
        }
    }

    void CreateLatencyGraph()
    {
        GameObject graphObj = new GameObject("LatencyGraph");
        graphObj.transform.SetParent(debugPanel.transform, false);

        RectTransform graphRect = graphObj.AddComponent<RectTransform>();
        graphRect.anchorMin = new Vector2(0, 0);
        graphRect.anchorMax = new Vector2(1, 0);
        graphRect.pivot = new Vector2(0.5f, 0);
        graphRect.anchoredPosition = new Vector2(0, 100);
        graphRect.sizeDelta = new Vector2(-20, GRAPH_HEIGHT);

        latencyGraph = graphObj.AddComponent<RawImage>();
        
        graphTexture = new Texture2D(GRAPH_WIDTH, GRAPH_HEIGHT, TextureFormat.RGBA32, false);
        graphTexture.filterMode = FilterMode.Point;
        latencyGraph.texture = graphTexture;

        ClearGraph();
    }

    void ClearGraph()
    {
        if (graphTexture == null) return;

        Color[] pixels = new Color[GRAPH_WIDTH * GRAPH_HEIGHT];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(0.1f, 0.1f, 0.1f, 1f);

        // Draw threshold line at 200ms
        int targetLine = (int)(200f / 600f * GRAPH_HEIGHT); // Assuming 600ms max scale
        for (int x = 0; x < GRAPH_WIDTH; x++)
        {
            pixels[targetLine * GRAPH_WIDTH + x] = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }

        graphTexture.SetPixels(pixels);
        graphTexture.Apply();
    }

    void UpdateGraph()
    {
        if (graphTexture == null || latencyHistory.Count < 2) return;

        ClearGraph();

        Color[] pixels = graphTexture.GetPixels();

        // Find max for scaling (with minimum of 600ms)
        float maxScale = 600f; // 600ms max on graph
        foreach (var latency in latencyHistory)
            if (latency * 1000f > maxScale && latency * 1000f < 2000f)
                maxScale = latency * 1000f;

        // Draw latency line
        float step = (float)GRAPH_WIDTH / MAX_LATENCY_SAMPLES;
        
        for (int i = 1; i < latencyHistory.Count; i++)
        {
            int x1 = (int)((i - 1) * step);
            int y1 = (int)((latencyHistory[i - 1] * 1000f / maxScale) * GRAPH_HEIGHT);
            
            int x2 = (int)(i * step);
            int y2 = (int)((latencyHistory[i] * 1000f / maxScale) * GRAPH_HEIGHT);

            y1 = Mathf.Clamp(y1, 0, GRAPH_HEIGHT - 1);
            y2 = Mathf.Clamp(y2, 0, GRAPH_HEIGHT - 1);

            // Color based on speed
            float avgMs = (latencyHistory[i - 1] + latencyHistory[i]) * 500f;
            Color lineColor = avgMs < 200f ? fastColor : (avgMs < 400f ? Color.yellow : slowColor);

            DrawLine(pixels, x1, y1, x2, y2, lineColor);
        }

        graphTexture.SetPixels(pixels);
        graphTexture.Apply();
    }

    void DrawLine(Color[] pixels, int x1, int y1, int x2, int y2, Color color)
    {
        int dx = Mathf.Abs(x2 - x1);
        int dy = Mathf.Abs(y2 - y1);
        int sx = x1 < x2 ? 1 : -1;
        int sy = y1 < y2 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            if (x1 >= 0 && x1 < GRAPH_WIDTH && y1 >= 0 && y1 < GRAPH_HEIGHT)
            {
                int idx = y1 * GRAPH_WIDTH + x1;
                if (idx >= 0 && idx < pixels.Length)
                    pixels[idx] = color;
            }

            if (x1 == x2 && y1 == y2) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x1 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y1 += sy;
            }
        }
    }

    void Update()
    {
        if (Time.time - lastUpdateTime < updateRate)
            return;

        lastUpdateTime = Time.time;
        UpdateUI();
        UpdateVisuals();
    }

    void UpdateUI()
    {
        if (recognizer == null)
            return;

        if (showPerformanceStats && statsText != null)
        {
            float avgLatency = recognizer.GetAverageLatency() * 1000f;
            float successRate = totalDetections > 0 ? (fastDetections * 100f / totalDetections) : 0f;

            string optimizationStatus = avgLatency < 200f ? "<color=green>EXCELLENT</color>" :
                                       avgLatency < 400f ? "<color=yellow>GOOD</color>" :
                                       "<color=red>NEEDS TUNING</color>";

            statsText.text = $"<b>OPTIMIZED VOICE RECOGNITION</b>\n" +
                           $"Status: {optimizationStatus}\n" +
                           $"Uptime: {recognizer.GetUptime():F1}s\n" +
                           $"Frames: {recognizer.GetFrameCount()}\n" +
                           $"Detections: {recognizer.GetSampleCount()}\n" +
                           $"\n<b>LATENCY</b>\n" +
                           $"Avg: <color=green>{avgLatency:F1}ms</color>\n" +
                           $"Min: <color=green>{minLatency:F1}ms</color>\n" +
                           $"Max: <color=red>{maxLatency:F1}ms</color>\n" +
                           $"Fast (<200ms): <color=green>{successRate:F0}%</color>\n" +
                           $"\n<b>SETTINGS</b>\n" +
                           $"Threshold: {recognizer.frameThreshold:F1}\n" +
                           $"Delta: {(recognizer.useDeltaFeatures ? "ON" : "OFF")}";
        }

        if (stateText != null && audioInput != null)
        {
            float processingTime = audioInput.GetProcessingTime();
            
            stateText.text = $"<b>REAL-TIME STATUS</b>\n" +
                           $"Energy: {lastEnergy:F4} (thr: {audioInput.vadThreshold:F4})\n" +
                           $"Processing: {processingTime:F2}ms/frame\n" +
                           $"Last: {(lastLatency > 0 ? $"<color=cyan>{lastLatency:F1}ms</color>" : "waiting...")}";
        }
    }

    void UpdateVisuals()
    {
        if (!showVisualFeedback)
            return;

        // Update visual indicator
        if (visualIndicator != null)
        {
            if (visualFeedbackTimer > 0f)
            {
                visualFeedbackTimer -= updateRate;
                
                // Color based on last latency
                if (lastLatency > 0)
                {
                    visualIndicator.color = lastLatency < 200f ? fastColor : 
                                           lastLatency < 400f ? detectedColor : slowColor;
                }
                else
                {
                    visualIndicator.color = detectedColor;
                }
            }
            else if (lastEnergy > (audioInput?.vadThreshold ?? 0.002f))
            {
                visualIndicator.color = activeColor;
            }
            else
            {
                visualIndicator.color = idleColor;
            }
        }

        // Update energy bar
        if (energyBar != null && audioInput != null)
        {
            float normalizedEnergy = Mathf.Clamp01(lastEnergy / (audioInput.vadThreshold * 10f));
            energyBar.fillAmount = Mathf.Lerp(energyBar.fillAmount, normalizedEnergy, 0.3f);
            energyBar.color = Color.Lerp(Color.red, Color.green, normalizedEnergy);
        }
    }

    void OnDetection(object sender, VoiceEventArgs e)
    {
        visualFeedbackTimer = 0.4f;
        lastLatency = e.Metrics.totalLatency * 1000f;

        // Track statistics
        totalDetections++;
        if (lastLatency < 200f)
            fastDetections++;

        if (lastLatency < minLatency)
            minLatency = lastLatency;
        if (lastLatency > maxLatency)
            maxLatency = lastLatency;

        // Update graph
        if (showLatencyGraph)
        {
            latencyHistory.Add(e.Metrics.totalLatency);
            if (latencyHistory.Count > MAX_LATENCY_SAMPLES)
                latencyHistory.RemoveAt(0);

            UpdateGraph();
        }
    }

    void OnVoiceActivity(float energy)
    {
        lastEnergy = energy;
    }

    public void ToggleVisibility()
    {
        if (debugPanel != null)
            debugPanel.SetActive(!debugPanel.activeSelf);
    }

    public void ResetStats()
    {
        minLatency = float.MaxValue;
        maxLatency = 0f;
        fastDetections = 0;
        totalDetections = 0;
        latencyHistory.Clear();
        ClearGraph();
    }
}
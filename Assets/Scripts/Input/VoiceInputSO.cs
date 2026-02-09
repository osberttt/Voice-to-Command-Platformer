using UnityEngine;

[CreateAssetMenu(fileName = "VoiceInput", menuName = "Input/Voice Input")]
public class VoiceInputSO : InputProviderSO
{
    [Header("Voice Recognition Settings")]
    public float frameThreshold = 12f; // Optimized lower threshold
    public float cooldown = 0.15f; // Reduced cooldown

    [Header("Audio Settings")]
    public int sampleRate = 16000;
    public float vadThreshold = 0.002f;

    [Header("Optimization")]
    [Tooltip("Minimum frames required for valid command (2 = faster)")]
    public int minFramesRequired = 2;
    
    [Tooltip("Use delta (derivative) features for better accuracy")]
    public bool useDeltaFeatures = true;
    
    [Tooltip("Weight of delta features vs static features (0-1)")]
    [Range(0f, 1f)]
    public float deltaWeight = 0.3f;

    [Header("Debugging")]
    public bool enableDebugLogs = true;
    public bool trackMetrics = true;
    public bool showVisualFeedback = false;
    public bool logFrameDistances = false;
    public bool showPerformanceStats = false;

    [Header("Advanced Debug")]
    public bool pauseOnDetection = false;
    public float debugUIUpdateRate = 0.05f; // Faster UI updates

    private bool jumpRequested;
    private bool turnRequested;
    private VoiceRecognizerOptimized recognizer;
    private GameObject recognizerObject;
    private VoiceDebugUIOptimized debugUI;

    public override bool JumpRequested => jumpRequested;
    public override bool TurnRequested => turnRequested;

    // Debug data exposure
    public VoiceRecognizerOptimized Recognizer => recognizer;
    public VoiceDebugUIOptimized DebugUI => debugUI;

    public override void Initialize()
    {
        jumpRequested = false;
        turnRequested = false;

        // Instantiate the voice recognizer
        recognizerObject = new GameObject("VoiceRecognizer_Optimized");
        Object.DontDestroyOnLoad(recognizerObject);

        // Add optimized AudioInput component
        var audioInput = recognizerObject.AddComponent<AudioInputOptimized>();
        audioInput.sampleRate = sampleRate;
        audioInput.vadThreshold = vadThreshold;

        // Add optimized VoiceRecognizer component
        recognizer = recognizerObject.AddComponent<VoiceRecognizerOptimized>();
        recognizer.audioInput = audioInput;
        recognizer.frameThreshold = frameThreshold;
        recognizer.cooldown = cooldown;
        recognizer.minFramesRequired = minFramesRequired;
        recognizer.useDeltaFeatures = useDeltaFeatures;
        recognizer.deltaWeight = deltaWeight;
        recognizer.enableDebugLogs = enableDebugLogs;
        recognizer.trackMetrics = trackMetrics;

        // Subscribe to events
        recognizer.onJumpDetected.AddListener(OnJump);
        recognizer.onTurnDetected.AddListener(OnTurn);

        // Add debug UI if requested
        if (showVisualFeedback || showPerformanceStats)
        {
            debugUI = recognizerObject.AddComponent<VoiceDebugUIOptimized>();
            debugUI.recognizer = recognizer;
            debugUI.audioInput = audioInput;
            debugUI.showVisualFeedback = showVisualFeedback;
            debugUI.showPerformanceStats = showPerformanceStats;
            debugUI.showLatencyGraph = trackMetrics;
            debugUI.updateRate = debugUIUpdateRate;
        }

        Debug.Log($"Voice Input initialized (OPTIMIZED)\n" +
                  $"Frame size: 256 samples = 16ms@16kHz\n" +
                  $"Expected latency: 120-180ms\n" +
                  $"Delta features: {useDeltaFeatures}");
    }

    public override void Cleanup()
    {
        if (recognizer != null)
        {
            recognizer.onJumpDetected.RemoveListener(OnJump);
            recognizer.onTurnDetected.RemoveListener(OnTurn);
        }

        if (recognizerObject != null)
        {
            Object.Destroy(recognizerObject);
        }

        jumpRequested = false;
        turnRequested = false;

        Debug.Log("Voice Input cleaned up");
    }

    void OnJump()
    {
        jumpRequested = true;
    }

    void OnTurn()
    {
        turnRequested = true;
    }

    public override void ConsumeJump()
    {
        jumpRequested = false;
    }

    public override void ConsumeTurn()
    {
        turnRequested = false;
    }

    // Runtime debug control
    public void SetDebugLogs(bool enabled)
    {
        enableDebugLogs = enabled;
        if (recognizer != null)
            recognizer.enableDebugLogs = enabled;
    }

    public void SetMetricsTracking(bool enabled)
    {
        trackMetrics = enabled;
        if (recognizer != null)
            recognizer.trackMetrics = enabled;
    }

    public void SetThreshold(float threshold)
    {
        frameThreshold = threshold;
        if (recognizer != null)
            recognizer.frameThreshold = threshold;
    }

    public void SetCooldown(float cd)
    {
        cooldown = cd;
        if (recognizer != null)
            recognizer.cooldown = cd;
    }

    public void SetDeltaFeatures(bool enabled)
    {
        useDeltaFeatures = enabled;
        if (recognizer != null)
            recognizer.useDeltaFeatures = enabled;
    }

    public void SetDeltaWeight(float weight)
    {
        deltaWeight = Mathf.Clamp01(weight);
        if (recognizer != null)
            recognizer.deltaWeight = deltaWeight;
    }

    // Get debug stats
    public string GetDebugStats()
    {
        if (recognizer == null)
            return "Recognizer not initialized";

        return $"OPTIMIZED VOICE RECOGNITION\n" +
               $"Uptime: {recognizer.GetUptime():F1}s\n" +
               $"Frames: {recognizer.GetFrameCount()}\n" +
               $"Detections: {recognizer.GetSampleCount()}\n" +
               $"Avg Latency: {recognizer.GetAverageLatency() * 1000f:F1}ms\n" +
               $"Delta Features: {useDeltaFeatures}\n" +
               $"Threshold: {frameThreshold:F1}";
    }
}
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Guided voice calibration wizard. Creates its own UI.
/// Records each command 3 times, merges into robust templates.
/// </summary>
public class VoiceCalibrationOptimized : MonoBehaviour
{
    public AudioInputOptimized audioInput;

    [Header("Settings")]
    public int recordingsPerCommand = 3;
    public Color accentColor = new Color(0.3f, 0.7f, 1f);

    // ── State machine ──
    enum Phase { Start, Listening, Recording, Success, Fail, Merging, Complete }
    Phase phase = Phase.Start;

    int commandIndex;       // 0 = jump, 1 = turn
    int recordingIndex;     // 0..2 within current command

    List<VoiceTemplateOptimized> jumpTemplates = new();
    List<VoiceTemplateOptimized> turnTemplates = new();

    List<float[]> buffer = new();
    bool speechStarted;
    float lastWindowTime;
    float recordingStartTime;
    float phaseTimer;

    const float SILENCE_TIME = 0.08f;
    const float MIN_DURATION = 0.15f;
    const float MAX_DURATION = 0.6f;
    const float PAUSE_AFTER_SUCCESS = 0.8f;

    // ── UI references (created in code) ──
    Canvas canvas;
    GameObject panel;
    TextMeshProUGUI titleText;
    TextMeshProUGUI instructionText;
    TextMeshProUGUI subText;
    TextMeshProUGUI statusText;
    TextMeshProUGUI resultText;
    Image energyBarFill;
    Image[] progressDots;
    Button startButton;
    Button redoButton;

    float lastEnergy;

    string CommandName => commandIndex == 0 ? "JUMP" : "TURN";
    int TotalStep => commandIndex * recordingsPerCommand + recordingIndex;
    int TotalSteps => 2 * recordingsPerCommand;

    // ── Lifecycle ──

    void Start()
    {
        CreateUI();
        SetPhase(Phase.Start);

        audioInput.OnWindow += OnWindow;
        audioInput.OnVoiceActivity += e => lastEnergy = e;
    }

    void OnDisable()
    {
        if (audioInput != null)
        {
            audioInput.OnWindow -= OnWindow;
        }
    }

    void Update()
    {
        UpdateEnergyBar();

        switch (phase)
        {
            case Phase.Recording:
                if (!speechStarted) break;

                if (Time.time - recordingStartTime > MAX_DURATION)
                { FinishRecording(); return; }

                if (Time.time - lastWindowTime > SILENCE_TIME)
                { FinishRecording(); return; }
                break;

            case Phase.Success:
                phaseTimer -= Time.deltaTime;
                if (phaseTimer <= 0f)
                    AdvanceStep();
                break;
        }
    }

    // ── Audio callbacks ──

    void OnWindow(float[] mfcc)
    {
        if (phase == Phase.Listening)
        {
            // First voiced frame → transition to recording
            SetPhase(Phase.Recording);
            speechStarted = true;
            recordingStartTime = Time.time;
            lastWindowTime = Time.time;
            buffer.Clear();
            buffer.Add((float[])mfcc.Clone());
            return;
        }

        if (phase != Phase.Recording) return;

        lastWindowTime = Time.time;
        buffer.Add((float[])mfcc.Clone());
    }

    // ── State transitions ──

    void SetPhase(Phase p)
    {
        phase = p;

        switch (p)
        {
            case Phase.Start:
                titleText.text = "VOICE CALIBRATION";
                instructionText.text = "";
                subText.text = $"Record each command {recordingsPerCommand} times";
                statusText.text = "";
                resultText.text = "";
                startButton.gameObject.SetActive(true);
                redoButton.gameObject.SetActive(false);
                UpdateProgressDots();
                break;

            case Phase.Listening:
                speechStarted = false;
                buffer.Clear();
                startButton.gameObject.SetActive(false);
                instructionText.text = $"Say  <b>\"{CommandName}\"</b>";
                subText.text = $"Recording {recordingIndex + 1} of {recordingsPerCommand}";
                statusText.text = "<color=#888>Listening...</color>";
                resultText.text = "";
                redoButton.gameObject.SetActive(TotalStep > 0);
                UpdateProgressDots();
                break;

            case Phase.Recording:
                statusText.text = "<color=#FFD700>Recording...</color>";
                break;

            case Phase.Success:
                phaseTimer = PAUSE_AFTER_SUCCESS;
                statusText.text = "<color=#66FF66>Got it!</color>";
                redoButton.gameObject.SetActive(false);
                break;

            case Phase.Fail:
                statusText.text = "<color=#FF6666>Too short, try again</color>";
                // Auto-retry after brief pause
                Invoke(nameof(RetryCurrentStep), 0.6f);
                break;

            case Phase.Merging:
                instructionText.text = "Processing...";
                subText.text = "";
                statusText.text = "";
                MergeAndSave();
                break;

            case Phase.Complete:
                instructionText.text = "<color=#66FF66>Calibration Complete!</color>";
                startButton.gameObject.SetActive(false);
                redoButton.gameObject.SetActive(false);
                UpdateProgressDots();
                break;
        }
    }

    void BeginCalibration()
    {
        commandIndex = 0;
        recordingIndex = 0;
        jumpTemplates.Clear();
        turnTemplates.Clear();
        SetPhase(Phase.Listening);
    }

    void AdvanceStep()
    {
        recordingIndex++;

        if (recordingIndex >= recordingsPerCommand)
        {
            // Move to next command
            commandIndex++;
            recordingIndex = 0;

            if (commandIndex >= 2)
            {
                // All done
                SetPhase(Phase.Merging);
                return;
            }
        }

        SetPhase(Phase.Listening);
    }

    void RetryCurrentStep()
    {
        SetPhase(Phase.Listening);
    }

    void RedoLastStep()
    {
        // Go back one step
        if (recordingIndex > 0)
        {
            recordingIndex--;
            var list = commandIndex == 0 ? jumpTemplates : turnTemplates;
            if (list.Count > recordingIndex)
                list.RemoveAt(recordingIndex);
        }
        else if (commandIndex > 0)
        {
            commandIndex--;
            recordingIndex = recordingsPerCommand - 1;
            var list = commandIndex == 0 ? jumpTemplates : turnTemplates;
            if (list.Count > recordingIndex)
                list.RemoveAt(recordingIndex);
        }

        SetPhase(Phase.Listening);
    }

    // ── Recording logic ──

    void FinishRecording()
    {
        float duration = Time.time - recordingStartTime;

        if (duration < MIN_DURATION || buffer.Count < 3)
        {
            SetPhase(Phase.Fail);
            return;
        }

        var template = new VoiceTemplateOptimized();
        template.BuildFromRecording(buffer);

        if (!template.IsComplete)
        {
            SetPhase(Phase.Fail);
            return;
        }

        if (commandIndex == 0)
            jumpTemplates.Add(template);
        else
            turnTemplates.Add(template);

        subText.text = $"{duration * 1000:F0}ms, {buffer.Count} frames";
        Debug.Log($"{CommandName} recording {recordingIndex + 1}: {template.windows.Count} windows, {buffer.Count} frames");

        SetPhase(Phase.Success);
    }

    // ── Merge & save ──

    void MergeAndSave()
    {
        var data = new VoiceTemplateDataOptimized();

        data.jump = VoiceTemplateOptimized.MergeTemplates(jumpTemplates);
        data.turn = VoiceTemplateOptimized.MergeTemplates(turnTemplates);

        if (data.jump == null || data.turn == null || !data.jump.IsComplete || !data.turn.IsComplete)
        {
            instructionText.text = "<color=#FF6666>Merge failed</color>";
            return;
        }

        // Compute inter-template distance
        float interDist = CentroidDistance(data.jump.centroid, data.turn.centroid);

        // Compute within-class spread
        float jumpSpread = VoiceTemplateOptimized.ComputeSpread(jumpTemplates, data.jump.centroid);
        float turnSpread = VoiceTemplateOptimized.ComputeSpread(turnTemplates, data.turn.centroid);
        float maxSpread = Mathf.Max(jumpSpread, turnSpread);

        // Smart threshold: tight enough to separate, loose enough for variation
        float threshold = Mathf.Min(interDist * 0.5f, interDist - maxSpread * 1.5f);
        threshold = Mathf.Max(threshold, maxSpread * 1.2f);
        threshold = Mathf.Max(threshold, 0.05f); // absolute floor

        data.autoThreshold = threshold;
        data.jump.autoThreshold = threshold;
        data.turn.autoThreshold = threshold;

        // Save
        string path = Path.Combine(Application.persistentDataPath, "voice_templates_optimized.json");
        File.WriteAllText(path, JsonUtility.ToJson(data, true));

        Debug.Log($"Calibration saved: interDist={interDist:F3}, spread={maxSpread:F3}, threshold={threshold:F3}");
        Debug.Log($"Saved to: {path}");

        // Show results
        string quality = interDist > maxSpread * 3f ? "<color=#66FF66>Excellent</color>" :
                         interDist > maxSpread * 2f ? "<color=#FFD700>Good</color>" :
                                                      "<color=#FF6666>Low — try more distinct sounds</color>";

        resultText.text = $"Distance between commands: {interDist:F3}\n" +
                          $"Variation spread: {maxSpread:F3}\n" +
                          $"Threshold: {threshold:F3}\n" +
                          $"Quality: {quality}";

        SetPhase(Phase.Complete);
    }

    float CentroidDistance(float[] a, float[] b)
    {
        float sum = 0f;
        for (int i = 0; i < Mathf.Min(a.Length, b.Length); i++)
        {
            float d = a[i] - b[i];
            sum += d * d;
        }
        return Mathf.Sqrt(sum);
    }

    // ── UI creation ──

    void CreateUI()
    {
        // Canvas
        var canvasObj = new GameObject("CalibrationCanvas");
        canvasObj.transform.SetParent(transform);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        // Background
        var bgObj = CreateRect("Background", canvasObj.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.08f, 0.08f, 0.12f, 1f);

        // Center panel
        panel = CreateRect("Panel", canvasObj.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(600, 500));
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);

        // Title
        titleText = CreateTMP("Title", panel.transform,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -20), new Vector2(0, 50), 28, Color.white, TextAlignmentOptions.Center);
        titleText.fontStyle = FontStyles.Bold;

        // Progress dots container
        var dotsParent = CreateRect("ProgressDots", panel.transform,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -80), new Vector2(200, 20));

        progressDots = new Image[6];
        float dotSpacing = 30f;
        float dotsStart = -(6 - 1) * dotSpacing * 0.5f;
        for (int i = 0; i < 6; i++)
        {
            var dotObj = CreateRect($"Dot{i}", dotsParent.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(dotsStart + i * dotSpacing, 0), new Vector2(14, 14));
            progressDots[i] = dotObj.AddComponent<Image>();
            progressDots[i].color = new Color(0.3f, 0.3f, 0.3f);
        }

        // Instruction text (large)
        instructionText = CreateTMP("Instruction", panel.transform,
            new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 50), new Vector2(0, 60), 36, Color.white, TextAlignmentOptions.Center);
        instructionText.richText = true;

        // Sub text
        subText = CreateTMP("SubText", panel.transform,
            new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 10), new Vector2(0, 30), 18, new Color(0.6f, 0.6f, 0.6f), TextAlignmentOptions.Center);

        // Energy bar background
        var energyBg = CreateRect("EnergyBarBg", panel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -40), new Vector2(400, 16));
        var energyBgImg = energyBg.AddComponent<Image>();
        energyBgImg.color = new Color(0.2f, 0.2f, 0.2f);

        // Energy bar fill
        var energyFillObj = CreateRect("EnergyFill", energyBg.transform,
            Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f),
            Vector2.zero, Vector2.zero);
        energyBarFill = energyFillObj.AddComponent<Image>();
        energyBarFill.color = accentColor;

        // Status text
        statusText = CreateTMP("Status", panel.transform,
            new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -75), new Vector2(0, 30), 20, Color.white, TextAlignmentOptions.Center);
        statusText.richText = true;

        // Result text (multi-line, for completion screen)
        resultText = CreateTMP("Result", panel.transform,
            new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(0.5f, 0),
            new Vector2(0, 20), new Vector2(-40, -120), 16, new Color(0.8f, 0.8f, 0.8f), TextAlignmentOptions.Center);
        resultText.richText = true;

        // Start button
        startButton = CreateButton("StartBtn", panel.transform,
            new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(200, 50),
            "Start", accentColor, OnStartClicked);

        // Redo button
        redoButton = CreateButton("RedoBtn", panel.transform,
            new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(200, 40),
            "Redo Last", new Color(0.4f, 0.4f, 0.5f), OnRedoClicked);
        redoButton.gameObject.SetActive(false);
    }

    void UpdateEnergyBar()
    {
        if (energyBarFill == null) return;

        float target = 0f;
        if (phase == Phase.Listening || phase == Phase.Recording)
        {
            target = Mathf.Clamp01(lastEnergy / (audioInput.vadThreshold * 10f));
        }

        var rt = energyBarFill.GetComponent<RectTransform>();
        float current = rt.anchorMax.x;
        float smooth = Mathf.Lerp(current, target, Time.deltaTime * 15f);
        rt.anchorMax = new Vector2(smooth, 1);

        energyBarFill.color = phase == Phase.Recording ?
            Color.Lerp(accentColor, new Color(1f, 0.8f, 0.2f), Mathf.PingPong(Time.time * 3f, 1f)) :
            accentColor;
    }

    void UpdateProgressDots()
    {
        for (int i = 0; i < progressDots.Length; i++)
        {
            if (i < TotalStep)
                progressDots[i].color = new Color(0.3f, 1f, 0.4f); // completed
            else if (i == TotalStep && phase != Phase.Start && phase != Phase.Complete)
                progressDots[i].color = accentColor; // current
            else if (phase == Phase.Complete)
                progressDots[i].color = new Color(0.3f, 1f, 0.4f); // all done
            else
                progressDots[i].color = new Color(0.3f, 0.3f, 0.3f); // pending
        }
    }

    // ── Button callbacks ──

    void OnStartClicked() => BeginCalibration();
    void OnRedoClicked() => RedoLastStep();

    // Keep legacy public API for backward compatibility
    public void RecordJump() => BeginCalibration();
    public void RecordTurn() { commandIndex = 1; recordingIndex = 0; SetPhase(Phase.Listening); }
    public void Save() => MergeAndSave();

    // ── UI factory helpers ──

    GameObject CreateRect(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        return obj;
    }

    TextMeshProUGUI CreateTMP(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta,
        float fontSize, Color color, TextAlignmentOptions align)
    {
        var obj = CreateRect(name, parent, anchorMin, anchorMax, pivot, anchoredPos, sizeDelta);
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        return tmp;
    }

    Button CreateButton(string name, Transform parent,
        Vector2 anchor, Vector2 anchoredPos, Vector2 size,
        string label, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        var obj = CreateRect(name, parent,
            anchor, anchor, new Vector2(0.5f, 0),
            anchoredPos, size);

        var img = obj.AddComponent<Image>();
        img.color = bgColor;

        var btn = obj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var txtObj = new GameObject("Label");
        txtObj.transform.SetParent(obj.transform, false);
        var txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        var tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        return btn;
    }
}

[System.Serializable]
public class VoiceTemplateDataOptimized
{
    public VoiceTemplateOptimized jump = new VoiceTemplateOptimized();
    public VoiceTemplateOptimized turn = new VoiceTemplateOptimized();
    public float autoThreshold = 2.0f;
}

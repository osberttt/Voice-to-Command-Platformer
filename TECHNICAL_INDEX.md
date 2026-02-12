# Voice-to-Command Platformer — Technical Index

## Overview

A Unity 2D auto-runner controlled by two voice commands: **JUMP** and **TURN**. No arrow keys. The player calibrates by recording each command once, the system extracts MFCC features from those recordings and saves them as L2-normalized templates with a centroid vector. During gameplay, incoming audio is normalized and compared against template centroids using competitive matching — only the closer template can accumulate match credit, with a margin requirement to prevent ambiguous triggers.

**After v2 fixes:** Old templates are invalidated (MFCC domain changed). Must recalibrate.

---

## Directory Map

```
Assets/Scripts/
├── Core/Audio Input/
│   ├── AudioInputOptimized.cs            ← Mic capture + VAD + MFCC dispatch
│   ├── VoiceCalibrationOptimized.cs      ← Record templates + auto-threshold
│   ├── VoiceRecognizerOptimized.cs       ← Competitive centroid matching engine
│   ├── Utilities/
│   │   ├── FFT Utility.cs               ← Fallback O(n²) DFT
│   │   ├── LiteMFCC.cs                  ← Fallback MFCC extractor
│   │   ├── Mel Filter Bank.cs           ← 26 triangular mel filters
│   │   ├── MFCCFrame.cs                 ← Serializable MFCC container
│   │   ├── VoiceTemplateLite.cs         ← Legacy template (unused)
│   │   ├── VoiceTemplateDataLite.cs     ← Legacy data wrapper (unused)
│   │   └── Fast/
│   │       ├── FastFFTJob.cs            ← Burst-compiled Cooley-Tukey FFT
│   │       ├── FastMFCC.cs              ← MFCC: Hamming→FFT→Mel→Log→DCT
│   │       └── VoiceTemplateOptimized.cs← Template: centroid + L2 norm + onset frames
│   └── Performance/
│       ├── VoiceDebugUI.cs              ← Runtime overlay (latency graph, stats)
│       ├── VoiceRecognitionMetrics.cs   ← Metrics struct + VoiceEventArgs
│       ├── VoiceTestRecorder.cs         ← Multi-repetition WAV recorder
│       └── WavUtility.cs               ← 16-bit PCM WAV read/write
├── Input/
│   ├── InputProviderSO.cs               ← Abstract input base (ScriptableObject)
│   ├── VoiceInputSO.cs                  ← Voice provider (wires pipeline + marginFactor)
│   └── KeyboardInputSO.cs              ← Keyboard fallback (J=jump, F=turn)
├── Player Movement.cs                   ← Auto-run platformer controller
└── Editor/
    └── PlayerMovementEditor.cs          ← Custom inspector
```

**Data files** (at `Application.persistentDataPath`):
- `voice_templates_optimized.json` — Saved calibration templates + auto-threshold
- `test_*_.wav` / `*_markers.json` — Test recordings from VoiceTestRecorder

**Scenes:**
- `Calibration Lite` — Record jump/turn templates
- `Recognize Lite` — Test recognition standalone
- `Base Map` — Main gameplay

---

## Pipeline: End-to-End

```
┌──────────────┐    ┌──────────────┐    ┌───────────────┐    ┌──────────────┐
│  MICROPHONE   │───▶│ AUDIO INPUT  │───▶│  RECOGNIZER   │───▶│   PLAYER     │
│  (16kHz mono) │    │ (frame+MFCC) │    │ (competitive  │    │ (auto-run +  │
│               │    │              │    │  centroid      │    │  jump/turn)  │
│               │    │              │    │  matching)     │    │              │
└──────────────┘    └──────────────┘    └───────────────┘    └──────────────┘
```

### Stage 1 — Audio Capture (`AudioInputOptimized.cs`)

| Parameter       | Value   | Notes                          |
|-----------------|---------|--------------------------------|
| Sample rate     | 16,000 Hz | Adequate for speech           |
| Frame size      | 256 samples | = 16ms per frame            |
| Hop size        | 128 samples | 50% overlap                 |
| VAD threshold   | 0.002   | Energy = sum(sample²)          |
| Mic buffer      | 1 second | Circular, `Microphone.Start`  |

**Flow:**
1. `Update()` reads new samples from Unity's `Microphone.GetPosition`
2. Samples feed into a ring buffer of size 256
3. When ring is full → `ProcessFrame()`
4. Compute frame energy. If energy < 0.002 → skip (silence)
5. If voiced → `FastMFCC.Extract(frame)` → 6 MFCC coefficients
6. Fire `OnWindow(float[6])` callback to recognizer
7. Fire `OnVoiceActivity(energy)` callback to debug UI

### Stage 2 — Feature Extraction (`FastMFCC.cs` + dependencies)

```
256 samples
    │
    ▼
Hamming window (256-point, matched to frame size)
    │
    ▼
FastFFTJob (Burst-compiled Cooley-Tukey, O(n log n))
    │  Output: 128 magnitude bins
    ▼
Power spectrum (magnitude²)
    │
    ▼
Mel Filter Bank (26 triangular filters, 0–8kHz, nfft=128)
    │  Output: 26 mel energies (floor = 1e-10)
    ▼
Log compression: log(mel energy)
    │
    ▼
DCT → 6 coefficients (MFCC_DIM = 6)
```

**Key details:**
- Hamming window is `FRAME_SIZE = 256` elements, matching the actual audio frame. Symmetric, properly tapered at edges.
- `MelFilterBank` is constructed with `frameSize=256` → `nfft = 128`, exactly matching the 128 FFT magnitude bins.
- Log compression (`Mathf.Log`) is applied to mel energies before DCT. The mel bank floors at 1e-10, so log produces ~-23 at minimum (valid finite float). This is standard MFCC and critical for separating phonetically different sounds.

### Stage 3 — Calibration (`VoiceCalibrationOptimized.cs`)

**User flow:** Press "Say JUMP" → speak → silence auto-stops → press "Say TURN" → speak → press "Save"

**Recording logic:**
1. Starts collecting every MFCC frame that passes VAD
2. First voiced frame sets `speechStarted = true`
3. Stops when 80ms of silence (`SILENCE_TIME = 0.08s`) or 600ms elapsed
4. Validates: duration >= 150ms, frame count >= 3, then >=5 for `BuildFromRecording`

**Template building** (`VoiceTemplateOptimized.BuildFromRecording`):
1. `FindOnset()` — Finds frame with max energy derivative (biggest energy jump)
2. `SelectDiscriminativeFrames()` — Picks onset-1 through onset+3 (**up to 5 frames** covering the attack transient)
3. **Centroid computation** — Averages ALL voiced frames into a single MFCC vector, then L2-normalizes
4. All stored frames are L2-normalized (unit vectors)
5. `ComputeDeltas()` — Delta computed from normalized frames for consistent domain
6. `energyProfile` — Sum of absolute MFCC values for up to 3 selected frames

**Auto-threshold (on Save):**
- Computes Euclidean distance between jump and turn centroids
- Sets `autoThreshold = interDist * 0.5` (half the inter-template distance)
- Stored in JSON alongside templates
- Recognizer loads and uses this instead of hard-coded 12.0

**Saved template structure** (per command):
```json
{
  "windows": [
    {"values": [6 floats]},   // normalized onset-1
    {"values": [6 floats]},   // normalized onset
    {"values": [6 floats]},   // normalized onset+1
    {"values": [6 floats]},   // normalized onset+2
    {"values": [6 floats]}    // normalized onset+3
  ],
  "energyProfile": [float, float, float],
  "deltaCoefficients": [6 floats],
  "centroid": [6 floats],
  "autoThreshold": float
}
```

Top-level JSON also contains `"autoThreshold"` field.

### Stage 4 — Recognition (`VoiceRecognizerOptimized.cs`)

**Per MFCC frame (every ~16ms when voiced):**

```
1. L2-normalize incoming MFCC → normMFCC (volume invariance)

2. Compute delta: normMFCC - prevNormMFCC

3. Compute distance to BOTH template centroids:
   jumpDist = EuclideanDist(normMFCC, jump.centroid)
   turnDist = EuclideanDist(normMFCC, turn.centroid)
   (optionally blended with delta distance at 30% weight)

4. Competitive matching — winner-take-all:
   If jumpDist <= turnDist:
     jumpCloser = true
     hasMargin = jumpDist < turnDist * marginFactor (0.8)
     → Only JUMP can accumulate match credit
     → TURN matchCount resets to 0
   Else:
     → Only TURN can accumulate
     → JUMP matchCount resets to 0

5. For the winner:
   If dist < autoThreshold AND hasMargin:
     matchCount++
     If matchCount >= minFramesRequired (2) → FIRE command
   Else:
     matchCount = 0
```

**On fire:**
- Lock recognizer for `cooldown` (150ms)
- Reset both jump and turn match counts
- Invoke Unity event → `VoiceInputSO` sets `jumpRequested`/`turnRequested` flag
- Record `VoiceRecognitionMetrics`

**Key parameters:**
| Parameter         | Value      | Effect                                    |
|-------------------|------------|-------------------------------------------|
| frameThreshold    | auto       | Loaded from template JSON (interDist*0.5) |
| minFramesRequired | 2          | Consecutive matching frames needed        |
| cooldown          | 0.15s      | Lockout after detection                   |
| useDeltaFeatures  | true       | Blend delta distance at 30% weight        |
| deltaWeight       | 0.3        | How much delta contributes to final dist  |
| marginFactor      | 0.8        | Winner must be < 80% of loser's distance  |

### Stage 5 — Player Movement (`Player Movement.cs`)

Auto-runner with physics-based 2D movement.

| Parameter          | Value      | Notes                        |
|--------------------|------------|------------------------------|
| Top speed          | 8 units/s  | Constant auto-run speed      |
| Jump max height    | 4 units    | Derived from gravity formula |
| Time to apex       | 0.4s       | Controls jump arc            |
| Time to fall       | 0.3s       | Faster fall than rise        |
| Air jumps          | 1          | Double jump                  |
| Coyote time        | 0.1s       | Grace period after ledge     |
| Jump buffer        | 0.1s       | Pre-land input buffer        |
| Air control        | 0.5x       | Reduced horizontal in air    |
| Max fall speed     | 20 units/s | Terminal velocity            |

**Input flow:**
- `Update()` reads `inputProvider.JumpRequested` / `TurnRequested`
- TURN → flip `facingDir` (1 or -1), consume input
- JUMP → set `bufferTimer = 0.1s`, consume input
- `FixedUpdate()` applies physics: ground check, coyote timer, jump execution, asymmetric gravity

**Input abstraction** (`InputProviderSO`):
- `VoiceInputSO` — Creates recognizer pipeline at runtime, exposes debug controls + marginFactor
- `KeyboardInputSO` — J key = jump, F key = turn

---

## Debug & Measurement Tools

### VoiceDebugUI (`VoiceDebugUI.cs`)

Runtime overlay created programmatically (no prefab needed):
- **Stats panel** (top-left): Status, uptime, frame count, detections, avg/min/max latency, % fast (<200ms), current threshold and delta setting
- **Visual indicator** (top-right): Green=listening, Yellow=detected, color-coded by latency
- **Energy bar**: Normalized mic energy level
- **Latency graph**: 300x100px texture, 50-sample history, Bresenham line drawing. Green <200ms, yellow 200–400ms, red >400ms. Threshold line at 200ms.

### VoiceRecognitionMetrics (`VoiceRecognitionMetrics.cs`)

Captured per detection event:
- `speakStartTime` — When first frame matched
- `invokeTime` — When command fired
- `totalLatency` — invokeTime - speakStartTime (this is **matching** latency, not end-to-end)
- `frameLatency` — Number of consecutive frames that matched
- `command` — "JUMP" or "TURN"

**Important:** The reported latency only measures time from first template match to fire. It does NOT include mic-to-first-match delay (speech onset → first frame exceeding threshold → MFCC extraction). Actual end-to-end latency is higher.

### VoiceTestRecorder (`VoiceTestRecorder.cs`)

Records N repetitions of a command for offline analysis:
- Auto-detects command boundaries via VAD + silence gap (150ms)
- Minimum command duration: 300ms
- Saves raw 16-bit PCM WAV + JSON markers with start/end timestamps per utterance
- Default: 5 repetitions per command

### WavUtility (`WavUtility.cs`)

Simple 16-bit PCM mono WAV reader/writer. Used by VoiceTestRecorder for saving test data.

---

## Issues Fixed (v2)

### 1. Frame size / Hamming window mismatch — FIXED
- **Was:** Hamming window size 400, MelFilterBank frameSize=400, but frames are 256 samples
- **Now:** `FastMFCC.FRAME_SIZE = 256`. Hamming window and MelFilterBank both sized to 256. FFT produces 128 bins, mel bank expects 128 bins — perfect alignment.

### 2. Only 2 template frames — FIXED
- **Was:** `SelectDiscriminativeFrames` kept only onset + onset+1 (2 frames)
- **Now:** Selects onset-1 through onset+3 (up to 5 frames). Also computes a **centroid** (mean of ALL voiced frames), which the recognizer uses for distance comparison. Much more robust.

### 3. No MFCC normalization — FIXED
- **Was:** Raw Euclidean distance on un-normalized values. Volume shifts broke matching.
- **Now:** L2 normalization applied everywhere — stored template frames, centroid, and incoming frames at recognition time. `VoiceTemplateOptimized.NormalizeMFCC()` divides by vector magnitude. Distance is now purely about spectral shape, not volume.

### 4. No log compression — FIXED
- **Was:** Mel energies went directly to DCT. High-energy bands dominated.
- **Now:** `Mathf.Log(melSpec[n])` applied after mel filtering, before DCT. Standard MFCC pipeline. Mel bank floors at 1e-10 so log is always finite.

### 5. Hard-coded threshold (12.0) — FIXED
- **Was:** `frameThreshold = 12.0` regardless of actual template separation. The old templates were only ~1.37 apart in MFCC space — threshold was 9x too loose.
- **Now:** Auto-calibrated at save time. `autoThreshold = interCentroidDistance * 0.5`. Stored in JSON, loaded by recognizer. Each template's acceptance radius exactly reaches the midpoint between templates.

### 6. No mutual exclusion — FIXED
- **Was:** Both `CheckCommand(jump)` and `CheckCommand(turn)` ran independently. Ambiguous frames could trigger the wrong command.
- **Now:** Competitive matching. Each frame computes distance to BOTH centroids. Only the closer template accumulates match credit (winner-take-all). The loser's matchCount resets. Additionally requires a **margin**: winner's distance must be < loser's distance * `marginFactor` (0.8). Ambiguous frames (nearly equidistant) are rejected.

### Remaining potential improvements (not yet implemented)
- DTW (Dynamic Time Warping) for handling speech rate variation
- Multi-recording calibration (average templates from several attempts)
- Cepstral Mean Normalization (CMN) for noise robustness
- Adaptive threshold refinement during gameplay

---

## Latency Breakdown

| Component                               | Estimated   |
|-----------------------------------------|-------------|
| Unity `Microphone.GetPosition` polling  | 0–16ms      |
| Ring buffer fill (256 samples @ 16kHz)  | 16ms        |
| FastMFCC extraction                     | ~1–5ms      |
| Frame matching + normalization          | <1ms        |
| 2 consecutive matches needed            | ~32ms       |
| **Subtotal (pipeline)**                 | **~50–70ms**|
| Actual speech onset → energy > VAD      | Variable    |
| `Update()` polling interval             | 1 frame (~16ms @ 60fps) |

The reported `totalLatency` metric measures first-match-to-fire only. Actual end-to-end includes speech onset delay and VAD buildup.

---

## Config Quick Reference

**Tunable at runtime via VoiceInputSO inspector:**
| Field              | Default | Location                    |
|--------------------|---------|-----------------------------|
| frameThreshold     | 12.0 (overridden by auto) | VoiceInputSO / Recognizer |
| cooldown           | 0.15s   | VoiceInputSO / Recognizer   |
| minFramesRequired  | 2       | VoiceInputSO / Recognizer   |
| useDeltaFeatures   | true    | VoiceInputSO / Recognizer   |
| deltaWeight        | 0.3     | VoiceInputSO / Recognizer   |
| marginFactor       | 0.8     | VoiceInputSO / Recognizer   |
| sampleRate         | 16000   | VoiceInputSO / AudioInput   |
| vadThreshold       | 0.002   | VoiceInputSO / AudioInput   |

**Calibration constants** (hardcoded in VoiceCalibrationOptimized):
| Constant       | Value  |
|----------------|--------|
| SILENCE_TIME   | 0.08s  |
| MIN_DURATION   | 0.15s  |
| MAX_DURATION   | 0.6s   |

**Template building** (hardcoded in VoiceTemplateOptimized):
| Behavior                  | Value                   |
|---------------------------|-------------------------|
| Min frames for build      | 5                       |
| Frames kept in template   | up to 5 (onset-1..+3)  |
| Centroid                  | Mean of ALL frames, L2-normalized |
| Delta coefficients        | 6 (from normalized frames) |
| Energy profile slots      | 3                       |

**Tuning guide:**
- If too many false positives (wrong command fires): increase `marginFactor` toward 0.9
- If too many misses (valid commands ignored): decrease `marginFactor` toward 0.6
- If both commands always confused: re-record with more distinct pronunciation, check auto-threshold in debug log

---

## File-by-File Reference

| File | Lines | Role |
|------|-------|------|
| `AudioInputOptimized.cs` | 99 | Mic capture, ring buffer, VAD, MFCC dispatch |
| `VoiceCalibrationOptimized.cs` | 163 | Calibration recording + auto-threshold + save |
| `VoiceRecognizerOptimized.cs` | 296 | Competitive centroid matching + L2 normalization |
| `VoiceTemplateOptimized.cs` | 119 | Centroid + 5 onset frames + L2 norm + NormalizeMFCC |
| `FastMFCC.cs` | 78 | Hamming(256) → FFT → Mel → Log → DCT (6 coefficients) |
| `FastFFTJob.cs` | 100 | Burst-compiled Cooley-Tukey in-place FFT |
| `Mel Filter Bank.cs` | 76 | 26 triangular mel-scale filters |
| `MFCCFrame.cs` | 17 | Serializable List\<float\> wrapper |
| `VoiceDebugUI.cs` | 456 | Runtime debug overlay with latency graph |
| `VoiceRecognitionMetrics.cs` | 34 | Metrics struct + EventArgs |
| `VoiceTestRecorder.cs` | 227 | Multi-repetition WAV recorder for testing |
| `WavUtility.cs` | ~80 | 16-bit PCM WAV read/write |
| `InputProviderSO.cs` | 14 | Abstract input ScriptableObject |
| `VoiceInputSO.cs` | 203 | Voice input wiring + marginFactor + runtime debug API |
| `KeyboardInputSO.cs` | 46 | J/F keyboard fallback |
| `Player Movement.cs` | 284 | Auto-run 2D platformer controller |
| `PlayerMovementEditor.cs` | ~30 | Custom inspector for PlayerMovement |

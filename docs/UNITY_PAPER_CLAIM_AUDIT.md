# Unity Paper Claim Audit

Verified against the `ai-agent-integration` branch. Paths are under
`Unity-PassthroughCameraApiSamples/Assets/PassthroughCameraApiSamples/RipDetection/`.
Status vocabulary: **SUPPORTED**, **UNSUPPORTED**, **NOT MEASURED**,
**NOT ACTIVE IN SHIPPED CONFIGURATION**.

Shipped/active configuration = `RipDetectionScene` with `m_sentisModel =
medium.onnx`; `DetectionManager.Start()` also forces `medium` at startup.

| Claim | Status | Evidence |
|---|---|---|
| Runtime confidence threshold = 0.25 | SUPPORTED | Single YOLOv8 decode path for 5/6/84-channel outputs; confidence = best class score; `if (confidence >= 0.25f)` — `SentisInferenceUiManager.cs:411`. |
| Runtime NMS IoU threshold = 0.5 | SUPPORTED | `NmsIndices(candidates, 0.5f)` hardcoded literal — `SentisInferenceUiManager.cs:547`. |
| `m_scoreThreshold` (0.25 in scene / 0.23 default) affects runtime detections | UNSUPPORTED | Read only by a `Debug.Log` (`SentisInferenceRunManager.cs:157`) and the **Editor-only** `SentisModelEditorConverter` (`SentisInference/Editor/…:23,62`). Never used in `DrawUIBoxes`/NMS. |
| `m_iouThreshold` (0.45 in scene / 0.6 default) affects runtime detections | UNSUPPORTED | Same two read sites; Editor-only. Not used at runtime. |
| Active model filename | SUPPORTED | `medium.onnx`; scene `m_sentisModel` GUID `1e528bc3…` and `DetectionManager.Start()` → `SetModel(m_modelMedium, "YOLOv8-medium")`. |
| Active output shape | SUPPORTED | `output0 = [1, 5, 8400]` (ONNX inspection of `medium.onnx`). |
| Active class count | SUPPORTED | 5 channels = 4 bbox + **1 class** score, so argmax over class scores always gives `classId = 0` (`:406`). |
| Model input resolution 640×640 | SUPPORTED | `m_inputSize = new(640,640)` (`SentisInferenceRunManager.cs:15`); ONNX input `[1,3,640,640]`. |
| Multi-class model in shipped configuration | NOT ACTIVE IN SHIPPED CONFIGURATION | A 2-class model (`2 Types Model.onnx`, `[1,6,8400]`) exists and is switchable ("YOLOv8-dual"), but the default/active model is single-class `medium.onnx`. |
| `sediment_rip` emitted in shipped configuration | NOT ACTIVE IN SHIPPED CONFIGURATION | Only the 2-class model (6 channels: P(rip), P(sediment_rip)) can yield `classId = 1` via argmax (`:396-406`). The shipped 5-channel model has one class score, so it only emits `rip`. (Before the decode fix, the 6-channel path misread P(rip) as objectness and could never emit `sediment_rip`.) |
| Can emit `rip` | SUPPORTED | 5-channel model: single class score, `classId = 0`, `m_labels[0] = "rip"`. |
| "YOLOv8m" is the active model | SUPPORTED | Labeled "YOLOv8-medium"; `medium.onnx` is the scene/startup model. (Asset naming; exact training provenance not verifiable from output shape.) |
| "YOLOv8n" is the active model | NOT ACTIVE IN SHIPPED CONFIGURATION | `nano.onnx` (also `[1,5,8400]`) is a runtime-switchable alternative, not the default. |
| Scheduled inference timing available | SUPPORTED | `lastInferenceDurationMs` computed (`SentisInferenceRunManager.cs:182`); now logged as `RIPFINDER_TIMING,stage=inference`. |
| Application FPS available | UNSUPPORTED | HUD "Inference rate … per second" (formerly "Frame rate … FPS") is `1000f / inferenceTimeMs` (`DetectionUiMenuManager.cs`) — reciprocal of scheduled inference duration, not application frame rate. True app FPS is not measured. |
| Post-processing timing available | SUPPORTED | `RIPFINDER_TIMING,stage=post_processing` around decode→NMS (`SentisInferenceUiManager.cs`), excluding UI drawing. The rip snapshot (PNG capture/save) now runs after this timed section; earlier builds included it in some samples. |
| HTTP round-trip timing available | SUPPORTED | Added `RIPFINDER_TIMING,stage=http_round_trip` around `SendWebRequest()` (`RipFinderAgentClient.cs`). |
| Claude text reaches the Unity panel | SUPPORTED (code path); NOT MEASURED (live) | `AssessmentReceived → RipFinderAssessmentPanel.ShowAssessment` renders `summary`/`recommendation`/`safety_note`/`risk_level`/`risk_score` (`RipFinderAssessmentPanel.cs:53-66`). A live end-to-end capture with `source=claude` was not run (no API key in this workspace). |
| `assessment_source` compatibility | SUPPORTED | Field declared in `RipFinderAssessmentResponse` (`RipFinderModels.cs:21`); `JsonUtility.FromJson` populates it; now logged in the panel. No parse breakage. |

## Correction to the earlier audit

An earlier note reported the Unity thresholds as "0.23 / 0.6." Those are the
`SentisInferenceRunManager` serialized fields, which are **Editor-only** (model
conversion + a log line) and do **not** affect runtime detection. The actual
runtime post-processing for the shipped 5-channel `medium.onnx` uses the
hardcoded literals **confidence 0.25** and **NMS IoU 0.5** in
`SentisInferenceUiManager`. Report 0.25 / 0.5 in the paper.

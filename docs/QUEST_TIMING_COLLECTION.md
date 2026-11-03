# Quest Timing Collection

Procedure for collecting real on-device timings from the RipFinder build. These
are hardware measurements — collect them on a Meta Quest 3; do not estimate.

The build emits three machine-readable timing lines (removable instrumentation):

```
RIPFINDER_TIMING,stage=inference,ms=...
RIPFINDER_TIMING,stage=post_processing,ms=...
RIPFINDER_TIMING,stage=http_round_trip,ms=...
```

> `scheduled inference duration != application FPS`. The `inference` value is the
> wall-clock duration of one Sentis inference stepped across multiple frames
> (`m_layersPerFrame` layers/frame); it includes inter-frame gaps. The HUD
> "Inference rate … per second" line (formerly labelled "Frame rate … FPS") is `1000 / inference_ms`, an inference-throughput
> reciprocal — **not** the application frame rate.

## Steps

1. Build and run the production `RipDetectionScene` on Quest 3 (active model:
   `medium.onnx`, input 640×640).
2. Warm up for ~30 seconds (let model load, shaders compile, thermals settle).
3. Start log capture from a connected host:

   ```
   adb logcat -c
   adb logcat -s Unity > quest_timing.log
   ```

4. Run a beach / rip scene for at least 30–60 seconds.
5. Trigger multiple assessments (move so `rip_detected`/confidence/count change,
   or wait past the coordinator cooldown) so several `http_round_trip` lines fire.
6. Stop capture (Ctrl-C).
7. Extract the timing lines:

   ```
   grep RIPFINDER_TIMING quest_timing.log > quest_timing_filtered.log
   ```

   Per-stage split, if wanted:

   ```
   grep 'stage=inference'       quest_timing_filtered.log
   grep 'stage=post_processing' quest_timing_filtered.log
   grep 'stage=http_round_trip' quest_timing_filtered.log
   ```

## Target sample counts (post-warm-up)

- inference: **20–30** samples
- post_processing: **20–30** samples
- http_round_trip: as many as practical (each is one live `/v1/assess` call)

Discard everything from the first ~30 s warm-up before computing statistics.
Report per stage: N, median, p95, min, max (use the same linear-interpolation
percentile as the backend benchmark so the tables are comparable).

## Record with the numbers

- Unity version (Project Settings → About, or the `ProjectVersion.txt`)
- Sentis version: **2.1.3** (shown on the in-app HUD label)
- Quest model: Quest 3
- Active ONNX model: `medium.onnx` (label "YOLOv8-medium")
- Input resolution: 640×640

## What each timer measures

- **inference** — from just before `m_engine.ScheduleIterable` to schedule
  completion (`SentisInferenceRunManager`); spans multiple frames.
- **post_processing** — output decode + confidence filtering + candidate
  construction + NMS, in `SentisInferenceUiManager.DrawUIBoxes`. **Stops before**
  UI box/arrow drawing; the O(detections) rip-summarization loop is not included.
  The rip snapshot (PNG capture + save) runs after this section. In builds made
  before that change it ran inside the timed section, so re-collect any
  post_processing samples taken with an older build.
- **http_round_trip** — around `UnityWebRequest.SendWebRequest()` in
  `RipFinderAgentClient`. Client-observed HTTP round-trip (transport +
  serialization + full server processing incl. Claude), **not** pure network
  latency.

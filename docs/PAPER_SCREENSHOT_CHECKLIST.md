# Paper Screenshot Checklist

Four figures for the paper. Figures 1, 3, 4 are captured manually from
Unity/Quest; Figure 2 is the rendered architecture diagram (see
`docs/paper_architecture.mmd`). Do **not** capture the forecast WebView or the
safety-poster screen — not used in this paper.

Active configuration to capture against: model `medium.onnx` (single-class,
5-channel), input 640×640, runtime confidence ≥ 0.25, NMS IoU 0.5.

---

## FIGURE 1: Teaser (headset + AR view)

Desired content: Meta Quest 3 headset / user context **plus** a representative
AR passthrough view with a rip detection overlay.

Status: **no teaser asset exists in the project or a paper folder** (searched;
none found). This must be manually composed or captured — e.g. a photo of the
user wearing the Quest 3 composited with an in-headset AR capture, or a single
representative in-headset frame.

---

## FIGURE 3: Positive case

Must show, together:

- an actual detection box over the rip region,
- the assessment panel with: risk level, risk score, and the Claude-generated
  summary / recommendation / safety note.

**Before capture, verify provenance in the Unity console / logcat:**

```
[RipFinder Assessment] source=claude
```

(Do **not** caption fallback text as Claude output. If you instead see
`source=rule_engine_fallback`, the backend had no working Claude call — fix that
and re-run before capturing.)

Input: use the already-validated real rip test image, or a live Quest detection.
If headset capture is unavailable, capture from the Unity **Game** view (not the
Scene view).

Crop / remove before use:
- Unity Hierarchy, Inspector, Console, Project panels
- OS menu bar, window chrome
- usernames, email, local file paths, machine name, Git branch name

---

## FIGURE 4: Negative case

Must show:

- a no-rip input/result,
- a LOW risk assessment on the panel,
- confidence and detection count at zero if they are displayed,
- the assessment panel populated (summary/recommendation/safety note).

Same cropping rules as Figure 3.

---

## Double-blind check (every screenshot)

Confirm none of the following is visible anywhere in the frame (including title
bars, paths, and logs left in view):

- `nithu`
- `nithyasri`
- `palanisamy`
- `Cal Poly`
- any local file path (e.g. `/Users/...`)
- any email address
- any GitHub username
- any organization ID

Tip: capture into a clean, full-screen Game view; if any editor chrome or path
is present, crop it out before the figure goes in the paper.

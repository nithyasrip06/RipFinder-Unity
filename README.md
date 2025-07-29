where should features section be
# RipFinder-Unity

An Augmented Reality application built with **Unity** and **Meta Quest 3** to visualize rip current detection in real time. The system integrates a **YOLOv8 ONNX model** using **Unity Sentis** and overlays bounding boxes on the passthrough camera feed to enhance beach safety.

> ⚠️ **Important**: This project has only been tested with **Unity `6000.0.47f1`**. Using other versions (e.g., Unity 2022.x) may cause compatibility issues with Meta APIs or Unity Sentis.

---

## 🧪 Tested With

- **Unity Editor**: `6000.0.47f1`
- **Meta Quest 3**

---

## 📦 Packages / Dependencies

Ensure the following packages are installed:

- `com.meta.xr.mrutilitykit` (Meta MRUK, **v74.0.0** or higher)
- `com.unity.sentis` (Unity Sentis, **v2.1.1**)

---

## 🛠 Setup Instructions

```bash
# 1. Install Git LFS
git lfs install

# 2. Clone the repository
git clone https://github.com/nithyasrip06/RipFinder-Unity.git

# 3. In Unity Hub, click "Add project from disk" and open 
RipFinder-Unity/Unity-PassthroughCameraApiSamples
```
---

## 🎮 How to Run the RipDetection Scene

Do **not** open the default `StartScene`.

Instead, open this scene directly in Unity:

```
Assets/PassthroughCameraApiSamples/RipDetection/RipDetectionScene.unity
```

This is the main custom scene that runs the YOLOv8 model on the passthrough feed.

---

## 📱 Build Configuration (for Meta Quest 3)

1. Open **File > Build Profiles** and set the platform to **Android**
2. Go to **Edit > Project Settings > XR Plug-in Management**
   - Enable **OpenXR** for Android
3. In the **Scene List**, make sure `RipDetectionScene.unity` is **checked**
   - Unity will load the **first checked scene** at runtime
4. Connect your Quest 3 (in developer mode), and click **Build & Run**

---

## 🔐 Required Permissions

Make sure the following permissions are granted to the app on your Quest 3:

- `android.permission.CAMERA`
- `com.horizonos.permission.HEADSET_CAMERA`

You can do this by modifying the `AndroidManifest.xml` file located at 
```
Assets/Plugins/Android/AndroidManifest.xml
```

> ❗ If you deny these permissions at first launch, you will need to **uninstall and reinstall** the app. Unity does not support re-requesting them by default.

---

## ✅ Features

### Real-time Rip Current Detection  
Detects rip currents using a YOLOv8 ONNX model running locally on-device with Unity Sentis.

### Passthrough AR View  
Visualizes bounding boxes directly on the Meta Quest 3 passthrough camera feed for intuitive AR overlays.

### Safety Guidance Poster  
When a rip current is detected, a **safety poster** appears after a short delay to inform users of what actions to take.  

### Hand and Controller Support  
Fully supports both Meta Quest hand tracking and controllers:
- **Pinch to start** or press [A] on controller
- **Distance grab** to reposition the poster using either hand gestures or controller triggers

### Performance Feedback  
Inference time and frame rate are continuously calculated and displayed in real time on the bottom UI panel, providing insight into model speed and efficiency.

---

## 📝 Assets and Attribution
- Rip Current Safety Poster from NOAA: [https://www.weather.gov/safety/ripcurrent-infographic](https://www.weather.gov/safety/ripcurrent-signs-brochures)
- Source: NOAA (public domain)

---

## 📚 Acknowledgments

This project builds on Meta’s [Unity-PassthroughCameraApiSamples](https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples) and was developed under the guidance of **Dr. Khan** as part of a research initiative at Cal Poly San Luis Obispo.

---

## 📄 License

- Scripts in `Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model` are licensed under **MIT**
- All other components follow the **Oculus License**

See the `LICENSE` file for details.

---

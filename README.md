# RipFinder-Unity

An Augmented Reality application built with **Unity** and **Meta Quest 3** to visualize rip current detection in real time. The system integrates a **YOLOv8 ONNX model** using **Unity Sentis** and overlays bounding boxes on the passthrough camera feed to enhance beach safety.

> ⚠️ **Important**: This project has only been tested with **Unity `6000.0.47f1`**. Using other versions (e.g., Unity 2022.x) may cause compatibility issues with Meta APIs or Unity Sentis.

---

## Demo
[![Demo Video](./unity-passthroughcameraapisamples/assets/demo-thumbnail.png)](https://drive.google.com/file/d/1lbjzIdrEDZhnlRlkDCeo5Wo3o5meqy6E/view?usp=sharing)

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
- `horizonos.permission.HEADSET_CAMERA`

You can do this by modifying the `AndroidManifest.xml` file located at 
```
Assets/Plugins/Android/AndroidManifest.xml
```

> ❗ If you deny these permissions at first launch, you will need to **uninstall and reinstall** the app. Unity does not support re-requesting them by default.

---

## ✅ Features

### Real-time Rip Current Detection  
Detects rip currents using a YOLOv8 ONNX model running locally on-device with Unity Sentis.
Supports multi-class detection and distinguishes between rip currents and sediment rip currents.

### Passthrough AR View  
Visualizes bounding boxes directly on the Meta Quest 3 passthrough camera feed for intuitive AR overlays.

### Safety Guidance Poster  
When a rip current is detected, a **safety poster** appears after a short delay to inform users of what actions to take.  

### Dynamic Surf Forecast Web Page
Integrates a live, auto-updating NOAA surf forecast for San Luis Obispo.

### Screenshot Capture on Detection
The app automatically saves a screenshot of the passthrough view to the headset’s internal storage when a rip current is detected. These in-app screenshots do **not** include overlays; to capture bounding boxes and UI, use the native Quest Camera to record video or take photos.

### Hand and Controller Support  
Fully supports both Meta Quest hand tracking and controllers:
- **Pinch to start** or press [A] on controller
- **Press [B]** to toggle between different YOLOv8 models (e.g., nano / medium / multi-class)
- **Distance grab** to reposition the poster using either hand gestures or controller triggers

### Oriented Arrows for Direction
An arrow overlay is anchored to each detection, showing the direction of the detected rip current in AR space for better spatial awareness.

### Performance Feedback  
Inference time and frame rate are continuously calculated and displayed in real time on the bottom UI panel, providing insight into model speed and efficiency.

---

## 🎥 Recording & Photos with the Native Quest Camera
You can use the built-in Quest camera to record video or take photos of the app. 

> ⚠️ **Note**: Native Quest recordings and photos include the full AR view (bounding boxes and UI overlays). The automatic in-app screenshots only capture the passthrough camera image and do not include overlays due to platform limitations. To avoid duplicate media, you can disable in-app screenshots. 

**Record a video**
1. Press the Meta button on right controller to open the Universal Menu.
2. Open Camera.
3. Choose Record video to start.
   
**Take a photo**
1. Press the Meta button.
2. Open Camera.
3. Select Take photo.

---

## 📸 Accessing Captured Screenshots (Passthrough)
When a rip current is detected, a screenshot of the passthrough view is automatically saved to your Quest's internal storage using Unity's persistentDataPath. You can retrieve these screenshots using ADB (Android Debug Bridge) and USB Developer Mode. 

> ⚠️ **Note**: Due to Meta Quest’s rendering limitations, bounding boxes are not included in the screenshots. Only the underlying passthrough camera image is captured and saved.

1. Enable Developer Mode on Your Meta Quest 3
   - Open the Meta Quest mobile app
   - Go to: Devices > Developer Mode
   - Toggle Developer Mode ON
2. Install ADB (Android Debug Bridge)
   - On macOS:
     ```
     brew install android-platform-tools
     ```
   - On Windows:
     Download [SDK Platform Tools from Google](https://developer.android.com/studio/releases/platform-tools)
     Extract and remember the folder path
3. Connect Your Quest to Your Computer
   - Connect your Meta Quest 3 to your computer via USB-C
   - Put on your headset and accept the **Allow USB Debugging** prompt
4. Verify ADB Connection
   - In Terminal, run
     ```
     adb devices
     ```
   - Expected output:
     ```
     List of devices attached
     XXXXXXXXXXXX	device
     ```
5. Find Your Unity App’s Package Name
   - In Unity Editor, go to **Edit > Project Settings > Player > Android**
   - Under **Other Settings**, locate: **Package Name**
   - This is the path Unity uses for Application.persistentDataPath.
6. Pull Saved Images from Quest
   - Once you've confirmed the package name, run this command:
     ```
     adb pull /sdcard/Android/data/com.package.name/files/ ./screenshots/
     ```
   - This copies all screenshots to a folder named ./screenshots/ on your local machine.
   - Run the command again to retrieve new screenshots after future detections.
     
---

## 📝 Assets and Attribution
- Rip Current Safety Poster (NOAA): [https://www.weather.gov/safety/ripcurrent-signs-brochures](https://www.weather.gov/safety/ripcurrent-signs-brochures)
- Surf Zone Forecast (NWS Los Angeles/Oxnard – LOX, SRF): [https://forecast.weather.gov/product.php?site=LOX&issuedby=LOX&product=SRF&format=CI&version=1&glossary=1&highlight=on](https://forecast.weather.gov/product.php?site=LOX&issuedby=LOX&product=SRF&format=CI&version=1&glossary=1&highlight=on)

Source: NOAA / National Weather Service. U.S. Government work; generally public domain unless otherwise noted.

---

## 📚 Acknowledgments

This project builds on Meta’s [Unity-PassthroughCameraApiSamples](https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples) and was developed under the guidance of **Dr. Khan** as part of a research initiative at Cal Poly San Luis Obispo.

---

## 📄 License

- Scripts in `Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model` are licensed under **MIT**
- All other components follow the **Oculus License**

---

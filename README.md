# 🛡️ Secure File Monitor

Secure File Monitor is a state-of-the-art Windows application designed for high-security environments. It provides real-time oversight of file system integrity, leveraging local AI for deep content analysis without compromising privacy.

![Secure File Monitor UI](./assets/main_ui.png)

## 🌟 Core Features

### 🔍 Semantic Content Search
Don't just search for filenames; search for **meanings**. Our integrated BERT-based embedding engine allows you to find files based on their actual content.
- *Example:* Searching for "suspicious network logs" will find relevant files even if they are named "Untitled_1.txt".

### 🎙️ AI Transcription & Vision
- **Whisper OCR**: High-fidelity transcription of audio and video files. Supports GPU acceleration (CUDA) for blazing-fast local processing.
- **Video Description (VLM)**: Visual Language Models describe the contents of video files, making visual data searchable.
- **Privacy First**: All AI processing is 100% local. No data ever leaves your machine.

### 🚄 Live System Activity
Monitors your file system at the kernel level using ETW (Event Tracing for Windows).
- **Process Tracking**: See exactly which background process (e.g., `cmd.exe`, `powershell.exe`) is creating, modifying, or deleting your files.
- **Smart Filtering**: Automatically hides noisy system updates while highlighting critical user changes.
- **True Create Logic**: Distinguishes between opening a file and actually creating one.

### 🛡️ Forensic Integrity Monitoring
- **Smart Integrity Scanning**: Detects files created, deleted, or modified while the application was offline.
- **Merkle Tree Granular Diffing**: Identifies exactly which blocks of a file were modified (4MB block granularity), enabling forensic analysis of tampered data.
- **Persistent Forensic Logs**: All offline changes are logged to an encrypted database with a strong audit trail that survives application restarts.
- **Background Hashing**: Efficiently processes large files (>50MB) in the background so your UI remains smooth using SHA256.

---

## 🚀 Getting Started

### Prerequisites
- **Windows 10/11** (Admin privileges required for ETW monitoring)
- **NVIDIA GPU** (Optional, recommended for Whisper CUDA acceleration)
- **.NET 10 Runtime**

### Installation
1. Download the latest release from the [Releases](https://github.com/dparksports/SecureFileMonitor/releases) page.
2. Extract the archive to a secure directory.
3. Run `SecureFileMonitor.UI.exe` as Administrator.
4. On first launch, the app will download necessary AI models (Whisper/BERT) locally.

---

## 🛠️ Technology Stack
- **C# / WPF**: Native Windows performance and aesthetics.
- **CommunityToolkit.Mvvm**: Robust architecture.
- **Whisper.net**: Fast, local audio transcription.
- **Microsoft.ML.OnnxRuntime**: Efficient local model inference.
- **SQLite (SQLCipher)**: Encrypted storage for all metadata and history.
- **TraceEvent**: Real-time kernel event processing.

---

## 📄 License
This project is licensed under the **Apache License 2.0**. See the [LICENSE](./LICENSE) file for details.

Developed with ❤️ for secure environments.

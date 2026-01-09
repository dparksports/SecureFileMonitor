# Secure File Monitor

**Secure File Monitor** is a high-security, native Windows application designed to provide comprehensive, real-time monitoring of all file activities across mounted drives. Built with a focus on integrity verification and forensic visibility, it tracks creations, modifications, and deletions with precision.

## Key Features

- **Real-Time Monitoring**: Leverages ETW (Event Tracing for Windows) to capture every file operation across the system with minimal overhead.
- **Vault Verification**: Maintains a local database of file states, allowing for instant detection of unauthorized changes or "silent" modifications.
- **Deep Content Analysis**:
  - **Whisper AI Transcription**: Automatically transcribes audio and video files using OpenAI's Whisper models (locally).
  - **Semantic Search**: Index and search files by their content description and transcriptions using BERT embeddings.
  - **GPU Acceleration**: Built-in support for CUDA-enabled GPUs for lightning-fast AI processing.
- **Advanced Filtering**:
  - Filter by file type, system directories (Windows/System32), and specific extensions like `.psd1` or `.pnf`.
  - **Process-Based Ignoring**: Suppress noise by ignoring trusted processes directly from the live feed.
- **Large File Handling**: Background SHA256 hashing for files larger than 50MB with real-time progress and ETA reporting.
- **Forensic Detail**: Track not just what changed, but who changed it (User SID/Name) and which process was responsible.

## High Security Mode

The application operates in a "High Security Mode," prioritizing data integrity and local processing. All AI models run locally on your machine—no data is sent to the cloud.

## Installation & Requirements

- **OS**: Windows 10/11
- **Framework**: .NET 8.0 / WPF
- **Dependencies**: 
  - FFmpeg (included/downloaded automatically)
  - SQLite (Local encrypted database)
  - Whisper.net (Local AI runtime)

## License

This project is licensed under the Apache License 2.0. See the [LICENSE](LICENSE) file for details.

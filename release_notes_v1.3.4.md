# Release v1.3.4 - Release Size Optimization

This release optimizes the distribution size by temporarily removing GPU acceleration components while preserving core functionality and Threaded Hashing.

## 📦 Optimization
- **Size Reduction**: Reduced release size from >600MB to ~150MB by removing NVIDIA CUDA libraries and multi-platform runtimes.
- **GPU Hashing**: Temporarily disabled "Use GPU" feature. The option is hidden in the UI.
- **Whisper AI**: Preserved CPU-based local speech recognition (Whisper.net) as requested.

## 🐛 Bug Fixes & Improvements (from v1.3.3)
- **Runtime Stability**: Fixed crashes related to Multi-threading and Dependency Injection.
- **UI Persistence**: Fixed "Resume" button disappearing after restart.
- **UI Polish**: Fixed unreadable buttons in Dark Mode and filter layout issues.

# Secure Files

![Secure Files Preview](assets/app_preview.png?v=2)

## Overview

**Secure Files** is a robust file integrity monitoring solution designed for Windows. It provides real-time tracking of file changes, cryptographic hashing for integrity verification, and audit logging to detect unauthorized modifications.

Built with a focus on performance and security, it features a modern, dark-themed UI ("Auto Command" style) and is optimized for efficient background processing.

## Key Features

*   **File Integrity Vault**: Maintains a database of file states (Size, Modified Date, Hash) to detect tampering.
*   **Cryptographic Verification**: Computes SHA-256 hashes for files to ensure 100% integrity assurance.
*   **Smart Scanning**:
    *   **Skip Logic**: Automatically skips system directories like `Windows` and `Program Files` to focus on user data.
    *   **Multi-threading**: Utilizes CPU multi-threading for fast parallel scanning.
*   **Live Monitoring**: Tracks real-time file system events (Create, Modify, Delete, Rename).
*   **Audit Logging**: detailed logs of all file activities for security audits.
*   **Ignore List**: Customizable rules to exclude specific files, folders, or extensions from monitoring.

## Getting Started

### Installation
1.  Download the latest `SecureFileMonitor_Release.zip` from the [Releases](https://github.com/dparksports/SecureFileMonitor/releases) page.
2.  Extract the archive to a folder of your choice.
3.  Run `SecureFileMonitor.UI.exe`.

### Usage
1.  **Select Drives**: Choose the drives you wish to monitor from the Dashboard.
2.  **Scan Full**: Click "Scan Full" to build the initial integrity baseline.
3.  **Monitor**: Switch to the "Monitor Live" tab to watch for real-time changes.
4.  **Review Changes**: The "Vault" tab highlights files that have changed since the last scan.

## Settings
*   **Use Multi-threading**: Toggle parallel processing for faster scans implementation.
*   **Skip Windows / Program Files**: Enable these filters to exclude system files and improve performance.
*   **Verify GPU Hash**: (Optional) Experimental verification mode.

## System Requirements
*   **OS**: Windows 10/11 (64-bit)
*   **Runtime**: .NET 10.0 (included in release or installed separately)

## License
apache license

---
_made with ❤️ in california_

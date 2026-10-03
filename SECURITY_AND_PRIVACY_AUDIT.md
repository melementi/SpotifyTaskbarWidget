# Security and Privacy Audit - Spotify Taskbar Widget

## Overview

This document outlines the security and privacy aspects of the Spotify Taskbar Widget, based on a source code review. The application is a lightweight Windows 11 utility written in C# and WPF that fetches lyrics for the currently playing Spotify song and displays them on the taskbar.

## Privacy

The application respects user privacy and operates with minimal data collection and transmission.

*   **No Telemetry or Tracking:** The application does not include any analytics, tracking, or crash reporting software. No data is sent to the developer.
*   **Third-Party Services:** The only external service contacted is LRCLIB (`https://lrclib.net`), a free and open-source lyrics provider.
*   **Data Sent:** To retrieve lyrics, the application sends the following data to LRCLIB in the URL parameters of standard HTTP GET requests:
    *   Track Title
    *   Artist Name
    *   Track Duration (in seconds)
*   **No Authentication Required:** The application does not require a Spotify login, OAuth token, or any API keys. It reads media information directly from the local Windows media controls (`GlobalSystemMediaTransportControlsSessionManager`).
*   **Local Storage:**
    *   Settings are stored locally in `%LOCALAPPDATA%\SpotifyTaskbarWidget\config.json`.
    *   Downloaded lyrics are cached in `%LOCALAPPDATA%\SpotifyTaskbarWidget\lyrics`. The filenames in this cache are generated using a SHA256 hash of the track query (artist, title, and duration), which provides a mild layer of obfuscation, though the file contents are plain JSON containing the lyrics.

## Security

The application uses standard .NET libraries and safe practices.

*   **Network Security:** All communication with LRCLIB is performed over secure HTTPS (`https://lrclib.net/`).
*   **Input Validation & Data Processing:**
    *   URL parameters sent to LRCLIB are properly escaped using `Uri.EscapeDataString()`.
    *   JSON responses are parsed securely using `System.Text.Json.JsonSerializer`.
    *   Lyrics (LRC format) are parsed using a compiled Regular Expression (`LrcParser.cs`). The regex pattern `\G\s*\[(\d{1,3}):(\d{1,2})(?:[.:](\d{1,3}))?\]` is straightforward and not vulnerable to ReDoS (Regular Expression Denial of Service).
*   **Local File System Interaction:**
    *   The application handles exceptions gracefully when reading or writing to the local cache or config file (e.g., catching `IOException`, `UnauthorizedAccessException`, and `JsonException`), ensuring that local file system issues do not crash the application or expose sensitive information.
*   **Privileges:** The application is designed to run in user space and does not require administrator rights to install or run.
*   **Window Management:** The widget interacts with low-level Windows APIs (`user32.dll`) via P/Invoke to position itself on the taskbar. These calls are standard for desktop customization tools and do not pose a direct security threat.

## Conclusion

The Spotify Taskbar Widget is a well-behaved, privacy-respecting application. It does not collect personal data, requires no authentication, and communicates only with a single, necessary third-party lyrics service over a secure connection. The local data storage and data processing are implemented safely.

# AfterCopy

**AfterCopy** is a lightweight, real-time clipboard monitor and overlay utility built with C# and WPF (Windows Presentation Foundation). It provides an unobtrusive, always-on-top visualization of your system clipboard content, designed to improve workflow efficiency without interrupting your focus.

## 🚀 Overview

AfterCopy sits quietly in the bottom-right corner of your screen. Whenever you copy text from any application, the overlay updates instantly to show you what was captured. It is engineered to be **passive**, meaning it will never steal keyboard focus or appear in your Alt-Tab switcher, allowing you to continue typing and working seamlessly.

## ✨ Key Features

*   **Real-Time Monitoring:** Instantly detects clipboard changes system-wide using native Win32 API hooks (`WM_CLIPBOARDUPDATE`).
*   **Unobtrusive Overlay:**
    *   **Always on Top:** Stays visible above other windows.
    *   **Transparent Design:** Semi-transparent dark background for readability without blocking the view completely.
    *   **Click-Through / No Focus:** Uses extended window styles (`WS_EX_NOACTIVATE`) to ensure it never steals focus from your active application.
*   **Smart Positioning:** Automatically anchors itself to the bottom-right corner of the primary display's working area, respecting the taskbar position.
*   **Content Awareness:** Displays text content immediately and handles non-text (images/files) updates gracefully.
*   **Minimal Footprint:** Lightweight application with no taskbar presence (`WS_EX_TOOLWINDOW`).

## 🛠️ Prerequisites

*   **OS:** Windows 10 or Windows 11.
*   **Runtime:** .NET 8.0 SDK (or newer).

## 📦 Getting Started

### 1. Clone the Repository
```bash
git clone https://github.com/yourusername/AfterCopy.git
cd AfterCopy
```

### 2. Build the Project
You can build the project using the .NET CLI:
```bash
dotnet build -c Release
```

### 3. Run the Application
```bash
dotnet run
```
*Note: Since this application uses Windows-specific APIs (`user32.dll`), it must be run on a Windows operating system.*

## 🔧 Technical Implementation

AfterCopy demonstrates advanced WPF interop techniques to achieve its unique behavior:

*   **Clipboard Hooks:** Implements `AddClipboardFormatListener` from `user32.dll` to receive system messages whenever the clipboard content changes, avoiding inefficient polling timers.
*   **Window Interop:** Uses `WindowInteropHelper` and `HwndSource` to hook into the Windows message loop (`WndProc`).
*   **Extended Styles:** Applies `WS_EX_NOACTIVATE` (0x08000000) and `WS_EX_TOOLWINDOW` (0x00000080) via `SetWindowLong` to manage window behavior at the OS window manager level.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📄 License

This project is licensed under the MIT License.

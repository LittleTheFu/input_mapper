# Input Mapper Desktop

Windows 11 desktop shell for the current input visualizer prototype.

## Build and run

Requirements: .NET 9 SDK and Microsoft Edge WebView2 Runtime.

From this directory, run:

```powershell
dotnet run
```

The WPF host embeds the existing HTML interface with WebView2. When **开始映射** is enabled, Windows low-level keyboard and mouse hooks forward physical key and mouse events to the visualizer. The hooks are removed when mapping is stopped or the window closes.

## Structure

- `MainWindow.xaml` hosts WebView2.
- `GlobalInputCapture.cs` listens for global keyboard and mouse events without suppressing them.
- `web/index.html` and its sprite images are copied into the app's `web` output folder.

# Input Mapper Desktop

Windows 11 desktop shell for the current input visualizer prototype.

## Build and run

Requirements: .NET 9 SDK and Microsoft Edge WebView2 Runtime.

From this directory, run:

```powershell
dotnet run
```

The WPF host embeds the HTML interface with WebView2. Global keyboard and mouse capture starts automatically after the interface loads and stops when the window closes. Physical input events are forwarded to the visualizer without suppressing them.

## Structure

- `MainWindow.xaml` hosts WebView2.
- `GlobalInputCapture.cs` listens for global keyboard and mouse events without suppressing them.
- `web/index.html` and its sprite images are copied into the app's `web` output folder.

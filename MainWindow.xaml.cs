using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace InputMapperDesktop;

public partial class MainWindow : Window
{
    private readonly GlobalInputCapture _capture = new();
    private bool _ready;
    private DateTime _lastMouseMove = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += (_, _) => _capture.Stop();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await Browser.EnsureCoreWebView2Async();
            Browser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _capture.Input += OnNativeInput;
            _ready = true;
            var page = Path.Combine(AppContext.BaseDirectory, "web", "index.html");
            Browser.Source = new Uri(page);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "无法启动 WebView2。请先安装 Microsoft Edge WebView2 Runtime。\n\n" + ex.Message,
                "Input Mapper", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var json = JsonDocument.Parse(e.WebMessageAsJson);
            var root = json.RootElement;
            if (root.TryGetProperty("type", out var type) && type.GetString() == "capture" &&
                root.TryGetProperty("active", out var active))
            {
                if (active.GetBoolean())
                {
                    try { _capture.Start(); }
                    catch { _ = Browser.CoreWebView2.ExecuteScriptAsync("window.dispatchEvent(new Event('captureerror'))"); }
                }
                else _capture.Stop();
            }
        }
        catch (JsonException)
        {
            // Ignore messages that are not part of the small host bridge.
        }
    }

    private void OnNativeInput(NativeInputEvent input)
    {
        if (!_ready || Browser.CoreWebView2 is null) return;
        if (input.Kind == "mouse" && input.Phase == "move")
        {
            var now = DateTime.UtcNow;
            if ((now - _lastMouseMove).TotalMilliseconds < 30) return;
            _lastMouseMove = now;
        }
        var json = JsonSerializer.Serialize(input, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        _ = Browser.CoreWebView2.ExecuteScriptAsync(
            $"window.dispatchEvent(new CustomEvent('nativeinput',{{detail:{json}}}));");
    }
}

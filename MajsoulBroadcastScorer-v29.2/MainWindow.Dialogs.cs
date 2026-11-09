using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace MajsoulBroadcastScorer.V27;

public partial class MainWindow
{
    private readonly Dictionary<string, Window> _toolWindows = new();
    private bool _isAuthenticated;
    private bool _closingToolWindows;
    private Task? _previewInitialization;

    private void OpenObservationControl_Click(object sender, RoutedEventArgs e) =>
        ShowToolWindow("control", "观战登录", ObservationControlContent, 760, 820);

    private void OpenObservationMode_Click(object sender, RoutedEventArgs e) =>
        ShowToolWindow("mode", "延迟/实时观战", ObservationModeContent, 560, 440);

    private void OpenReports_Click(object sender, RoutedEventArgs e) =>
        ShowToolWindow("reports", "对局记录", ReportsContent, 980, 740);

    private async void OpenPreview_Click(object sender, RoutedEventArgs e)
    {
        ShowPreviewWindow();
        await EnsurePreviewInitializedAsync();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ShowPreviewWindow();
        await EnsurePreviewInitializedAsync();
    }

    private void ShowPreviewWindow() =>
        ShowToolWindow("preview", "实时预览", PreviewContent, 960, 610, floatingPreview: true);

    private Task EnsurePreviewInitializedAsync() =>
        _previewInitialization ??= InitializePreviewAsync();

    private void OpenPlayerDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PlayerEditorViewModel editor })
            return;
        var content = new ContentControl
        {
            Content = editor,
            ContentTemplate = (DataTemplate)FindResource("PlayerDetailsTemplate"),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        ShowToolWindow($"player-{editor.Seat}", $"{editor.SeatLabel}设置", content, 780, 600);
    }

    private void ShowToolWindow(
        string key, string title, FrameworkElement content, double width, double height,
        bool floatingPreview = false)
    {
        if (_closingToolWindows)
            return;
        if (!_toolWindows.TryGetValue(key, out var window))
        {
            // 移动现有控件，保留登录、对局列表、草稿及对应事件处理。
            if (content.Parent is Panel parent)
                parent.Children.Remove(content);
            UIElement body = floatingPreview
                ? content
                : new ScrollViewer
                {
                    Content = content,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                };
            var shell = new Grid();
            shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            if (!floatingPreview)
            {
                var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 22) };
                heading.Children.Add(new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeights.SemiBold });
                var description = key switch
                {
                    "control" => "登录后输入大会 ID，选择要观战的对局。",
                    "mode" => "选择下一场的观战方式，设置 OBS 画面的播出延迟。",
                    "reports" => "查看已收到的逐局结算，保存或打开详细报告。",
                    _ => "调整选手资料与头像效果，完成后点击确认修改。"
                };
                heading.Children.Add(new TextBlock { Text = description, Margin = new Thickness(0, 7, 0, 0),
                    Foreground = (System.Windows.Media.Brush)FindResource("AppMutedTextBrush"), TextWrapping = TextWrapping.Wrap });
                shell.Children.Add(heading);
            }
            Grid.SetRow(body, 1);
            shell.Children.Add(body);
            window = new Window
            {
                Title = $"{title} · 0.292 测试版",
                Owner = this,
                Width = width,
                Height = height,
                MinWidth = floatingPreview ? 480 : key == "reports" ? 720 : key == "control" ? 600 : key.StartsWith("player-") ? 680 : 480,
                MinHeight = floatingPreview ? 320 : 280,
                Background = Background,
                FontFamily = FontFamily,
                FontSize = FontSize,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true,
                Foreground = Foreground,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
                Topmost = floatingPreview,
                Content = new Border { Padding = new Thickness(floatingPreview ? 16 : 24), Child = shell }
            };
            var toolWindow = window;
            if (floatingPreview)
                toolWindow.SourceInitialized += (_, _) => DisablePreviewCloseButton(toolWindow);
            toolWindow.Closing += (_, e) =>
            {
                if (_closingToolWindows)
                    return;
                e.Cancel = true;
                if (floatingPreview) return;
                if (key == "control")
                    PasswordInput.Clear();
                // 关闭事件结束后再隐藏，避免取消关闭时恢复窗口可见状态。
                toolWindow.Dispatcher.BeginInvoke(() =>
                {
                    if (!_closingToolWindows) toolWindow.Hide();
                });
            };
            _toolWindows.Add(key, toolWindow);
        }
        if (!window.IsVisible)
            window.Show();
        window.Activate();
    }

    private void SetAuthenticationState(bool authenticated)
    {
        _isAuthenticated = authenticated;
        if (!authenticated)
        {
            _contestGames.Clear();
            AccountStatusText.Text = "雀魂账号：未登录";
        }
        LoginRequiredText.Visibility = authenticated ? Visibility.Collapsed : Visibility.Visible;
        UpdateConnectionIndicator();
        UpdateControlButtons();
    }

    private static void DisablePreviewCloseButton(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var menu = GetPreviewSystemMenu(handle, false);
        if (menu == IntPtr.Zero) return;
        EnablePreviewMenuItem(menu, 0xF060, 0x0001);
        DrawPreviewMenuBar(handle);
    }

    [DllImport("user32.dll", EntryPoint = "GetSystemMenu")]
    private static extern IntPtr GetPreviewSystemMenu(IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool revert);

    [DllImport("user32.dll", EntryPoint = "EnableMenuItem")]
    private static extern uint EnablePreviewMenuItem(IntPtr menu, uint item, uint flags);

    [DllImport("user32.dll", EntryPoint = "DrawMenuBar")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DrawPreviewMenuBar(IntPtr window);

    private void ContestGameSelection_Changed(object sender, SelectionChangedEventArgs e) =>
        UpdateControlButtons();

    private void CloseToolWindows()
    {
        _closingToolWindows = true;
        foreach (var window in _toolWindows.Values)
            window.Close();
        PasswordInput.Clear();
        if (PreviewWebView.CoreWebView2 is { } core)
            core.WebMessageReceived -= PreviewWebMessageReceived;
        PreviewWebView.Dispose();
    }
}

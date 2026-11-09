using System.Windows;
using System.Windows.Controls;

namespace MajsoulBroadcastScorer.V27;

public partial class MainWindow
{
    private readonly Dictionary<string, Window> _toolWindows = new();
    private bool _isAuthenticated;
    private bool _closingToolWindows;
    private Task? _previewInitialization;

    private void OpenObservationControl_Click(object sender, RoutedEventArgs e) =>
        ShowToolWindow("control", "观战控制", ObservationControlContent, 760, 640);

    private void OpenObservationMode_Click(object sender, RoutedEventArgs e) =>
        ShowToolWindow("mode", "观战方式", ObservationModeContent, 560, 370);

    private void OpenReports_Click(object sender, RoutedEventArgs e) =>
        ShowToolWindow("reports", "对局记录", ReportsContent, 640, 340);

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
        ShowToolWindow($"player-{editor.Seat}", $"{editor.SeatLabel}设置", content, 780, 450);
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
            window = new Window
            {
                Title = $"{title} · 0.292 测试版",
                Owner = this,
                Width = width,
                Height = height,
                MinWidth = floatingPreview ? 480 : 460,
                MinHeight = floatingPreview ? 320 : 280,
                Background = Background,
                Foreground = Foreground,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
                Topmost = floatingPreview,
                Content = new Border { Padding = new Thickness(16), Child = body }
            };
            var toolWindow = window;
            toolWindow.Closing += (_, e) =>
            {
                if (_closingToolWindows)
                    return;
                e.Cancel = true;
                if (key == "control")
                    PasswordInput.Clear();
                toolWindow.Hide();
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

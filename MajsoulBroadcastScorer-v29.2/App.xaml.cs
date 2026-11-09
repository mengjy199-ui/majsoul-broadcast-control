using System.Windows;
using System.IO;
using MajsoulBroadcastScorer.Core.V27;
using MajsoulBroadcastScorer.V27.Services;

namespace MajsoulBroadcastScorer.V27;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "Local\\MajsoulBroadcastScorer.v29.SingleInstance";
    private Mutex? _singleInstanceMutex;
    private BroadcastApiClient? _apiClient;
    private ObsOverlayServer? _obsServer;
    private BackendProcessManager? _backendManager;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("已有 v29 系列控制中心正在运行，请关闭后再启动 v29.2。", "无法重复启动", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(2);
            return;
        }

        try
        {
            var settingsStore = new SettingsStore(AppContext.BaseDirectory);
            var settings = await settingsStore.LoadAsync();
            _backendManager = new BackendProcessManager(AppContext.BaseDirectory);
            _backendManager.Start();
            _obsServer = new ObsOverlayServer();
            _obsServer.Start();
            _apiClient = new BroadcastApiClient(controlToken: _backendManager.ControlToken);

            var window = new MainWindow(settingsStore, settings, _apiClient, _obsServer, _backendManager);
            MainWindow = window;
            window.Show();
            if (!string.IsNullOrWhiteSpace(settingsStore.LoadWarning))
                MessageBox.Show(settingsStore.LoadWarning, "配置恢复提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            _apiClient.Start();
        }
        catch (Exception exception)
        {
            var root = exception;
            while (root.InnerException is not null)
                root = root.InnerException;
            try
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "v29.2-startup-error.log"), exception.ToString());
            }
            catch
            {
            }
            MessageBox.Show($"程序启动失败：{root.Message}", "雀魂直播控制中心 v29.2", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void App_DispatcherUnhandledException(
        object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        if (e.Exception is OutOfMemoryException)
            return;
        try
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "v29.2-runtime-error.log"),
                $"{DateTime.Now:O}{Environment.NewLine}{e.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
        }

        e.Handled = true;
        MessageBox.Show(
            $"程序遇到一个错误，但已阻止闪退。{Environment.NewLine}{e.Exception.Message}{Environment.NewLine}{Environment.NewLine}详细记录：v29.2-runtime-error.log",
            "雀魂直播控制中心 v29.2",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }


    protected override void OnExit(ExitEventArgs e)
    {
        try { _apiClient?.SendControlAsync("host.shutdown").Wait(TimeSpan.FromSeconds(1)); } catch { }
        _apiClient?.Dispose();
        _obsServer?.Dispose();
        _backendManager?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}

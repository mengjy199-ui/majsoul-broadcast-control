using MajsoulBroadcastScorer.Core.V27;
using System.IO;

namespace MajsoulBroadcastScorer.V27.Services;

public sealed class SettingsStore
{
    private readonly string _baseDirectory;
    private readonly string _settingsPath;
    private readonly string _backupPath;
    private readonly string _assetsDirectory;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private bool _loadedPrimarySuccessfully = true;

    public SettingsStore(string baseDirectory)
    {
        _baseDirectory = Path.GetFullPath(baseDirectory);
        _settingsPath = Path.Combine(_baseDirectory, "v0.292-settings.json");
        _backupPath = _settingsPath + ".bak";
        _assetsDirectory = Path.Combine(_baseDirectory, "assets");
    }

    public string SettingsPath => _settingsPath;
    public string? LoadWarning { get; private set; }

    public async Task<V27Settings> LoadAsync()
    {
        var loadPath = new[] { _settingsPath, Path.Combine(_baseDirectory, "v29.2-settings.json"), Path.Combine(_baseDirectory, "v29.1-settings.json"), Path.Combine(_baseDirectory, "v29-settings.json") }
            .FirstOrDefault(File.Exists) ?? _settingsPath;
        var backupPath = loadPath + ".bak";
        if (!File.Exists(loadPath))
            return new V27Settings();
        try
        {
            var settings = V27SettingsJson.Deserialize(await File.ReadAllTextAsync(loadPath));
            _loadedPrimarySuccessfully = true;
            return settings;
        }
        catch (Exception primaryException)
        {
            _loadedPrimarySuccessfully = false;
            if (File.Exists(backupPath))
            {
                try
                {
                    var recovered = V27SettingsJson.Deserialize(await File.ReadAllTextAsync(backupPath));
                    LoadWarning = "主配置文件损坏，已恢复上一次有效配置。原文件不会被自动删除。";
                    return recovered;
                }
                catch
                {
                }
            }
            LoadWarning = $"配置文件无法读取，已使用默认设置：{primaryException.Message}";
            return new V27Settings();
        }
    }

    public async Task SaveAsync(V27Settings settings)
    {
        await _saveLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(_baseDirectory);
            var temporaryPath = _settingsPath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, V27SettingsJson.Serialize(settings));
            if (_loadedPrimarySuccessfully && File.Exists(_settingsPath))
                File.Copy(_settingsPath, _backupPath, true);
            File.Move(temporaryPath, _settingsPath, true);
            _loadedPrimarySuccessfully = true;
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public async Task<string> ImportAssetAsync(string sourcePath, string assetKey)
    {
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".bmp"))
            throw new InvalidDataException("仅支持 PNG、JPG、WEBP、GIF 或 BMP 图片。");

        var source = new FileInfo(sourcePath);
        if (!source.Exists || source.Length == 0)
            throw new InvalidDataException("选择的图片文件不存在或内容为空。");
        if (source.Length > 50 * 1024 * 1024)
            throw new InvalidDataException("图片文件超过 50 MB，请先压缩后再上传。");

        Directory.CreateDirectory(_assetsDirectory);
        var safeKey = new string(assetKey.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_').ToArray());
        if (safeKey.Length == 0)
            throw new InvalidDataException("素材名称无效。");
        var destination = Path.Combine(_assetsDirectory, safeKey + extension);
        if (Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            return Path.GetRelativePath(_baseDirectory, destination).Replace('\\', '/');
        var temporaryPath = destination + ".tmp";
        await using (var input = File.OpenRead(sourcePath))
        await using (var output = File.Create(temporaryPath))
            await input.CopyToAsync(output);
        File.Move(temporaryPath, destination, true);
        return Path.GetRelativePath(_baseDirectory, destination).Replace('\\', '/');
    }

    public string? ResolveAsset(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            return null;
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, relativePath));
            var prefix = _baseDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
                return null;
            return fullPath;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
        {
            return null;
        }
    }
}

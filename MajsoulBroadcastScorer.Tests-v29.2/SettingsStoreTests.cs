using MajsoulBroadcastScorer.Core.V27;
using MajsoulBroadcastScorer.V27.Services;

namespace MajsoulBroadcastScorer.Tests.V27;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "majsoul-v29-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAndLoad_RestoresManualSettings()
    {
        var store = new SettingsStore(_directory);
        var settings = new V27Settings
        {
            Department = "直播部",
            ShowTotalPoints = false,
            ApiDisplayDelaySeconds = 2.5,
            AccountName = "referee@example.com",
            ContestId = "123456"
        };
        settings.Players[1].ScoreLocked = true;
        settings.Players[1].ManualScore = 30100;

        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();

        Assert.Equal("直播部", loaded.Department);
        Assert.False(loaded.ShowTotalPoints);
        Assert.Equal(2.5, loaded.ApiDisplayDelaySeconds);
        Assert.True(loaded.Players[1].ScoreLocked);
        Assert.Equal(30100, loaded.Players[1].ManualScore);
        Assert.Equal("referee@example.com", loaded.AccountName);
        Assert.Equal("123456", loaded.ContestId);
        Assert.EndsWith("v0.292-settings.json", store.SettingsPath);
    }

    [Fact]
    public async Task LoadLegacyV29Settings_SavesAsBetaWithoutOverwritingOriginal()
    {
        Directory.CreateDirectory(_directory);
        var legacyPath = Path.Combine(_directory, "v29-settings.json");
        const string legacyJson = """{"version":290,"department":"原赛事部","players":[{"seat":0,"teamName":"旧队伍","avatarPath":"assets/player.png"}]}""";
        await File.WriteAllTextAsync(legacyPath, legacyJson);
        var store = new SettingsStore(_directory);

        var loaded = await store.LoadAsync();
        Assert.Equal(292, loaded.Version);
        Assert.Equal("旧队伍", loaded.Players[0].TeamName);
        Assert.Equal("assets/player.png", loaded.Players[0].AvatarPath);
        loaded.Department = "新赛事部";
        await store.SaveAsync(loaded);

        Assert.Equal(legacyJson, await File.ReadAllTextAsync(legacyPath));
        Assert.Equal("新赛事部", (await store.LoadAsync()).Department);
    }

    [Fact]
    public async Task ImportAsset_CopiesIntoAssets_AndInvalidPathStaysMissing()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "source.png");
        await File.WriteAllBytesAsync(source, [137, 80, 78, 71]);
        var store = new SettingsStore(_directory);

        var relative = await store.ImportAssetAsync(source, "avatar-0");

        Assert.StartsWith("assets/", relative);
        Assert.NotNull(store.ResolveAsset(relative));
        Assert.Null(store.ResolveAsset("assets/missing.png"));
        Assert.Null(store.ResolveAsset("../outside.png"));
    }

    [Fact]
    public async Task Load_WhenPrimaryIsBroken_RestoresLastValidBackup()
    {
        var store = new SettingsStore(_directory);
        await store.SaveAsync(new V27Settings { Department = "第一版" });
        await store.SaveAsync(new V27Settings { Department = "第二版" });
        await File.WriteAllTextAsync(store.SettingsPath, "{broken");

        var loaded = await store.LoadAsync();

        Assert.Equal("第一版", loaded.Department);
        Assert.Contains("已恢复", store.LoadWarning);
    }

    [Fact]
    public async Task ImportAsset_RejectsEmptyFile()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "empty.png");
        await File.WriteAllBytesAsync(source, []);
        var store = new SettingsStore(_directory);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.ImportAssetAsync(source, "avatar-0"));
    }

    [Fact]
    public void MalformedAssetPathIsUnavailableInsteadOfBreakingStartup()
    {
        var store = new SettingsStore(_directory);
        Assert.Null(store.ResolveAsset("assets/\0broken.png"));
    }

    [Fact]
    public async Task V292LegacyFileStaysUntouchedWhenSavingBeta()
    {
        Directory.CreateDirectory(_directory);
        var legacy = Path.Combine(_directory, "v29.2-settings.json");
        var original = V27SettingsJson.Serialize(new V27Settings { Department = "旧版设置" });
        await File.WriteAllTextAsync(legacy, original);
        var store = new SettingsStore(_directory);
        var loaded = await store.LoadAsync();
        Assert.Equal("旧版设置", loaded.Department);
        loaded.Department = "测试版设置";
        await store.SaveAsync(loaded);
        Assert.Equal(original, await File.ReadAllTextAsync(legacy));
        Assert.Equal("测试版设置", (await store.LoadAsync()).Department);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }
}

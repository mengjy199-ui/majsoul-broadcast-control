using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MajsoulBroadcastScorer.V27;

public sealed class PlayerEditorViewModel : INotifyPropertyChanged
{
    private bool _silent;
    private string _name = string.Empty;
    private string _score = "25000";
    private string _wind = "?";
    private string _total = "+0.0";
    private string _riichiMode = "自动跟随 API";
    private string _avatarMode = "上传图片";
    private string _avatarStatus = "未上传";
    private string _avatarFrameMode = "默认边框";
    private string _avatarFrameStatus = "默认";
    private string _teamName = string.Empty;
    private string _teamSource = "API";
    private string _nameSource = "API";
    private string _scoreSource = "API";
    private string _windSource = "API";
    private bool _showAvatarControls = true;

    public PlayerEditorViewModel(int seat, Action<PlayerEditorViewModel, string> changed)
    {
        Seat = seat;
        Changed = changed;
    }

    public int Seat { get; }
    public string SeatLabel => $"seat {Seat}";
    public Action<PlayerEditorViewModel, string> Changed { get; }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Score { get => _score; set => Set(ref _score, value); }
    public string Wind { get => _wind; set => Set(ref _wind, value); }
    public string Total { get => _total; set => Set(ref _total, value); }
    public string RiichiMode { get => _riichiMode; set => Set(ref _riichiMode, value); }
    public string AvatarMode { get => _avatarMode; set => Set(ref _avatarMode, value); }
    public string AvatarStatus { get => _avatarStatus; set => Set(ref _avatarStatus, value); }
    public string AvatarFrameMode { get => _avatarFrameMode; set => Set(ref _avatarFrameMode, value); }
    public string AvatarFrameStatus { get => _avatarFrameStatus; set => Set(ref _avatarFrameStatus, value); }
    public string TeamName { get => _teamName; set => Set(ref _teamName, value); }
    public string TeamSource { get => _teamSource; set => Set(ref _teamSource, value); }
    public string NameSource { get => _nameSource; set => Set(ref _nameSource, value); }
    public string ScoreSource { get => _scoreSource; set => Set(ref _scoreSource, value); }
    public string WindSource { get => _windSource; set => Set(ref _windSource, value); }
    public bool ShowAvatarControls
    {
        get => _showAvatarControls;
        set
        {
            if (_showAvatarControls == value)
                return;
            _showAvatarControls = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowAvatarControls)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void UpdateSilently(Action update)
    {
        _silent = true;
        try { update(); }
        finally { _silent = false; }
    }

    private void Set(ref string field, string value, [CallerMemberName] string propertyName = "")
    {
        if (field == value)
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (!_silent)
            Changed(this, propertyName);
    }
}

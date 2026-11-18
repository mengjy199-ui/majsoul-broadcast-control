using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace MajsoulBroadcastScorer.V27;

public sealed class PlayerEditorViewModel : INotifyPropertyChanged
{
    private bool _silent;
    private string _name = string.Empty;
    private string _score = "25000";
    private string _wind = "?";
    private string _total = "+0.0";
    private string _totalLabel = "此前累计精算点";
    private Visibility _totalVisibility = Visibility.Visible;
    private bool _canEditTotal = true;
    private string _riichiMode = "自动跟随 API";
    private string _avatarStatus = "未上传";
    private string _avatarPreviewText = "未上传";
    private ImageSource? _avatarPreview;
    private string _avatarFrameMode = "默认边框";
    private string _avatarFrameStatus = "默认";
    private string _teamName = string.Empty;
    private string _teamSource = "API";
    private string _nameSource = "API";
    private string _scoreSource = "API";
    private string _windSource = "API";

    public PlayerEditorViewModel(int seat, Action<PlayerEditorViewModel, string> changed)
    {
        Seat = seat;
        Changed = changed;
    }

    public int Seat { get; }
    public override string ToString() => SeatLabel;
    public string SeatLabel => $"选手 {Seat + 1}";
    public Action<PlayerEditorViewModel, string> Changed { get; }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Score { get => _score; set => Set(ref _score, value); }
    public string ScoreDisplay => int.TryParse(_score, out var score)
        ? score.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) : _score;
    public string Wind { get => _wind; set => Set(ref _wind, value); }
    public string Total { get => _total; set => Set(ref _total, value); }
    public string TotalLabel { get => _totalLabel; set => Set(ref _totalLabel, value); }
    public Visibility TotalVisibility { get => _totalVisibility; set => Set(ref _totalVisibility, value); }
    public bool CanEditTotal { get => _canEditTotal; set => Set(ref _canEditTotal, value); }
    public string RiichiMode { get => _riichiMode; set => Set(ref _riichiMode, value); }
    public string AvatarStatus { get => _avatarStatus; set => Set(ref _avatarStatus, value); }
    public string AvatarPreviewText { get => _avatarPreviewText; set => Set(ref _avatarPreviewText, value); }
    public ImageSource? AvatarPreview
    {
        get => _avatarPreview;
        set
        {
            if (ReferenceEquals(_avatarPreview, value)) return;
            _avatarPreview = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvatarPreview)));
        }
    }
    public string AvatarFrameMode { get => _avatarFrameMode; set => Set(ref _avatarFrameMode, value); }
    public string AvatarFrameStatus { get => _avatarFrameStatus; set => Set(ref _avatarFrameStatus, value); }
    public string TeamName { get => _teamName; set => Set(ref _teamName, value); }
    public string TeamSource { get => _teamSource; set => Set(ref _teamSource, value); }
    public string NameSource { get => _nameSource; set => Set(ref _nameSource, value); }
    public string ScoreSource { get => _scoreSource; set => Set(ref _scoreSource, value); }
    public string WindSource { get => _windSource; set => Set(ref _windSource, value); }
    public event PropertyChangedEventHandler? PropertyChanged;

    public void UpdateSilently(Action update)
    {
        _silent = true;
        try { update(); }
        finally { _silent = false; }
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (propertyName == nameof(Score))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScoreDisplay)));
        if (!_silent)
            Changed(this, propertyName);
    }
}

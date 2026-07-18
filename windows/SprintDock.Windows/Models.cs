using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SprintDock.Windows;

public abstract class ObservableModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class SprintState : ObservableModel
{
    private string _goal = "";
    private string _finishLine = "";
    private string _nextAction = "";
    private string _notes = "";
    private int _focusMinutes = 25;
    private int _remainingSeconds = 1500;
    private DateTime? _timerTargetUtc;
    private int _completedSessions;

    public string Goal { get => _goal; set => Set(ref _goal, value); }
    public string FinishLine { get => _finishLine; set => Set(ref _finishLine, value); }
    public string NextAction { get => _nextAction; set => Set(ref _nextAction, value); }
    public string Notes { get => _notes; set => Set(ref _notes, value); }
    public int FocusMinutes { get => _focusMinutes; set => Set(ref _focusMinutes, value); }
    public int RemainingSeconds { get => _remainingSeconds; set => Set(ref _remainingSeconds, value); }
    public DateTime? TimerTargetUtc { get => _timerTargetUtc; set => Set(ref _timerTargetUtc, value); }
    public int CompletedSessions { get => _completedSessions; set => Set(ref _completedSessions, value); }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ObservableCollection<SprintTask> Tasks { get; set; } = [];
    public ObservableCollection<BlockerItem> Blockers { get; set; } = [];
    public ObservableCollection<ChecklistItem> Checklist { get; set; } = [];
    public ObservableCollection<ActivityItem> Activity { get; set; } = [];
}

public sealed class SprintTask : ObservableModel
{
    private string _title = "";
    private bool _isDone;
    private bool _isCurrent;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get => _title; set => Set(ref _title, value); }
    public bool IsDone { get => _isDone; set => Set(ref _isDone, value); }
    public bool IsCurrent { get => _isCurrent; set => Set(ref _isCurrent, value); }
}

public sealed class BlockerItem : ObservableModel
{
    private string _text = "";
    private bool _isResolved;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get => _text; set => Set(ref _text, value); }
    public bool IsResolved { get => _isResolved; set => Set(ref _isResolved, value); }
}

public sealed class ChecklistItem : ObservableModel
{
    private bool _isDone;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Group { get; set; } = "Finish";
    public string Title { get; set; } = "";
    public bool IsDone { get => _isDone; set => Set(ref _isDone, value); }
}

public sealed class ActivityItem
{
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public string Message { get; set; } = "";
    public string DisplayTime => AtUtc.ToLocalTime().ToString("MMM d, h:mm tt");
}

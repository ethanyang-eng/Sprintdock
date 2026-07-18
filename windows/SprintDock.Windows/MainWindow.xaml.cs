using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SprintDock.Windows;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private SprintState? _state;
    private SprintState? _draft;
    private bool _suppressTextEvents;

    public MainWindow()
    {
        InitializeComponent();
        _timer.Tick += Timer_Tick;
        _timer.Start();
        Loaded += MainWindow_Loaded;
        Closing += (_, _) => SaveState();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _state = LocalStore.Load();
        if (_state is not null)
        {
            NormalizeState(_state);
            ContinueCard.Visibility = Visibility.Visible;
            ContinueGoalText.Text = _state.Goal;
            RefreshWorkspace();
        }
        else
        {
            WorkspaceHost.Visibility = Visibility.Hidden;
        }
        GoalInput.Focus();
    }

    private static void NormalizeState(SprintState state)
    {
        state.Tasks ??= [];
        state.Blockers ??= [];
        state.Checklist ??= [];
        state.Activity ??= [];
        if (state.FocusMinutes < 1) state.FocusMinutes = 25;
        if (state.RemainingSeconds < 1 && state.TimerTargetUtc is null)
            state.RemainingSeconds = state.FocusMinutes * 60;
        EnsureCurrentTask(state);
    }

    private static void EnsureCurrentTask(SprintState state)
    {
        var available = state.Tasks.Where(task => !task.IsDone).ToList();
        if (available.Count == 0) return;
        var current = available.FirstOrDefault(task => task.IsCurrent) ?? available[0];
        foreach (var task in state.Tasks) task.IsCurrent = task == current;
        state.NextAction = current.Title;
    }

    private void ContinueSprint_Click(object sender, RoutedEventArgs e)
    {
        if (_state is null) return;
        OnboardingOverlay.Visibility = Visibility.Collapsed;
        WorkspaceHost.Visibility = Visibility.Visible;
        RefreshWorkspace();
    }

    private void BuildPlan_Click(object sender, RoutedEventArgs e)
    {
        var goal = GoalInput.Text.Trim();
        if (goal.Length < 3)
        {
            MessageBox.Show(this, "Add a clearer finish line first.", "Sprint Dock", MessageBoxButton.OK, MessageBoxImage.Information);
            GoalInput.Focus();
            return;
        }

        _draft = LocalPlanner.Build(goal, DetailsInput.Text, SelectedFocusMinutes());
        ShowPlanPreview();
    }

    private int SelectedFocusMinutes()
    {
        if (FocusLengthCombo.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var minutes))
            return minutes;
        return 25;
    }

    private void ShowPlanPreview()
    {
        if (_draft is null) return;
        PreviewGoalText.Text = _draft.Goal;
        PreviewFinishText.Text = _draft.FinishLine;
        DraftTaskItems.ItemsSource = _draft.Tasks;
        GoalEntryPanel.Visibility = Visibility.Collapsed;
        PlanPreviewPanel.Visibility = Visibility.Visible;
    }

    private void RebuildPlan_Click(object sender, RoutedEventArgs e)
    {
        var goal = PreviewGoalText.Text.Trim();
        if (goal.Length < 3) return;
        _draft = LocalPlanner.Build(goal, PreviewFinishText.Text, _draft?.FocusMinutes ?? SelectedFocusMinutes());
        ShowPlanPreview();
    }

    private void BackToGoal_Click(object sender, RoutedEventArgs e)
    {
        GoalEntryPanel.Visibility = Visibility.Visible;
        PlanPreviewPanel.Visibility = Visibility.Collapsed;
    }

    private void AddDraftTask_Click(object sender, RoutedEventArgs e)
    {
        _draft?.Tasks.Add(new SprintTask { Title = "New step" });
    }

    private void DeleteDraftTask_Click(object sender, RoutedEventArgs e)
    {
        if (_draft is null || TaskFromSender(sender) is not { } task || _draft.Tasks.Count <= 1) return;
        _draft.Tasks.Remove(task);
    }

    private void StartSprint_Click(object sender, RoutedEventArgs e)
    {
        if (_draft is null) return;
        _draft.Goal = PreviewGoalText.Text.Trim();
        _draft.FinishLine = PreviewFinishText.Text.Trim();
        var empty = _draft.Tasks.Where(task => string.IsNullOrWhiteSpace(task.Title)).ToList();
        foreach (var task in empty) _draft.Tasks.Remove(task);
        if (_draft.Tasks.Count == 0)
            _draft.Tasks.Add(new SprintTask { Title = "Define the first useful step" });
        foreach (var task in _draft.Tasks) task.IsCurrent = false;
        _draft.Tasks[0].IsCurrent = true;
        _draft.NextAction = _draft.Tasks[0].Title;
        _draft.Activity.Add(new ActivityItem { Message = "Reviewed and started the plan" });
        _state = _draft;
        _draft = null;
        SaveState();
        OnboardingOverlay.Visibility = Visibility.Collapsed;
        WorkspaceHost.Visibility = Visibility.Visible;
        RefreshWorkspace();
    }

    private void NewSprint_Click(object sender, RoutedEventArgs e)
    {
        if (_state is not null)
        {
            var answer = MessageBox.Show(this, "Start a new sprint? Your current sprint will be replaced on this PC.",
                "New sprint", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
        }

        GoalInput.Clear();
        DetailsInput.Clear();
        _draft = null;
        GoalEntryPanel.Visibility = Visibility.Visible;
        PlanPreviewPanel.Visibility = Visibility.Collapsed;
        OnboardingOverlay.Visibility = Visibility.Visible;
        GoalInput.Focus();
    }

    private void RefreshWorkspace()
    {
        if (_state is null) return;
        _suppressTextEvents = true;
        GoalWorkspaceText.Text = _state.Goal;
        FinishLineText.Text = _state.FinishLine;
        NextActionText.Text = _state.NextAction;
        NotesText.Text = _state.Notes;
        TimerMinutesBox.Text = _state.FocusMinutes.ToString();
        _suppressTextEvents = false;

        EnsureCurrentTask(_state);
        var current = _state.Tasks.FirstOrDefault(task => task.IsCurrent && !task.IsDone);
        CurrentTaskText.Text = current?.Title ?? "Sprint complete";

        var completed = _state.Tasks.Count(task => task.IsDone);
        var total = _state.Tasks.Count;
        var percent = total == 0 ? 0 : completed * 100d / total;
        SprintProgress.Value = percent;
        HeaderProgressText.Text = $"{completed} of {total} steps";
        RailProgressText.Text = $"{completed} of {total} steps";
        StepsSummary.Text = $"{completed}/{total}  Steps";

        var openBlockers = _state.Blockers.Count(blocker => !blocker.IsResolved);
        BlockersSummary.Text = $"{openBlockers}  Blockers";
        var ready = _state.Checklist.Count == 0 ? 0 : _state.Checklist.Count(item => item.IsDone) * 100 / _state.Checklist.Count;
        ReadySummary.Text = $"{ready}%  Ready";
        SessionsSummary.Text = $"{_state.CompletedSessions}  Sessions";

        TaskItems.ItemsSource = _state.Tasks;
        BlockerItems.ItemsSource = _state.Blockers.Where(blocker => !blocker.IsResolved).ToList();
        ChecklistItems.ItemsSource = _state.Checklist;
        ActivityItems.ItemsSource = _state.Activity.Reverse().ToList();
        UpdateTimerDisplay();
    }

    private void WorkspaceText_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextEvents || _state is null) return;
        _state.Goal = GoalWorkspaceText.Text;
        _state.FinishLine = FinishLineText.Text;
        _state.NextAction = NextActionText.Text;
        SaveState();
    }

    private void NotesText_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextEvents || _state is null) return;
        _state.Notes = NotesText.Text;
        SaveState();
    }

    private void Replan_Click(object sender, RoutedEventArgs e)
    {
        if (_state is null) return;
        var completed = _state.Tasks.Where(task => task.IsDone).ToList();
        var context = string.Join("\n", new[] { _state.FinishLine, _state.Notes }
            .Concat(_state.Blockers.Where(blocker => !blocker.IsResolved).Select(blocker => blocker.Text))
            .Where(text => !string.IsNullOrWhiteSpace(text)));
        var replacement = LocalPlanner.Build(_state.Goal, context, _state.FocusMinutes);
        _state.Tasks = new ObservableCollection<SprintTask>(completed.Concat(replacement.Tasks));
        foreach (var task in completed) task.IsCurrent = false;
        EnsureCurrentTask(_state);
        AddActivity("Replanned the remaining steps locally");
        SaveState();
        RefreshWorkspace();
    }

    private void AddTask_Click(object sender, RoutedEventArgs e) => AddTask();

    private void NewTaskBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        AddTask();
        e.Handled = true;
    }

    private void AddTask()
    {
        if (_state is null) return;
        var title = NewTaskBox.Text.Trim();
        if (title.Length == 0) return;
        var task = new SprintTask { Title = title, IsCurrent = !_state.Tasks.Any(item => !item.IsDone) };
        _state.Tasks.Add(task);
        NewTaskBox.Clear();
        AddActivity($"Added step: {title}");
        EnsureCurrentTask(_state);
        SaveState();
        RefreshWorkspace();
    }

    private void FocusTask_Click(object sender, RoutedEventArgs e)
    {
        if (_state is null || TaskFromSender(sender) is not { } task || task.IsDone) return;
        foreach (var item in _state.Tasks) item.IsCurrent = item == task;
        _state.NextAction = task.Title;
        AddActivity($"Focused step: {task.Title}");
        SaveState();
        RefreshWorkspace();
    }

    private void CompleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (TaskFromSender(sender) is { } task) CompleteTask(task);
    }

    private void CompleteCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (_state?.Tasks.FirstOrDefault(task => task.IsCurrent && !task.IsDone) is { } current)
            CompleteTask(current);
    }

    private void CompleteTask(SprintTask task)
    {
        if (_state is null || task.IsDone) return;
        task.IsDone = true;
        task.IsCurrent = false;
        AddActivity($"Completed step: {task.Title}");
        EnsureCurrentTask(_state);
        if (_state.Tasks.All(item => item.IsDone)) AddActivity("Completed the sprint");
        SaveState();
        RefreshWorkspace();
    }

    private void TaskTitle_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_state is null) return;
        EnsureCurrentTask(_state);
        SaveState();
        RefreshWorkspace();
    }

    private void TaskMore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is null) return;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }

    private void MoveTaskUp_Click(object sender, RoutedEventArgs e) => MoveTask(sender, -1);
    private void MoveTaskDown_Click(object sender, RoutedEventArgs e) => MoveTask(sender, 1);

    private void MoveTask(object sender, int offset)
    {
        if (_state is null || TaskFromSender(sender) is not { } task) return;
        var index = _state.Tasks.IndexOf(task);
        var destination = index + offset;
        if (index < 0 || destination < 0 || destination >= _state.Tasks.Count) return;
        _state.Tasks.Move(index, destination);
        SaveState();
        RefreshWorkspace();
    }

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (_state is null || TaskFromSender(sender) is not { } task) return;
        _state.Tasks.Remove(task);
        AddActivity($"Deleted step: {task.Title}");
        EnsureCurrentTask(_state);
        SaveState();
        RefreshWorkspace();
    }

    private static SprintTask? TaskFromSender(object sender)
    {
        if (sender is FrameworkElement { DataContext: SprintTask task }) return task;
        if (sender is MenuItem menuItem && ItemsControl.ItemsControlFromItemContainer(menuItem) is ContextMenu menu
            && menu.PlacementTarget is FrameworkElement { DataContext: SprintTask contextTask }) return contextTask;
        return null;
    }

    private void AddBlocker_Click(object sender, RoutedEventArgs e) => AddBlocker();

    private void NewBlockerBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        AddBlocker();
        e.Handled = true;
    }

    private void AddBlocker()
    {
        if (_state is null) return;
        var text = NewBlockerBox.Text.Trim();
        if (text.Length == 0) return;
        _state.Blockers.Add(new BlockerItem { Text = text });
        NewBlockerBox.Clear();
        AddActivity($"Added blocker: {text}");
        SaveState();
        RefreshWorkspace();
    }

    private void ResolveBlocker_Click(object sender, RoutedEventArgs e)
    {
        if (_state is null || BlockerFromSender(sender) is not { } blocker) return;
        blocker.IsResolved = true;
        AddActivity($"Resolved blocker: {blocker.Text}");
        SaveState();
        RefreshWorkspace();
    }

    private void DeleteBlocker_Click(object sender, RoutedEventArgs e)
    {
        if (_state is null || BlockerFromSender(sender) is not { } blocker) return;
        _state.Blockers.Remove(blocker);
        SaveState();
        RefreshWorkspace();
    }

    private void BlockerText_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => SaveState();

    private static BlockerItem? BlockerFromSender(object sender) =>
        sender is FrameworkElement { DataContext: BlockerItem blocker } ? blocker : null;

    private void Checklist_Changed(object sender, RoutedEventArgs e)
    {
        if (_state is null || _suppressTextEvents) return;
        SaveState();
        RefreshWorkspace();
    }

    private void TimerStart_Click(object sender, RoutedEventArgs e)
    {
        if (_state is null) return;
        if (_state.TimerTargetUtc is not null)
        {
            _state.RemainingSeconds = EffectiveRemainingSeconds();
            _state.TimerTargetUtc = null;
            AddActivity("Paused the focus timer");
        }
        else
        {
            if (_state.RemainingSeconds <= 0) _state.RemainingSeconds = _state.FocusMinutes * 60;
            _state.TimerTargetUtc = DateTime.UtcNow.AddSeconds(_state.RemainingSeconds);
            AddActivity("Started a focus session");
        }
        SaveState();
        UpdateTimerDisplay();
    }

    private void TimerReset_Click(object sender, RoutedEventArgs e)
    {
        if (_state is null) return;
        _state.TimerTargetUtc = null;
        _state.RemainingSeconds = _state.FocusMinutes * 60;
        AddActivity("Reset the focus timer");
        SaveState();
        UpdateTimerDisplay();
    }

    private void TimerExtend_Click(object sender, RoutedEventArgs e)
    {
        if (_state is null) return;
        if (_state.TimerTargetUtc is not null) _state.TimerTargetUtc = _state.TimerTargetUtc.Value.AddMinutes(5);
        else _state.RemainingSeconds += 300;
        AddActivity("Added five minutes");
        SaveState();
        UpdateTimerDisplay();
    }

    private void ApplyDuration_Click(object sender, RoutedEventArgs e)
    {
        if (_state is null || !int.TryParse(TimerMinutesBox.Text, out var minutes)) return;
        minutes = Math.Clamp(minutes, 1, 240);
        _state.FocusMinutes = minutes;
        _state.RemainingSeconds = minutes * 60;
        _state.TimerTargetUtc = null;
        TimerMinutesBox.Text = minutes.ToString();
        AddActivity($"Changed focus duration to {minutes} minutes");
        SaveState();
        UpdateTimerDisplay();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_state is null || _state.TimerTargetUtc is null) return;
        var remaining = EffectiveRemainingSeconds();
        if (remaining <= 0)
        {
            _state.TimerTargetUtc = null;
            _state.RemainingSeconds = _state.FocusMinutes * 60;
            _state.CompletedSessions += 1;
            AddActivity("Completed a focus session");
            SaveState();
            RefreshWorkspace();
            System.Media.SystemSounds.Asterisk.Play();
            return;
        }
        UpdateTimerDisplay();
    }

    private int EffectiveRemainingSeconds()
    {
        if (_state?.TimerTargetUtc is null) return Math.Max(0, _state?.RemainingSeconds ?? 0);
        return Math.Max(0, (int)Math.Ceiling((_state.TimerTargetUtc.Value - DateTime.UtcNow).TotalSeconds));
    }

    private void UpdateTimerDisplay()
    {
        if (_state is null) return;
        var remaining = EffectiveRemainingSeconds();
        TimerText.Text = $"{remaining / 60:00}:{remaining % 60:00}";
        TimerStartButton.Content = _state.TimerTargetUtc is null
            ? (remaining < _state.FocusMinutes * 60 ? "Resume" : "Start")
            : "Pause";
    }

    private void AddActivity(string message)
    {
        _state?.Activity.Add(new ActivityItem { Message = message });
    }

    private void SaveState()
    {
        if (_state is not null) LocalStore.Save(_state);
    }

    private void FocusRail_Click(object sender, RoutedEventArgs e) => DockPanel.Visibility = Visibility.Collapsed;
    private void PlanRail_Click(object sender, RoutedEventArgs e) => OpenDock("Plan", PlanDock);
    private void BlockersRail_Click(object sender, RoutedEventArgs e) => OpenDock("Blockers", BlockersDock);
    private void ChecklistRail_Click(object sender, RoutedEventArgs e) => OpenDock("Checklist", ChecklistDock);
    private void NotesRail_Click(object sender, RoutedEventArgs e) => OpenDock("Notes", NotesDock);
    private void HistoryRail_Click(object sender, RoutedEventArgs e) => OpenDock("History", HistoryDock);
    private void CloseDock_Click(object sender, RoutedEventArgs e) => DockPanel.Visibility = Visibility.Collapsed;

    private void OpenDock(string title, FrameworkElement selected)
    {
        DockTitle.Text = title;
        DockPanel.Visibility = Visibility.Visible;
        foreach (var panel in new FrameworkElement[] { PlanDock, BlockersDock, ChecklistDock, NotesDock, HistoryDock })
            panel.Visibility = panel == selected ? Visibility.Visible : Visibility.Collapsed;
        RefreshWorkspace();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ToggleMaximize();
        else DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}

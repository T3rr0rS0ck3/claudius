using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Claudius.Core;
using Claudius.Pet;
using Claudius.Shared;
using Forms = System.Windows.Forms;

namespace Claudius.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly App _app;
    private readonly List<(Border Tab, GroupBox[] Groups)> _tabs = [];

    /// <summary>The tab shown when the settings are opened again.</summary>
    private static int _lastTab;

    /// <summary>Tabs in the look of the inventory's, each showing some of the groups.</summary>
    private void BuildTabs()
    {
        (string Name, GroupBox[] Groups)[] tabs =
        [
            (Strings.TabGeneral, [GeneralGroup, UpdatesGroup]),
            (Strings.TabAssistant, [AssistantGroup, LaunchGroup]),
            (Strings.TabPet, [PetGroup]),
            (Strings.TabUsage, [UsageGroup]),
            (Strings.TabBubbles, [BubblesGroup]),
        ];
        foreach (var (name, groups) in tabs)
        {
            int index = _tabs.Count;
            var tab = new Border
            {
                Padding = new Thickness(0, 4, 0, 6),
                BorderThickness = new Thickness(0, 0, 0, 2),
                Background = Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = new TextBlock { Text = name, HorizontalAlignment = HorizontalAlignment.Center },
            };
            tab.MouseLeftButtonUp += (_, _) => ShowTab(index);
            _tabs.Add((tab, groups));
            TabStrip.Children.Add(tab);
        }
        ShowTab(_lastTab);
    }

    private void ShowTab(int index)
    {
        _lastTab = index;
        var accent = (Brush)FindResource("AccentBrush");
        var line = new SolidColorBrush(Color.FromRgb(0x3A, 0x39, 0x36));
        var text = new SolidColorBrush(Color.FromRgb(0xB5, 0xB1, 0xA8));
        for (int i = 0; i < _tabs.Count; i++)
        {
            var (tab, groups) = _tabs[i];
            tab.BorderBrush = i == index ? accent : line;
            ((TextBlock)tab.Child).Foreground = i == index ? accent : text;
            foreach (var group in groups) group.Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public SettingsWindow(App app, AppSettings settings)
    {
        InitializeComponent();
        Icon = Sprite.WindowIcon();
        // Scroll instead of growing past the screen; Escape closes like a normal dialog.
        MaxHeight = SystemParameters.WorkArea.Height - 20;
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
        _app = app;
        _settings = settings;
        BuildTabs();

        ScaleSlider.Value = settings.PetScale;
        AlwaysOnTopBox.IsChecked = settings.AlwaysOnTop;
        AnimationsBox.IsChecked = settings.Animations;
        WalkAroundBox.IsChecked = settings.WalkAround;
        CrossMonitorsBox.IsChecked = settings.CrossMonitors;
        ChaseCursorBox.IsChecked = settings.ChaseCursor;
        EmotesBox.IsChecked = settings.Emotes;
        NightModeBox.IsChecked = settings.NightMode;
        ShowTime(NightStartBox, settings.NightStart);
        ShowTime(NightEndBox, settings.NightEnd);
        BreakBox.IsChecked = settings.BreakReminder;
        BreakAfterBox.Text = settings.BreakAfterMinutes.ToString(CultureInfo.InvariantCulture);
        UpdatesBox.IsChecked = settings.CheckForUpdates;
        VersionText.Text = Strings.InstalledVersion(Updater.Format(Updater.CurrentVersion))
            + (Updater.IsDevBuild ? Strings.DevBuildSuffix : Updater.IsInstalled ? "" : Strings.PortableSuffix);
        CheckUpdateButton.IsEnabled = !Updater.IsDevBuild;
        ShowUpdate(app.AvailableUpdate);
        SessionMarksBox.IsChecked = settings.SessionMarks;
        AutostartBox.IsChecked = settings.StartWithWindows;
        IntervalBox.Text = settings.PollIntervalSeconds.ToString(CultureInfo.InvariantCulture);
        SessionWarnBox.Text = string.Join(", ", settings.SessionWarnThresholds);
        WeekWarnBox.Text = string.Join(", ", settings.WeekWarnThresholds);
        var t = settings.Thresholds;
        MoodThresholdsBox.Text = string.Join(", ", t.Normal, t.Attentive, t.Nervous, t.Worried, t.Panic, t.Exhausted);
        ContextThresholdsBox.Text = string.Join(", ", settings.ContextThresholds);
        ForecastBox.IsChecked = settings.ShowForecast;
        BubblesBox.IsChecked = settings.SpeechBubbles;
        BubbleDurationBox.Text = settings.BubbleDurationSeconds.ToString(CultureInfo.InvariantCulture);
        NameBox.Text = settings.UserName;
        NotificationsBox.IsChecked = settings.Notifications;
        ReposPathBox.Text = settings.ReposPath ?? "";
        LanguageBox.ItemsSource = LanguageChoices;
        LanguageBox.DisplayMemberPath = "Value";
        LanguageBox.SelectedValuePath = "Key";
        LanguageBox.SelectedValue = settings.Language;
        TerminalBox.ItemsSource = TerminalChoices();
        TerminalBox.DisplayMemberPath = "Value";
        TerminalBox.SelectedValuePath = "Key";
        TerminalBox.SelectedValue = settings.Terminal;
        GhostDragBox.IsChecked = settings.GhostDrag;
        VoiceBox.IsChecked = settings.VoiceChat;

        string? assistant = AssistantLauncher.FindAssistant();
        AssistantStatusText.Text = assistant != null ? Strings.AssistantFound(assistant) : "⚠ " + Strings.AssistantNotFound;

        RefreshSetupStatus();
    }

    // Each language in its own name.
    private static readonly KeyValuePair<string, string>[] LanguageChoices =
    [
        new(Strings.English, "English"),
        new(Strings.German, "Deutsch"),
    ];

    private static KeyValuePair<TerminalKind, string>[] TerminalChoices() =>
    [
        new(TerminalKind.Auto, Strings.TerminalAuto(AssistantLauncher.HasWindowsTerminal)),
        new(TerminalKind.WindowsTerminal, "Windows Terminal"),
        new(TerminalKind.Cmd, Strings.TerminalCmd),
        new(TerminalKind.PowerShell, "PowerShell"),
        new(TerminalKind.Desktop, Strings.TerminalDesktop),
    ];

    private void BrowseRepos_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = Strings.ChooseReposFolder };
        if (Directory.Exists(ReposPathBox.Text)) dialog.InitialDirectory = ReposPathBox.Text;
        if (dialog.ShowDialog(this) == true) ReposPathBox.Text = dialog.FolderName;
    }

    private void RefreshSetupStatus()
    {
        var status = AssistantSetup.GetStatus();
        SetupStatusText.Text = status switch
        {
            SetupStatus.Connected => Strings.SetupConnected,
            SetupStatus.OtherStatusLine => Strings.SetupOtherStatusLine + AssistantSetup.CurrentCommand(),
            SetupStatus.BridgeMissing => Strings.SetupBridgeMissing,
            _ => Strings.SetupNotConnected,
        };
        ConnectButton.IsEnabled = status is SetupStatus.NotConfigured or SetupStatus.OtherStatusLine;
        DisconnectButton.IsEnabled = status == SetupStatus.Connected;
    }

    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        _app.ConnectAssistant(this);
        RefreshSetupStatus();
    }

    private void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AssistantSetup.Uninstall();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Claudius", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        RefreshSetupStatus();
    }

    private void EditTexts_Click(object sender, RoutedEventArgs e)
    {
        _settings.Save();
        Process.Start(new ProcessStartInfo(DataPaths.SettingsFile) { UseShellExecute = true });
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = Strings.Checking;
        UpdateStatusText.Visibility = Visibility.Visible;
        try
        {
            var update = await _app.CheckForUpdateAsync(silent: false);
            ShowUpdate(update);
            if (update == null) UpdateStatusText.Text = Strings.UpToDate;
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = Strings.CheckFailed + ex.Message;
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void ShowUpdate(UpdateInfo? update)
    {
        InstallUpdateButton.Visibility = update != null ? Visibility.Visible : Visibility.Collapsed;
        if (update == null) return;
        InstallUpdateButton.Content = Updater.IsInstalled ? Strings.Install : Strings.Download;
        UpdateStatusText.Text = Strings.VersionAvailable(update.VersionText);
        UpdateStatusText.Visibility = Visibility.Visible;
    }

    private void InstallUpdate_Click(object sender, RoutedEventArgs e) => _app.InstallUpdate(this);

    private void TestNotification_Click(object sender, RoutedEventArgs e) =>
        _app.ShowNotification("Claudius", Strings.TestNotificationText);

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int interval = ParseInt(IntervalBox.Text, Strings.IntervalField);
            int breakAfter = ParseInt(BreakAfterBox.Text, Strings.BreakField);
            double duration = double.Parse(BubbleDurationBox.Text.Replace(',', '.'), CultureInfo.InvariantCulture);
            var sessionWarn = ParseList(SessionWarnBox.Text, Strings.SessionWarnField);
            var weekWarn = ParseList(WeekWarnBox.Text, Strings.WeekWarnField);
            var moods = ParseList(MoodThresholdsBox.Text, Strings.MoodField);
            if (moods.Count != 6) throw new FormatException(Strings.MoodCountError);
            var context = ParseList(ContextThresholdsBox.Text, Strings.ContextField);
            if (context.Count != 3 || context[0] > context[1] || context[1] > context[2])
                throw new FormatException(Strings.ContextError);
            string reposPath = ReposPathBox.Text.Trim().Trim('"');
            if (reposPath.Length > 0 && !Directory.Exists(reposPath))
                throw new FormatException(Strings.ReposFolderMissing);

            // Switching voice chat off also switches the assistant's dictation off again. That setting lives in
            // The assistant's own settings.json, so write it first: if it fails, nothing is applied.
            if (_settings.VoiceChat && VoiceBox.IsChecked != true)
            {
                try
                {
                    AssistantSetup.SetVoiceEnabled(false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                               or System.Text.Json.JsonException)
                {
                    throw new FormatException(Strings.AssistantSettingsNotWritten + ex.Message);
                }
            }
            for (int i = 1; i < moods.Count; i++)
                if (moods[i] < moods[i - 1]) throw new FormatException(Strings.MoodOrderError);


            if (LanguageBox.SelectedValue is string language) _settings.SetLanguage(language);
            _settings.PetScale = ScaleSlider.Value;
            _settings.AlwaysOnTop = AlwaysOnTopBox.IsChecked == true;
            _settings.Animations = AnimationsBox.IsChecked == true;
            _settings.WalkAround = WalkAroundBox.IsChecked == true;
            _settings.CrossMonitors = CrossMonitorsBox.IsChecked == true;
            _settings.ChaseCursor = ChaseCursorBox.IsChecked == true;
            _settings.Emotes = EmotesBox.IsChecked == true;
            _settings.NightMode = NightModeBox.IsChecked == true;
            _settings.NightStart = (string)NightStartBox.SelectedItem;
            _settings.NightEnd = (string)NightEndBox.SelectedItem;
            _settings.BreakReminder = BreakBox.IsChecked == true;
            _settings.BreakAfterMinutes = Math.Clamp(breakAfter, AppSettings.MinBreakAfterMinutes, AppSettings.MaxBreakAfterMinutes);
            _settings.CheckForUpdates = UpdatesBox.IsChecked == true;
            _settings.SessionMarks = SessionMarksBox.IsChecked == true;
            _settings.StartWithWindows = AutostartBox.IsChecked == true;
            _settings.PollIntervalSeconds = Math.Clamp(interval, 1, 300);
            _settings.SessionWarnThresholds = sessionWarn;
            _settings.WeekWarnThresholds = weekWarn;
            _settings.ContextThresholds = context;
            _settings.Thresholds = new MoodThresholds
            {
                Normal = moods[0], Attentive = moods[1], Nervous = moods[2],
                Worried = moods[3], Panic = moods[4], Exhausted = moods[5],
            };
            _settings.ShowForecast = ForecastBox.IsChecked == true;
            _settings.SpeechBubbles = BubblesBox.IsChecked == true;
            _settings.BubbleDurationSeconds = Math.Clamp(duration, 1, 60);
            _settings.UserName = NameBox.Text.Trim();
            _settings.Notifications = NotificationsBox.IsChecked == true;
            _settings.ReposPath = reposPath.Length > 0 ? reposPath : null;
            _settings.Terminal = TerminalBox.SelectedValue is TerminalKind terminal ? terminal : TerminalKind.Auto;
            _settings.GhostDrag = GhostDragBox.IsChecked == true;
            _settings.VoiceChat = VoiceBox.IsChecked == true;

            _app.ApplySettings(save: true);
            Close();
        }
        catch (FormatException ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    // Borderless like the usage overlay: drag by the title, close with the ✕.
    private void Title_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // DragMove keeps the mouse until it is released, so the close button would never see its click.
        if (e.OriginalSource == CloseButton) return;
        try { DragMove(); } catch (InvalidOperationException) { }
    }

    private void Close_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => Close();

    // IsCancel only closes modal dialogs; this window is shown non-modally.
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private static int ParseInt(string text, string field) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new FormatException(Strings.InvalidNumber(field));

    private static List<int> ParseList(string text, string field) =>
        text.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => Math.Clamp(ParseInt(part, field), 0, 100))
            .ToList();

    /// <summary>Full hours to choose from, plus a time entered by hand in settings.json (e.g. 22:30).</summary>
    private static void ShowTime(ComboBox box, string time)
    {
        var times = Enumerable.Range(0, 24).Select(h => $"{h:00}:00").ToList();
        if (!times.Contains(time)) times = times.Append(time).Order().ToList();
        box.ItemsSource = times;
        box.SelectedItem = time;
    }

}

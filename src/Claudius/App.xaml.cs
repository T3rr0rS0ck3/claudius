using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Claudius.Core;
using Claudius.Pet;
using Claudius.Shared;
using Claudius.Views;

namespace Claudius;

public partial class App : Application
{
    private const string MutexName = "Claudius.SingleInstance.v1";
    private const string ShowEventName = "Claudius.Show.v1";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private readonly UsageMonitor _monitor = new();
    private readonly VoiceWatcher _voice = new();
    private readonly SessionMonitor _sessions = new();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Random _random = new();
    private readonly WarnState _sessionWarn = new();
    private readonly WarnState _weekWarn = new();
    private readonly DispatcherTimer _updateTimer = new();
    private bool _updating;

    private PetWindow _pet = null!;
    private TrayIcon? _tray;
    private UsageOverlay? _overlay;
    private DateTime _overlayClosedAt;
    private SettingsWindow? _settingsWindow;
    private InventoryWindow? _inventoryWindow;
    private UsageState _state = UsageState.Compute(null, new MoodThresholds(), DateTimeOffset.Now);
    private PetMood? _lastMood;
    private bool? _wasNight;
    private IReadOnlyList<HistorySample>? _reviewedHistory;
    private readonly BreakTracker _breaks = new();
    /// <summary>A break was suggested and not taken yet; taking it is an achievement.</summary>
    private bool _breakSuggested;
    /// <summary>Achievements reached but not announced yet, one bubble at a time.</summary>
    private readonly Queue<Achievement> _announcements = new();
    private AchievementPopup? _achievementPopup;
    private DateTimeOffset _reviewCheckAt;
    private DateTime _settingsWrite;

    public AppSettings Settings { get; private set; } = null!;
    public Achievements Achievements { get; private set; } = null!;
    public bool PetVisible => _pet.IsVisible;
    /// <summary>A newer release found by the last update check.</summary>
    public UpdateInfo? AvailableUpdate { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (HandleInstallerCommand(e.Args))
        {
            Shutdown();
            return;
        }

        _mutex = new Mutex(true, MutexName, out bool firstInstance);
        if (!firstInstance)
        {
            // Already running: ask the running instance to show itself.
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            Shutdown();
            return;
        }
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => Dispatcher.BeginInvoke(() => { ShowPet(); ShowUsage(); }), null, -1, false);

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Write(args.Exception.ToString());
            args.Handled = true;
        };

        Legacy.MigrateDataDir();
        Settings = AppSettings.Load();
        Strings.Use(Settings.Language!);
        Settings.Save();
        _settingsWrite = File.GetLastWriteTimeUtc(DataPaths.SettingsFile);
        Legacy.RemoveOldAutostart();
        if (Settings.StartWithWindows != Autostart.IsEnabled()) Autostart.Set(Settings.StartWithWindows);
        Legacy.MigrateTheme(Settings);
        Achievements = Achievements.Load();
        Achievements.Unlocked += _announcements.Enqueue;

        ApplyPetColor();
        ContextLevels.Thresholds = Settings.ContextThresholds;
        _pet = new PetWindow();
        _pet.ApplySettings(Settings, initial: true);
        _pet.Clicked += OnPetClicked;
        _pet.DoubleClicked += ShowProjectMenu;
        _pet.SessionClicked += GoToSession;
        _pet.GhostDropped += OnGhostDropped;
        _pet.FilesDropped += OnFilesDropped;
        _pet.Moved += SavePosition;
        _pet.Moved += () => Achievements.Count(Achievements.DragsCounter);
        _pet.Emoted += emote =>
        {
            Say(emote != Emote.Feed ? emote.ToString() : _pet.Unwell ? "FeedSick" : Settings.Snack == Snack.Beans ? "FeedBeans" : "Feed");
            Achievements.Count(emote.ToString());
            if (emote == Emote.Feed && Settings.Snack == Snack.Beans) Achievements.Count(Achievements.BeansCounter);
        };
        _pet.Splatted += () => Achievements.Count(Achievements.FallsCounter);
        _pet.Jumped += () => Achievements.Count(Achievements.JumpsCounter);
        _pet.PetImage.ContextMenu = BuildContextMenu();
        _pet.Show();

        _tray = new TrayIcon(this);

        // Load the current state silently first: no bubbles/notifications for values that were
        // already reached before the app started.
        _monitor.Start(Settings.PollIntervalSeconds);
        Evaluate(initial: true);
        _monitor.Changed += () => Evaluate(initial: false);
        _clock.Tick += (_, _) => OnClock();
        _clock.Start();
        _voice.ListeningChanged += listening => _pet.SetListening(listening);
        _voice.Start();

        // "?" and bubbles from the assistant's hooks; the first read is silent like the usage values.
        SyncSessionHooks();
        _sessions.Changed += ApplySessions;
        _sessions.Poll();
        _sessions.Attention += session =>
        {
            if (Settings.SessionMarks) Say(session.State == SessionStates.Question ? "Question" : "Done", session.Label);
        };
        // A nearly full context window makes the pet sick; it recovers once /compact or /clear emptied it
        _sessions.ContextRose += (session, level) => Say(level >= 3 ? "ContextCritical" : "ContextFull", session.Label);
        _sessions.ContextReset += (session, from, kind) =>
        {
            _pet.PlayRecovery(from);
            Say(kind == "clear" ? "ContextCleared" : "ContextCompacted", session.Label);
            if (from >= 3) Achievements.Unlock(AchievementId.ReaperEscaped);
        };

        Say(_state.Mood == PetMood.Unknown ? "NoData" : "Greeting");

        _updateTimer.Tick += (_, _) =>
        {
            _updateTimer.Interval = TimeSpan.FromHours(24);
            _ = CheckForUpdateAsync(silent: true);
        };
        SyncUpdateChecks();
    }

    /// <summary>Silent commands used by the installer. Returns true if one was handled.</summary>
    private static bool HandleInstallerCommand(string[] args)
    {
        try
        {
            if (args.Contains("--connect-assistant"))
            {
                // Never replace someone's existing status line without asking.
                if (AssistantSetup.GetStatus() == SetupStatus.NotConfigured) AssistantSetup.Install();
                return true;
            }
            if (args.Contains("--uninstall-cleanup"))
            {
                AssistantSetup.Uninstall();
                Autostart.Set(false);
                Legacy.RemoveOldAutostart();
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Write("Installer-Befehl fehlgeschlagen: " + ex);
            return true;
        }
        return false;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // The pet may have wandered off since the last drag.
        if (_pet != null && Settings?.WalkAround == true)
        {
            try
            {
                Settings.PositionX = _pet.UsualLeft;
                Settings.PositionY = _pet.Top;
                Settings.Save();
            }
            catch (IOException) { }
        }
        _tray?.Dispose();
        _showEvent?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    // ---------------------------------------------------------------- usage evaluation

    private void OnClock()
    {
        // Pick up hand edits of settings.json (e.g. bubble texts).
        try
        {
            var write = File.GetLastWriteTimeUtc(DataPaths.SettingsFile);
            if (write != _settingsWrite)
            {
                _settingsWrite = write;
                Settings = AppSettings.Load();
                ApplySettings(save: false);
            }
        }
        catch (IOException) { }

        // Re-evaluate every second: reset times pass, "working" expires, countdowns tick.
        Evaluate(initial: false);
        _sessions.Poll();
        AnnounceAchievement();
    }

    /// <summary>
    /// Cheers about the next achievement reached, once the pet has finished speaking and the last pop-up is gone:
    /// with the pop-up if it is switched on, else in a bubble or notification.
    /// </summary>
    private void AnnounceAchievement()
    {
        if (_announcements.Count == 0 || _pet.Speaking || _achievementPopup != null) return;
        var achievement = _announcements.Dequeue();
        _pet.Cheer(TimeSpan.FromSeconds(3));
        if (Settings.AchievementPopup)
        {
            ShowAchievementPopup(achievement);
            return;
        }
        (string, string)[] extra = [("{achievement}", Strings.AchievementTitle(achievement.Id)), ("{item}", Strings.RewardName(achievement))];
        if (Settings.SpeechBubbles) Say("Achievement", extra: extra);
        else if (Settings.Notifications)
            ShowNotification("Claudius", Strings.AchievementNotification(Strings.AchievementTitle(achievement.Id), Strings.RewardName(achievement)));
    }

    /// <summary>The banner for an achievement in the middle of the main screen: the pet cheering with its reward.</summary>
    public void ShowAchievementPopup(Achievement achievement)
    {
        _achievementPopup?.Close();
        var frame = new SpriteFrame(Eyes.Happy, Mouth.Open, Arms.Up, Outfit: achievement.Reward);
        if (achievement.Id == AchievementId.LostSock) frame = frame with { Character = PetCharacter.Sock };
        int done = Achievements.All.Count(a => Achievements.IsUnlocked(a.Id));
        _achievementPopup = new AchievementPopup(Strings.AchievementTitle(achievement.Id),
            Strings.AchievementReward(Strings.RewardName(achievement)), Strings.AchievementCount(done, Achievements.All.Length) + " · " + Strings.AchievementPopupClick,
            Sprite.Render(frame), (System.Windows.Media.Brush)Resources["AccentBrush"], () => ShowInventory(achievements: true));
        _achievementPopup.Closed += (_, _) => _achievementPopup = null;
        _achievementPopup.Show();
    }

    private void Evaluate(bool initial)
    {
        var now = DateTimeOffset.Now;
        _state = UsageState.Compute(_monitor.Current, Settings.Thresholds, now);
        var mood = _state.Mood;

        if (!initial && _lastMood is { } last && mood != last && mood != PetMood.Unknown)
        {
            if (mood > last && mood >= PetMood.Normal)
            {
                if (mood == PetMood.Exhausted && Settings.BreakReminder) SuggestBreak(limit: true, now);
                else Say(mood.ToString());
            }
            else if (mood < last && last >= PetMood.Nervous && mood <= PetMood.Normal)
            {
                Say("Reset");
                _pet.Cheer(TimeSpan.FromSeconds(4));
            }
        }
        _lastMood = mood;
        _pet.SetMood(mood, Working);
        var family = Sprite.FamilyOf(_state.Snapshot?.Model);
        _pet.SetOutfit(Settings.ModelOutfits ? Settings.OutfitFor(family) : Outfit.None);
        Achievements.ModelSeen(family);
        if (mood == PetMood.Exhausted) Achievements.Unlock(AchievementId.LimitReached);
        ApplyOccasion(initial);
        if (!initial) CheckWeekReview(now);
        if (_breaks.Update(Working, now, TimeSpan.FromMinutes(Settings.BreakAfterMinutes)) && Settings.BreakReminder && !initial)
            SuggestBreak(limit: false, now);
        if (_breakSuggested && _breaks.OnBreak(now))
        {
            _breakSuggested = false;
            Achievements.Unlock(AchievementId.BreakTaken);
        }

        var snapshot = _state.Snapshot;
        CheckWarning(true, snapshot?.FiveHour, _state.Session, Settings.SessionWarnThresholds, _sessionWarn, initial, now);
        CheckWarning(false, snapshot?.SevenDay, _state.Week, Settings.WeekWarnThresholds, _weekWarn, initial, now);

        _tray?.Update(mood, _state.Max == null
            ? Strings.TrayWaiting
            : Strings.TrayUsage(Strings.Percent(PercentText(_state.Session)), Strings.Percent(PercentText(_state.Week))));

        _overlay?.Update(_state, _monitor.History, Settings, AssistantSetup.GetStatus(), now);
    }

    /// <summary>Seasonal or night headwear and sleepiness; says good night when the night starts, not on start-up.</summary>
    private void ApplyOccasion(bool initial)
    {
        var time = Occasions.Now;
        bool night = Occasions.IsNight(time, Settings);
        var accessory = Occasions.For(time, Settings);
        _pet.SetAccessory(accessory);
        _pet.SetNight(night);
        if (night) Achievements.Unlock(AchievementId.NightOwl);
        else if (accessory != Accessory.None) Achievements.Unlock(AchievementId.Celebration);
        if (night && _wasNight == false && !initial) Say("GoodNight");
        if (!night && _wasNight == true && !initial) Say("GoodMorning");
        _wasNight = night;
    }

    /// <summary>
    /// Sums up the past week once after its reset, also if the app was not running then (up to 3 days later).
    /// Rechecked when the history changes and once a minute, as a reset can pass without new data.
    /// </summary>
    private void CheckWeekReview(DateTimeOffset now)
    {
        if (_pet.Speaking) return;   // e.g. the greeting; it comes once the bubble is gone
        if (ReferenceEquals(_reviewedHistory, _monitor.History) && now < _reviewCheckAt) return;
        _reviewedHistory = _monitor.History;
        _reviewCheckAt = now.AddMinutes(1);

        var review = WeekReview.Last(_monitor.History, now);
        if (review == null || review.ResetsAt <= Settings.LastWeekReview + 600) return;
        Settings.LastWeekReview = review.ResetsAt;
        SaveSettings();
        if (now - review.ResetsAtTime > TimeSpan.FromDays(3)) return;

        string percent = PercentText(review.Percent);
        string? day = review.StrongestDay is { } d ? Strings.DayName(d) : null;
        bool quiet = review.Percent < 1 || day == null;
        if (Settings.SpeechBubbles)
            Say(quiet ? "WeekReviewQuiet" : "WeekReview", extra: [("{lastweek}", percent), ("{day}", day ?? "")]);
        else if (Settings.Notifications)
            ShowNotification("Claudius", Strings.LastWeek(Strings.Percent(percent), day));
    }

    /// <summary>Stretches and suggests a break: after long work without one, or when a limit is reached.</summary>
    private void SuggestBreak(bool limit, DateTimeOffset now)
    {
        _pet.Stretch();
        _breakSuggested = true;
        string hours = Strings.WorkTime(_breaks.Worked(now));
        if (Settings.SpeechBubbles)
            Say(limit ? "BreakLimit" : "Break", extra: [("{hours}", hours)]);
        else if (Settings.Notifications && !limit)   // reaching the limit has its own notification
            ShowNotification("Claudius", Strings.BreakNotification(hours));
    }

    private sealed class WarnState
    {
        public long ResetsAt;
        public int Highest;
    }

    private void CheckWarning(bool session, RateWindow? window, double? value, List<int> thresholds,
        WarnState state, bool initial, DateTimeOffset now)
    {
        if (window == null || value == null) return;
        if (Math.Abs(window.ResetsAt - state.ResetsAt) > 600)
        {
            state.ResetsAt = window.ResetsAt;   // new window: re-arm all thresholds
            state.Highest = 0;
        }

        int crossed = thresholds.Where(t => value >= t).DefaultIfEmpty(0).Max();
        if (crossed <= state.Highest) return;
        state.Highest = crossed;
        if (initial || !Settings.Notifications) return;

        string text = value >= 100
            ? Strings.LimitUsedUp(session, Format.DayTime(window.ResetsAtTime, now))
            : Strings.LimitReached(session, Strings.Percent(Format.Percent(value.Value)));
        ShowNotification("Claudius", text);
    }

    private void Say(string key, string? folder = null, (string Placeholder, string Value)[]? extra = null)
    {
        if (!Settings.SpeechBubbles) return;
        if (!Settings.Texts.TryGetValue(key, out var variants) || variants.Count == 0) return;

        string name = Settings.UserName;
        string text = variants[_random.Next(variants.Count)]
            .Replace("{NAME}", name.ToUpperInvariant())
            .Replace("{name}", name)
            .Replace("{folder}", folder ?? "")
            .Replace("{percent}", PercentText(_state.Max))
            .Replace("{session}", PercentText(_state.Session))
            .Replace("{week}", PercentText(_state.Week))
            .Replace("{version}", AvailableUpdate?.VersionText ?? "");
        foreach (var (placeholder, value) in extra ?? [])
            text = text.Replace(placeholder, value);
        text = text.Replace(" %", " %"); // keep "62 %" together when wrapping
        _pet.Say(text, Settings.BubbleDurationSeconds);
    }

    private static string PercentText(double? value) => value is { } v ? Format.Percent(v) : "–";

    // ---------------------------------------------------------------- actions (menus, tray, settings)

    public void ShowNotification(string title, string text) => _tray?.ShowBalloon(title, text);

    public void ShowUsage()
    {
        if (_overlay != null)
        {
            _overlay.Activate();
            return;
        }

        _overlay = new UsageOverlay();
        _overlay.Closed += (_, _) =>
        {
            _overlay = null;
            _overlayClosedAt = DateTime.Now;
            _pet.PauseWalking(false);
        };
        _pet.PauseWalking(true);
        _overlay.Update(_state, _monitor.History, Settings, AssistantSetup.GetStatus(), DateTimeOffset.Now);

        Rect anchor;
        if (_pet.IsVisible)
        {
            anchor = _pet.PetBounds;
        }
        else
        {
            var area = SystemParameters.WorkArea;
            anchor = new Rect(area.Right - 180, area.Bottom, 160, 0);
        }
        _overlay.ShowNear(anchor, _pet);
    }

    private void OnPetClicked(DateTime releasedAt)
    {
        // The click that deactivated (and closed) the overlay should not reopen it.
        if (_overlay == null && releasedAt - _overlayClosedAt < TimeSpan.FromMilliseconds(600)) return;
        if (_overlay != null) _overlay.Close();
        else ShowUsage();
    }

    /// <summary>Brings the terminal tab or Desktop app window of a session to the front.</summary>
    private async void GoToSession(SessionView session)
    {
        if (await SessionWindow.ActivateAsync(session)) return;
        Log.Write("Kein Fenster für Session " + session.Id + " in " + session.Folder + " gefunden.");
        Say("NoWindow", session.Label);
    }

    public void ShowPet()
    {
        if (!_pet.IsVisible) _pet.Show();
    }

    public void TogglePetVisible()
    {
        if (_pet.IsVisible) _pet.Hide();
        else _pet.Show();
    }

    public void ToggleAlwaysOnTop()
    {
        Settings.AlwaysOnTop = !Settings.AlwaysOnTop;
        ApplySettings(save: true);
    }

    public void ToggleWalkAround()
    {
        Settings.WalkAround = !Settings.WalkAround;
        // Stay where it is when it stops walking.
        Settings.PositionX = _pet.UsualLeft;
        Settings.PositionY = _pet.Top;
        ApplySettings(save: true);
    }

    public void ShowSettings()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(this, Settings);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    public void ShowInventory() => ShowInventory(achievements: false);

    /// <summary>Opens the inventory, or brings it to the front; on the achievements tab if asked.</summary>
    public void ShowInventory(bool achievements)
    {
        if (_inventoryWindow == null)
        {
            _inventoryWindow = new InventoryWindow(this, Settings, Sprite.FamilyOf(_state.Snapshot?.Model));
            _inventoryWindow.Closed += (_, _) => _inventoryWindow = null;
            _inventoryWindow.Show();
        }
        if (achievements) _inventoryWindow.ShowAchievementsTab();
        _inventoryWindow.Activate();
    }

    private void ApplySessions()
    {
        foreach (var session in _sessions.Sessions) Achievements.SessionSeen(session.Id);
        _pet.SetSessions(_sessions.Sessions, _sessions.Overall, Settings.SessionMarks);
        _pet.SetMood(_state.Mood, Working);
    }

    /// <summary>
    /// The assistant is working: the status line just reported, or a hooked session is busy. The latter also
    /// covers the Desktop app, which may not run the status line.
    /// </summary>
    private bool Working => _state.Active || (Settings.SessionMarks && _sessions.AnyWorking);

    /// <summary>
    /// Once the assistant is connected: statusLine and ?/! hooks point at this copy's bridge, and the hooks
    /// follow the setting.
    /// </summary>
    private void SyncSessionHooks()
    {
        try
        {
            if (AssistantSetup.GetStatus() != SetupStatus.Connected) return;
            if (AssistantSetup.Repoint()) Log.Write("statusLine auf " + AssistantSetup.BridgeCommand + " umgestellt.");
            AssistantSetup.SetHooks(Settings.SessionMarks);
        }
        catch (Exception ex)
        {
            Log.Write("Hooks des KI-Assistenten konnten nicht angepasst werden: " + ex.Message);
        }
    }

    public void ApplySettings(bool save)
    {
        if (save) SaveSettings();
        if (Settings.Language != Strings.Current) ApplyLanguage();
        SyncSessionHooks();
        ContextLevels.Thresholds = Settings.ContextThresholds;
        ApplySessions();
        ApplyPetColor();
        SyncMascotTheme();
        _pet.ApplySettings(Settings);
        _monitor.SetInterval(Settings.PollIntervalSeconds);
        if (Settings.StartWithWindows != Autostart.IsEnabled()) Autostart.Set(Settings.StartWithWindows);
        SyncUpdateChecks();
        Evaluate(initial: true);
    }

    /// <summary>Shows the pet's settings while they are being changed in the settings, without saving them.</summary>
    public void PreviewPetSettings()
    {
        _pet.ApplySettings(Settings);
        Evaluate(initial: true);
    }

    /// <summary>Menus that stay around are built again in the new language; windows pick it up when opened.</summary>
    private void ApplyLanguage()
    {
        Strings.Use(Settings.Language!);
        _pet.PetImage.ContextMenu = BuildContextMenu();
        _pet.ApplyLanguage();
    }

    /// <summary>Keeps the assistant's theme in line with <see cref="AppSettings.TerminalMascotColor"/>.</summary>
    private void SyncMascotTheme()
    {
        try
        {
            if (Settings.TerminalMascotColor)
            {
                if (!Settings.MascotThemeSwitched)
                {
                    string? current = AssistantSetup.CurrentTheme();
                    Settings.MascotThemeBefore = current == "custom:" + AssistantSetup.ThemeSlug ? null : current;
                }
                AssistantSetup.WritePetTheme(Settings.PetColor, Settings.MascotThemeBefore);
                if (!Settings.MascotThemeSwitched)
                {
                    // Only switch once: if the user picks another theme in the assistant later, that choice stays.
                    AssistantSetup.SelectPetTheme();
                    Settings.MascotThemeSwitched = true;
                    SaveSettings();
                }
            }
            else if (Settings.MascotThemeSwitched)
            {
                if (AssistantSetup.IsPetThemeSelected()) AssistantSetup.SelectTheme(Settings.MascotThemeBefore);
                AssistantSetup.DeletePetTheme();
                Settings.MascotThemeSwitched = false;
                Settings.MascotThemeBefore = null;
                SaveSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                       or System.Text.Json.JsonException)
        {
            Log.Write("Theme des KI-Assistenten konnte nicht geschrieben werden: " + ex);
            ShowNotification("Claudius", Strings.ThemeNotWritten + ex.Message);
        }
    }

    /// <summary>The pet's color is also the accent color of the overlay, menus and speech bubble.</summary>
    private void ApplyPetColor() => ApplyPetColor(ParsePetColor());

    private void ApplyPetColor(uint color)
    {
        Sprite.SetCharacter(Settings.Character);
        Sprite.SetBodyColor(color);
        var accent = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(
            (byte)(color >> 16), (byte)(color >> 8), (byte)color));
        accent.Freeze();
        Resources["AccentBrush"] = accent;
    }

    /// <summary>
    /// Shows a color on the pet, tray and accents without saving it, while it is being picked in the settings.
    /// Null goes back to the saved color.
    /// </summary>
    public void PreviewPetColor(uint? color)
    {
        int previous = Sprite.Version;
        ApplyPetColor(color ?? ParsePetColor());
        if (Sprite.Version == previous) return;
        _pet.Redraw();
        Evaluate(initial: true);
    }

    private uint ParsePetColor() =>
        Sprite.TryParseColor(Settings.PetColor, out uint color) ? color : Sprite.DefaultBodyColor;

    private void SaveSettings()
    {
        try
        {
            Settings.Save();
            _settingsWrite = File.GetLastWriteTimeUtc(DataPaths.SettingsFile);
        }
        catch (IOException ex)
        {
            Log.Write("Einstellungen konnten nicht gespeichert werden: " + ex.Message);
        }
    }

    // ---------------------------------------------------------------- opening the assistant

    public void ShowProjectMenu() => ShowProjectMenu(voice: false);

    private void ShowProjectMenu(bool voice)
    {
        // First use: nothing to offer yet, so ask for the repo folder right away.
        if (Settings.ReposPath == null && Settings.RecentProjects.Count == 0 && !ChooseReposFolder()) return;

        var menu = ProjectMenu.Build(Settings, folder => LaunchAssistant(folder, voice), () => ChooseAndLaunch(voice), () =>
        {
            if (ChooseReposFolder()) ShowProjectMenu(voice);
        });
        if (_pet.IsVisible)
        {
            menu.PlacementTarget = _pet.PetImage;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        }
        menu.IsOpen = true;
    }

    /// <summary>
    /// The assistant has no flag to start in voice mode, so voice dictation is switched on in its
    /// settings and the user is told how to talk (hold Space).
    /// </summary>
    public void StartVoiceChat()
    {
        if (!Settings.VoiceChat) return;
        if (!AssistantSetup.IsVoiceEnabled())
        {
            var answer = MessageBox.Show(Strings.VoiceEnableQuestion(AssistantSetup.SettingsPath),
                "Claudius", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
            try
            {
                AssistantSetup.SetVoiceEnabled(true);
            }
            catch (Exception ex)
            {
                Log.Write("Sprachdiktat konnte nicht eingeschaltet werden: " + ex);
                MessageBox.Show(Strings.AssistantSettingsError + ex.Message, "Claudius",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }
        ShowProjectMenu(voice: true);
    }

    public void LaunchAssistant(string folder) => LaunchAssistant(folder, voice: false);

    private void LaunchAssistant(string folder, bool voice, string? prompt = null)
    {
        try
        {
            if (AssistantLauncher.Launch(Settings, folder, prompt) is { } desktop) _ = ReportDesktopFailure(desktop);
        }
        catch (DirectoryNotFoundException)
        {
            Settings.RecentProjects.Remove(folder);
            SaveSettings();
            Say("NoFolder");
            return;
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
        {
            Log.Write("KI-Assistent konnte nicht gestartet werden: " + ex.Message);
            MessageBox.Show(ex.Message, "Claudius", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        SaveSettings();
        _pet.Cheer(TimeSpan.FromSeconds(2));
        Say(voice ? "Voice" : "Launch", AssistantLauncher.FolderName(folder));
    }

    /// <summary>The CLI's <c>--desktop</c> fails quietly (CLI too old, Desktop missing); say so instead.</summary>
    private async Task ReportDesktopFailure(System.Diagnostics.Process process)
    {
        using (process)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { return; }
            if (process.ExitCode == 0) return;
        }
        Log.Write(Strings.DesktopFailed);
        MessageBox.Show(Strings.DesktopFailed, "Claudius", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnGhostDropped(string? folder, bool overShell)
    {
        if (folder != null) LaunchAssistant(folder);
        else if (overShell) Say("NoFolder");   // e.g. "This PC"; anywhere else the drop is just a cancel
    }

    /// <summary>
    /// A folder dropped onto the pet opens like a ghost drop. Files open in their folder and go to the
    /// assistant as <c>@</c> mentions.
    /// </summary>
    private void OnFilesDropped(string[] paths)
    {
        if (paths is [var single] && Directory.Exists(single))
        {
            LaunchAssistant(single, voice: false);
            return;
        }
        if (Path.GetDirectoryName(paths[0]) is not { Length: > 0 } folder)
        {
            Say("NoFolder");
            return;
        }
        LaunchAssistant(folder, voice: false, AssistantLauncher.MentionPrompt(folder, paths));
    }

    private void ChooseAndLaunch(bool voice)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Strings.ChooseLaunchFolder };
        if (Settings.ReposPath != null) dialog.InitialDirectory = Settings.ReposPath;
        if (dialog.ShowDialog() == true) LaunchAssistant(dialog.FolderName, voice);
    }

    /// <summary>Asks for the folder containing all projects. Returns false if cancelled.</summary>
    private bool ChooseReposFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Strings.ChooseReposFolder };
        if (Settings.ReposPath != null) dialog.InitialDirectory = Settings.ReposPath;
        if (dialog.ShowDialog() != true) return false;
        Settings.ReposPath = dialog.FolderName;
        SaveSettings();
        return true;
    }

    private void SavePosition()
    {
        Settings.PositionX = _pet.UsualLeft;
        Settings.PositionY = _pet.Top;
        ApplySettings(save: true);
    }

    public void ConnectAssistant(Window? owner)
    {
        const string title = "Claudius";
        var status = AssistantSetup.GetStatus();
        string path = AssistantSetup.SettingsPath;

        switch (status)
        {
            case SetupStatus.Connected:
                MessageBox.Show(Strings.AlreadyConnected, title,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            case SetupStatus.BridgeMissing:
                MessageBox.Show(Strings.BridgeNotFound(AssistantSetup.BridgePath), title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
        }

        string question = status == SetupStatus.OtherStatusLine
            ? Strings.ReplaceStatusLine(path, AssistantSetup.CurrentCommand())
            : Strings.AddStatusLine(path);
        var answer = owner != null
            ? MessageBox.Show(owner, question, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(question, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            AssistantSetup.Install(hooks: Settings.SessionMarks);
            MessageBox.Show(Strings.Connected, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Write("KI-Assistent konnte nicht verbunden werden: " + ex);
            MessageBox.Show(Strings.AssistantSettingsError + ex.Message, title,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        Evaluate(initial: false);
    }

    // ---------------------------------------------------------------- updates

    /// <summary>Looks for a new release about 30 s after the start and then once a day, unless switched off.</summary>
    private void SyncUpdateChecks()
    {
        bool on = Settings.CheckForUpdates && !Updater.IsDevBuild;
        if (on && !_updateTimer.IsEnabled)
        {
            _updateTimer.Interval = TimeSpan.FromSeconds(30);
            _updateTimer.Start();
        }
        else if (!on)
        {
            _updateTimer.Stop();
        }
    }

    /// <summary>
    /// Asks GitHub for a newer release and remembers it for the menus; the pet mentions each new version once.
    /// Network errors are only logged when silent, otherwise thrown.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync(bool silent)
    {
        try
        {
            AvailableUpdate = await Updater.CheckAsync();
        }
        catch (Exception ex) when (silent)
        {
            Log.Write("Update-Prüfung fehlgeschlagen: " + ex.Message);
            return AvailableUpdate;
        }
        if (AvailableUpdate is { } update && Settings.NotifiedUpdate != update.VersionText)
        {
            Settings.NotifiedUpdate = update.VersionText;
            SaveSettings();
            if (Settings.SpeechBubbles) Say("Update");
            else ShowNotification("Claudius", Strings.UpdateNotification(update.VersionText));
        }
        return AvailableUpdate;
    }

    /// <summary>
    /// Installed via setup: asks, then downloads and installs the update; the setup starts the new version.
    /// Portable copies just open the release page.
    /// </summary>
    public async void InstallUpdate(Window? owner = null)
    {
        if (AvailableUpdate is not { } update || _updating) return;
        if (!Updater.IsInstalled)
        {
            OpenUrl(update.PageUrl);
            return;
        }

        const string title = "Claudius – Update";
        string question = Strings.InstallUpdateQuestion(update.VersionText);
        var answer = owner != null
            ? MessageBox.Show(owner, question, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(question, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        _updating = true;
        Say("Updating");
        try
        {
            await Updater.InstallAsync(update);
            Quit();
        }
        catch (Exception ex)
        {
            Log.Write("Update fehlgeschlagen: " + ex);
            MessageBox.Show(Strings.UpdateFailed + ex.Message, title,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _updating = false;
        }
    }

    /// <summary>Menu text for the available update, depending on whether it can be installed in place.</summary>
    public string UpdateMenuText => AvailableUpdate is not { } update ? ""
        : Updater.IsInstalled ? Strings.InstallUpdateMenu(update.VersionText) : Strings.DownloadVersionMenu(update.VersionText);

    public static void OpenUrl(string url) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

    public void Quit()
    {
        _overlay?.Close();
        _tray?.Dispose();
        _tray = null;
        Shutdown();
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu { Style = (Style)FindResource("PetMenu") };
        MenuItem Item(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
            return item;
        }

        Item(Strings.ShowUsage, ShowUsage);
        Item(Strings.OpenAssistant, ShowProjectMenu);
        var voice = Item(Strings.StartVoiceChat, StartVoiceChat);
        Item(Strings.SayHello, () =>
        {
            _pet.Cheer(TimeSpan.FromSeconds(2));
            if (Settings.SpeechBubbles) Say("Poke");
        });
        menu.Items.Add(new Separator());
        var onTop = Item(Strings.AlwaysOnTop, ToggleAlwaysOnTop);
        onTop.IsCheckable = true;
        var walk = Item(Strings.WalkAround, ToggleWalkAround);
        walk.IsCheckable = true;
        Item(Strings.MinimizeToTray, () => _pet.Hide());
        menu.Items.Add(new Separator());
        var update = Item("", () => InstallUpdate());
        Item(Strings.ConnectMenu, () => ConnectAssistant(null));
        Item(Strings.InventoryMenu, ShowInventory);
        Item(Strings.SettingsMenu, ShowSettings);
        menu.Items.Add(new Separator());
        Item(Strings.Quit, Quit);

        menu.Opened += (_, _) =>
        {
            onTop.IsChecked = Settings.AlwaysOnTop;
            walk.IsChecked = Settings.WalkAround;
            voice.Visibility = Settings.VoiceChat ? Visibility.Visible : Visibility.Collapsed;
            update.Header = UpdateMenuText;
            update.Visibility = AvailableUpdate != null ? Visibility.Visible : Visibility.Collapsed;
        };
        return menu;
    }
}

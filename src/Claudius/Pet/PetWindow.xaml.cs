using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Claudius.Core;
using Claudius.Shared;
using Forms = System.Windows.Forms;

namespace Claudius.Pet;

public partial class PetWindow : Window
{
    private const double BubbleArea = 130;
    private const double MinWidth_ = 260;
    /// <summary>Height of the emote buttons plus their gap to the pet.</summary>
    private const double EmoteBarArea = 34;
    /// <summary>Room for the speech bubble next to the climbing pet: tail, widest bubble and its shadow.</summary>
    private const double BubbleBesideArea = 262;

    private readonly DispatcherTimer _animationTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(1000.0 / PetAnimator.TicksPerSecond),
    };
    private readonly DispatcherTimer _bubbleTimer = new();
    private readonly DispatcherTimer _walkTimer = new() { Interval = TimeSpan.FromMilliseconds(1000.0 / 30) };
    private readonly Stopwatch _walkClock = new();
    private readonly PetWalker _walker = new();
    private readonly DesktopSurfaces _surfaces = new();
    private TimeSpan _lastScan;
    private TimeSpan _lastTopCheck;
    // A single click waits for the double-click time, so a double-click does not also flash the overlay.
    private readonly DispatcherTimer _clickTimer = new() { Interval = TimeSpan.FromMilliseconds(GetDoubleClickTime()) };
    private DateTime _clickedAt;

    // Right-button drag: a ghost copy of the pet is carried to an Explorer window.
    private readonly DispatcherTimer _ghostTimer = new() { Interval = TimeSpan.FromMilliseconds(15) };
    private GhostWindow? _ghost;
    private Point? _rightPressPoint;
    private bool _suppressContextMenu;
    private bool _ghostDrag = true;
    /// <summary>Files from Explorer are being dragged over the pet: it opens its mouth and reaches for them.</summary>
    private bool _filesOver;
    private double _scale = 5;

    private long _tick = 1;
    private PetMood _mood = PetMood.Unknown;
    private bool _working;
    private bool _animations = true;
    private DateTime _cheerUntil;
    private TimeSpan _lastTick;
    private Point? _pressPoint;
    private bool _walking;
    private bool _dragging;
    private bool _paused;
    private bool _needsPlace = true;
    private bool _petOnTop;
    /// <summary>Window width without the room for the overlays next to the climbing pet.</summary>
    private double _usualWidth;
    /// <summary>The side the overlays are laid out on: next to the pet climbing a wall (-1, 1) or not (0).</summary>
    private int _overlaySide;
    /// <summary>Room above the climbing pet, so the upright emote buttons can be centered on it (DIPs).</summary>
    private double _petInset;
    private bool _listening;
    private Mark _sessionMark = Mark.None;
    private Outfit _outfit = Outfit.None;
    private Snack _snack = Snack.Cookie;
    private Accessory _accessory = Accessory.None;
    private bool _night;
    private IReadOnlyList<SessionView> _sessions = [];
    private readonly DispatcherTimer _reaperTimer = new() { Interval = TimeSpan.FromMilliseconds(1000.0 / 30) };
    private readonly Stopwatch _reaperClock = Stopwatch.StartNew();
    private TimeSpan _lastReaperTick;
    private bool _topmost = true;
    private (Motion Motion, int Direction) _pose = (Motion.Idle, 1);
    private Eyes? _lookAt;
    private Eyes? _gaze;
    private bool _sad;
    /// <summary>Night: the pet goes to bed (on the taskbar, or right where it is if it does not walk around).</summary>
    private bool _bedtime;
    private bool _shownInBed;
    private DateTime _splatStart;
    private int _squashShown;
    private BedWindow? _bedWindow;
    private double _lastPetWidth;
    /// <summary>Picked up while asleep: by the blanket the bed comes along, by the head the pet comes out.</summary>
    private bool _carryingBed, _pulledOut;
    private DateTime _groggyUntil, _wakeUntil;
    private readonly DispatcherTimer _wakeTimer = new() { Interval = TimeSpan.FromSeconds(1.6) };
    private readonly Random _bedRandom = new();
    private bool _emotesEnabled = true;
    private Emote? _emote;
    private DateTime _emoteStart;
    /// <summary>Fed while sick: the current Feed emote is a polite no.</summary>
    private bool _refusing;
    /// <summary>How sick the fullest session's context window makes the pet, 0..3.</summary>
    private int _sick;
    /// <summary>Recovering after /compact or /clear: since when and from which sick level.</summary>
    private (DateTime Start, int From)? _recovery;
    /// <summary>Stretching for a break reminder: since when.</summary>
    private DateTime? _stretchStart;
    /// <summary>The grim reaper floating behind the pet while a context window is at the top sick level.</summary>
    private ReaperWindow? _reaper;
    // Leaving the pet towards the emote buttons (or back) must not hide them on the way.
    private readonly DispatcherTimer _emoteHideTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    /// <summary>Left click without dragging; carries the time the button was released.</summary>
    public event Action<DateTime>? Clicked;
    /// <summary>Left double-click.</summary>
    public event Action? DoubleClicked;
    /// <summary>
    /// The ghost was dropped: the folder under it (null if none) and whether it was over Explorer
    /// or the desktop at all.
    /// </summary>
    public event Action<string?, bool>? GhostDropped;
    /// <summary>Files or folders from Explorer were dropped onto the pet.</summary>
    public event Action<string[]>? FilesDropped;
    /// <summary>The "?" was clicked: bring that session's window to the front.</summary>
    public event Action<SessionView>? SessionClicked;
    /// <summary>The pet was dragged to a new position.</summary>
    public event Action? Moved;
    /// <summary>One of the emote buttons was clicked; the pet already reacts to it.</summary>
    public event Action<Emote>? Emoted;
    /// <summary>The pet landed from a long fall and went splat.</summary>
    public event Action? Splatted;
    /// <summary>The pet took off for a jump or a hop.</summary>
    public event Action? Jumped;

    public PetWindow()
    {
        InitializeComponent();
        _animationTimer.Tick += (_, _) => { _tick++; Render(); RenderReaper(); };
        _bubbleTimer.Tick += (_, _) => HideBubble();
        _emoteHideTimer.Tick += (_, _) =>
        {
            _emoteHideTimer.Stop();
            if (!PetImage.IsMouseOver && !EmoteBar.IsMouseOver) HideEmotes();
        };
        _walkTimer.Tick += (_, _) => Walk();
        _reaperTimer.Tick += (_, _) => FollowWithReaper();
        Closed += (_, _) =>
        {
            _reaper?.Close();
            CloseBed();
        };
        IsVisibleChanged += (_, _) => { if (!IsVisible) _bedWindow?.Hide(); };
        BedBackImage.Source = Sprite.RenderBed(BedLayer.Back);
        BedFrontImage.Source = Sprite.RenderBed(BedLayer.Front);
        _wakeTimer.Tick += (_, _) =>
        {
            _wakeTimer.Stop();
            EndBedtime();
        };
        _ghostTimer.Tick += (_, _) => UpdateGhost();
        _clickTimer.Tick += (_, _) =>
        {
            _clickTimer.Stop();
            if (InBed)
            {
                // Woken up for a moment: a sleepy blink, then back to sleep.
                _groggyUntil = DateTime.Now.AddSeconds(2);
                Render();
            }
            Clicked?.Invoke(_clickedAt);
        };
        SourceInitialized += (_, _) => HideFromAltTab();
        ApplyLanguage();
    }

    /// <summary>Tooltips of the emote buttons in the current language.</summary>
    public void ApplyLanguage()
    {
        foreach (var button in EmoteBar.Children.OfType<Button>())
            if (button.Tag is string tag && Enum.TryParse(tag, out Emote emote)) button.ToolTip = Strings.EmoteName(emote);
    }

    public void ApplySettings(AppSettings settings, bool initial = false)
    {
        Topmost = _topmost = settings.AlwaysOnTop;
        _snack = settings.Snack;
        FeedButton.Content = _snack == Snack.Beans ? "🫘" : "🍪";
        if (_reaper != null)
        {
            _reaper.Topmost = _topmost;
            _reaper.SetScale(settings.PetScale);
        }
        _animations = settings.Animations;
        _ghostDrag = settings.GhostDrag;
        _emotesEnabled = settings.Emotes;
        if (!_emotesEnabled) HideEmotes();
        _scale = settings.PetScale;

        double petWidth = Sprite.Width * settings.PetScale;
        double petHeight = Sprite.Height * settings.PetScale;
        double tearsWidth = (Sprite.Width + 2 * Sprite.TearMargin) * settings.PetScale;
        double splatWidth = (Sprite.Width + 2 * Sprite.SplatMargin) * settings.PetScale;
        double newWidth = Math.Max(MinWidth_, Math.Max(tearsWidth, splatWidth) + 20);
        _usualWidth = newWidth;
        double newHeight = BubbleArea + petHeight;

        if (initial)
        {
            Width = newWidth;
            Height = newHeight;
            var area = SystemParameters.WorkArea;
            Left = settings.PositionX ?? area.Right - newWidth - 20;
            Top = settings.PositionY ?? area.Bottom - newHeight - 4;
        }
        else
        {
            // Keep the pet's feet where they are when the size changes.
            double bottom = Top + Height, center = Left + Width / 2;
            Width = newWidth;
            Height = newHeight;
            Left = center - newWidth / 2;
            Top = bottom - newHeight;
        }
        ClampToScreen();

        PetImage.Width = petWidth;
        PetImage.Height = petHeight;
        TearsImage.Width = tearsWidth;
        TearsImage.Height = petHeight;
        SplatImage.Width = splatWidth;
        SplatImage.Height = petHeight;
        BedBackImage.Width = BedFrontImage.Width = petWidth;
        BedBackImage.Height = BedFrontImage.Height = petHeight;
        if (_petOnTop) SetPetOnTop(true); // the bubble below the pet moves with its size

        if (_animations) _animationTimer.Start();
        else _animationTimer.Stop();

        _walking = settings.WalkAround && _animations;
        _walker.CrossMonitors = settings.CrossMonitors;
        _walker.ChaseCursor = settings.ChaseCursor;
        _needsPlace = true;
        if (_walking)
        {
            _walkClock.Restart();
            _lastTick = _lastScan = TimeSpan.Zero;
            _walkTimer.Start();
        }
        else
        {
            _walkTimer.Stop();
            _pose = (Motion.Idle, 1);
            if (_petOnTop) SetPetOnTop(false);
        }
        Render();
    }

    /// <summary>
    /// <see cref="Window.Left"/> at the usual window width, to be saved: the window is wider while the pet climbs.
    /// </summary>
    public double UsualLeft => Left + (Width - _usualWidth) / 2;

    public void SetMood(PetMood mood, bool working)
    {
        if (mood == _mood && working == _working) return;
        _mood = mood;
        _working = working;
        Render();
    }

    /// <summary>What the pet wears, e.g. the outfit for the AI model in use.</summary>
    public void SetOutfit(Outfit outfit)
    {
        if (outfit == _outfit) return;
        _outfit = outfit;
        Render();
    }

    /// <summary>Headwear for the season or the night.</summary>
    public void SetAccessory(Accessory accessory)
    {
        if (accessory == _accessory) return;
        _accessory = accessory;
        Render();
    }

    /// <summary>Night: calm moods get sleepy and walk slower.</summary>
    public void SetNight(bool night)
    {
        if (night == _night) return;
        _night = night;
        _walker.Sleepy = night;
        if (night)
        {
            _bedtime = true;
            _wakeTimer.Stop();
            _wakeUntil = default;
        }
        else if (_bedtime)
        {
            // Morning: a stretch in bed first, then the bed goes; out of bed it just goes.
            if (InBed)
            {
                _wakeUntil = DateTime.Now + _wakeTimer.Interval;
                _wakeTimer.Start();
            }
            else EndBedtime();
        }
        Render();
    }

    /// <summary>Lying in its bed: once it got there, or right away where it is while it does not walk around.</summary>
    private bool InBed => _bedtime && (_dragging ? _carryingBed : !_walking || _walker.InBed || _carryingBed);

    private void EndBedtime()
    {
        _bedtime = false;
        _carryingBed = false;
        _walker.BedX = null;
        CloseBed();
        Render();
    }

    private void CloseBed()
    {
        _bedWindow?.Close();
        _bedWindow = null;
    }

    /// <summary>
    /// At night: picks a spot for the bed on the taskbar and shows the bed there until the pet lies in it,
    /// e.g. while it walks over or after it was carried off.
    /// </summary>
    private void UpdateBed(double petWidth)
    {
        if (!_bedtime) return;
        if (_walking && _walker.BedX == null) _walker.BedX = BedSpot(petWidth);
        if (_carryingBed && !_dragging && _walker.InBed) _carryingBed = false; // landed in it
        bool inBed = InBed;
        if (inBed != _shownInBed)
        {
            _shownInBed = inBed;
            Render();
        }
        if (inBed || !_walking || !IsVisible || _walker.BedX is not { } x)
        {
            _bedWindow?.Hide();
            return;
        }
        if (_surfaces.Platforms.Where(p => p.IsFloor && p.Contains(x)).Select(p => (double?)p.Y).FirstOrDefault() is not { } floor)
            return;
        if (_bedWindow == null || _bedWindow.Scale != _scale)
        {
            CloseBed();
            _bedWindow = new BedWindow(_scale, _topmost);
        }
        if (!_bedWindow.IsVisible) _bedWindow.Show();
        _bedWindow.PlaceAt(x, floor, new WindowInteropHelper(this).Handle);
    }

    /// <summary>A random spot on the taskbar of the pet's monitor, not too far from it.</summary>
    private double BedSpot(double petWidth)
    {
        double x = _walker.X;
        var area = _surfaces.WorkAreas.FirstOrDefault(a => x >= a.Left && x <= a.Right);
        var floors = _surfaces.Platforms
            .Where(p => p.IsFloor && p.X2 - p.X1 > 3 * petWidth && p.X1 < area.Right && p.X2 > area.Left)
            .ToList();
        if (floors.Count == 0) return x;
        var floor = floors[_bedRandom.Next(floors.Count)];
        double lo = Math.Max(floor.X1 + petWidth, x - 15 * petWidth), hi = Math.Min(floor.X2 - petWidth, x + 15 * petWidth);
        return lo < hi ? lo + _bedRandom.NextDouble() * (hi - lo) : (floor.X1 + floor.X2) / 2;
    }

    /// <summary>In bed: asleep, a sleepy blink after a click, a stretch when it wakes up in the morning.</summary>
    private SpriteFrame BedFrame(long t)
    {
        var now = DateTime.Now;
        if (now < _wakeUntil) return new SpriteFrame(Eyes.Happy, Mouth.Open, Arms.Up);
        if (now < _groggyUntil) return new SpriteFrame(t % 12 < 9 ? Eyes.Blink : Eyes.Closed);
        var z = (t / 8 % 3) switch { 0 => Mark.Zzz1, 1 => Mark.Zzz2, _ => Mark.Zzz3 };
        return new SpriteFrame(Eyes.Closed, Bob: (int)(t / 12 % 2), Mark: z);
    }

    /// <summary>Short happy animation, e.g. after a reset.</summary>
    public void Cheer(TimeSpan duration)
    {
        _cheerUntil = DateTime.Now + duration;
        Render();
    }

    /// <summary>A speech bubble is showing.</summary>
    public bool Speaking => _bubbleTimer.IsEnabled;

    public void Say(string text, double seconds)
    {
        BubbleText.Text = text;
        Bubble.Visibility = Visibility.Visible;
        UpdateLayout();
        UpdateBubblePlacement();
        Bubble.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)));
        _bubbleTimer.Stop();
        _bubbleTimer.Interval = TimeSpan.FromSeconds(seconds);
        _bubbleTimer.Start();
    }

    public void HideBubble()
    {
        _bubbleTimer.Stop();
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
        fade.Completed += (_, _) =>
        {
            if (_bubbleTimer.IsEnabled) return;
            Bubble.Visibility = Visibility.Collapsed;
            UpdateBubblePlacement();
        };
        Bubble.BeginAnimation(OpacityProperty, fade);
    }

    // ---------------------------------------------------------------- the assistant sessions

    /// <summary>
    /// Shows "?" while a session waits for an answer, makes the pet look sick for the fullest session's context
    /// window and, at the top sick level, brings in the grim reaper.
    /// </summary>
    public void SetSessions(IReadOnlyList<SessionView> sessions, string? overall, bool marks)
    {
        _sessions = sessions;
        var mark = marks && overall == SessionStates.Question ? Mark.Question : Mark.None;
        int sick = sessions.Count > 0 ? sessions.Max(s => s.ContextLevel) : 0;
        if (mark != _sessionMark || sick != _sick)
        {
            _sessionMark = mark;
            _sick = sick;
            _walker.Weak = sick >= 3;
            Render();
        }
        if (sick >= 3 && _reaper == null)
        {
            _reaper = new ReaperWindow(_scale, _topmost);
            if (IsVisible) _reaper.Show();
            _lastReaperTick = _reaperClock.Elapsed;
            _reaperTimer.Start();
            RenderReaper();
        }
        else if (sick < 3 && _reaper != null)
        {
            _reaperTimer.Stop();
            _reaper.Leave();
            _reaper = null;
        }
    }

    /// <summary>The grim reaper floats right behind the pet and drifts after it when it moves.</summary>
    private void FollowWithReaper()
    {
        var now = _reaperClock.Elapsed;
        double dt = Math.Clamp((now - _lastReaperTick).TotalSeconds, 0, 0.1);
        _lastReaperTick = now;
        if (_reaper == null) return;

        if (IsVisible && !_reaper.IsVisible) _reaper.Show();
        else if (!IsVisible && _reaper.IsVisible) _reaper.Hide();
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!IsVisible || hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect)) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        double petWidth = PetImage.Width * dpi.DpiScaleX;
        double feetX = (rect.Left + rect.Right) / 2.0;
        double feetY = _petOnTop ? rect.Top + TopToFeet(dpi.DpiScaleY) : rect.Bottom;
        _reaper.Follow(feetX - _pose.Direction * petWidth * 0.95, feetY, -_pose.Direction, dt);
    }

    /// <summary>Draws the pet again, e.g. after the body color changed.</summary>
    public void Redraw() => Render();

    /// <summary>Whether the pet feels too sick for a cookie (see <see cref="PlayEmote"/>).</summary>
    public bool Unwell => _sick >= 2;

    /// <summary>
    /// /compact or /clear emptied a session's context window that made the pet sick at level
    /// <paramref name="from"/>: it recovers, unless another session keeps it as sick.
    /// </summary>
    public void PlayRecovery(int from)
    {
        // Shown from the next tick on: by then the sessions, and with them the sick level, are up to date.
        _recovery = (DateTime.Now, from);
        _animationTimer.Start();
    }

    /// <summary>Standing still for <see cref="Stretch"/>; on a wall or in the air it just carries on.</summary>
    private bool Stretching => _stretchStart is { } start && !InBed && _pose.Motion is Motion.Idle or Motion.Walk or Motion.Run
                               && (DateTime.Now - start).TotalSeconds * PetAnimator.TicksPerSecond < PetAnimator.StretchTicks;

    /// <summary>Stretches and yawns, e.g. to suggest a break.</summary>
    public void Stretch()
    {
        _stretchStart = DateTime.Now;
        _animationTimer.Start();
        Render();
    }

    private void RenderReaper() => _reaper?.Render(_animations ? _tick : 1);

    /// <summary>The user is dictating to the assistant: stand still and listen.</summary>
    public void SetListening(bool listening)
    {
        if (listening == _listening) return;
        _listening = listening;
        Render();
    }

    /// <summary>Stops walking around while e.g. the usage overlay is attached to the pet.</summary>
    public void PauseWalking(bool paused)
    {
        _paused = paused;
        if (!paused) _needsPlace = true;
    }

    /// <summary>Screen rectangle (in DIPs) of the pet sprite itself.</summary>
    public Rect PetBounds
    {
        get
        {
            var topLeft = PetImage.TranslatePoint(new Point(0, 0), this);
            return new Rect(Left + topLeft.X, Top + topLeft.Y, PetImage.Width, PetImage.Height);
        }
    }

    private void Render()
    {
        ShowThought(Mark.None);
        if (_emote is { } emote)
        {
            // Emotes run on their own clock, so they also play with animations switched off.
            long et = (long)((DateTime.Now - _emoteStart).TotalSeconds * PetAnimator.TicksPerSecond);
            if (et < PetAnimator.EmoteTicks(emote))
            {
                var emoteFrame = _refusing ? PetAnimator.RefuseFrame(et, _sick) : PetAnimator.EmoteFrame(emote, et) with { Sick = _sick };
                PetImage.Source = Sprite.Render(emoteFrame with { Outfit = _outfit, Accessory = _accessory, Snack = _snack });
                return;
            }
            _emote = null;
            if (!_animations) _animationTimer.Stop();
        }
        if (_recovery is { } stale && _sick >= stale.From) _recovery = null; // another session keeps it as sick
        if (_recovery is { } recovery && !InBed && _pose.Motion is Motion.Idle or Motion.Walk or Motion.Run)
        {
            // Like emotes, on its own clock
            long rt = (long)((DateTime.Now - recovery.Start).TotalSeconds * PetAnimator.TicksPerSecond);
            if (rt < PetAnimator.RecoveryTicks(recovery.From))
            {
                var recoveryFrame = PetAnimator.RecoveryFrame(rt, recovery.From);
                if (recoveryFrame.Sick < _sick) recoveryFrame = recoveryFrame with { Sick = _sick };
                PetImage.Source = Sprite.Render(recoveryFrame with { Outfit = _outfit, Accessory = _accessory, Snack = _snack });
                return;
            }
            _recovery = null;
            if (!_animations) _animationTimer.Stop();
        }
        if (_stretchStart is { } stretch && !InBed && _pose.Motion is Motion.Idle or Motion.Walk or Motion.Run)
        {
            // Like emotes, on its own clock; standing still meanwhile (see Walk)
            if (Stretching)
            {
                long st = (long)((DateTime.Now - stretch).TotalSeconds * PetAnimator.TicksPerSecond);
                PetImage.Source = Sprite.Render(PetAnimator.StretchFrame(st) with { Sick = _sick, Outfit = _outfit, Accessory = _accessory, Snack = _snack });
                return;
            }
            _stretchStart = null;
            if (!_animations) _animationTimer.Stop();
        }
        bool cheering = DateTime.Now < _cheerUntil;
        long t = _animations ? _tick : 1;
        var frame = PetAnimator.Frame(_mood, _working, cheering, t, _pose.Motion, _pose.Direction, _listening, _night, _sick);
        bool inBed = InBed;
        BedBackImage.Visibility = BedFrontImage.Visibility = inBed ? Visibility.Visible : Visibility.Collapsed;
        if (inBed) frame = BedFrame(t);
        // Eyes on the ghost being dragged, or on the pointer it is about to chase or chasing.
        if ((_lookAt ?? _gaze) is { } eyes) frame = frame with { Eyes = eyes };
        // Could not grab the pointer: eyes squeezed shut, wailing with tears spurting out to the sides.
        else if (_sad)
            frame = frame with
            {
                Eyes = Eyes.Closed, Mouth = Mouth.Wavy, Arms = Arms.Down, Legs = 0,
                Bob = (int)(t / 3 % 2), Shake = 0, Mark = Mark.None,
            };
        TearsImage.Source = _sad ? Sprite.RenderTears((int)(t % 3), frame.Bob) : null;
        TearsImage.Visibility = _sad ? Visibility.Visible : Visibility.Collapsed;
        if (_filesOver && !inBed) frame = frame with { Mouth = Mouth.Open, Arms = Arms.Up, Eyes = Eyes.Wide };
        if (_sessionMark != Mark.None) frame = frame with { Mark = _sessionMark };
        _squashShown = SquashLevel();
        bool puddle = _squashShown >= 100;
        SplatImage.Source = puddle ? Sprite.RenderSplat(_squashShown - 100) : null;
        SplatImage.Visibility = puddle ? Visibility.Visible : Visibility.Collapsed;
        PetImage.Opacity = puddle ? 0 : 1;
        if (_squashShown is not 0 and < 100) frame = frame with { Squash = _squashShown, Mark = Mark.None, Sweat = 0 };
        PetImage.Source = Sprite.Render(frame with { Outfit = _outfit, Accessory = _accessory, Snack = _snack });
        ShowThought(frame.Mark);
    }

    /// <summary>
    /// The thought cloud with "!" or "?" up and to the right of the head, or hidden for any other mark. Where it
    /// would run off the right of the screen it is mirrored to the left; while the pet is at the top of the window,
    /// with no room above it, it floats beside the pet.
    /// </summary>
    private void ShowThought(Mark mark)
    {
        if (mark is not (Mark.Exclaim or Mark.Question))
        {
            ThoughtImage.Visibility = Visibility.Collapsed;
            ThoughtImage.Source = null;
            return;
        }
        int x = Sprite.ThoughtX + (_petOnTop ? 2 : 0); // in the pet's grid
        double reach = (x + Sprite.ThoughtWidth - Sprite.Width / 2.0) * _scale;
        bool left = Left + Width / 2 + reach > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth;
        if (left) x = Sprite.Width - x - Sprite.ThoughtWidth;
        ThoughtImage.Source = Sprite.RenderThought(mark, left);
        ThoughtImage.Width = Sprite.ThoughtWidth * _scale;
        ThoughtImage.Height = Sprite.ThoughtHeight * _scale;
        double fromLeft = Width / 2 + (x - Sprite.Width / 2.0) * _scale;
        ThoughtImage.VerticalAlignment = _petOnTop ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        ThoughtImage.Margin = _petOnTop
            ? new Thickness(fromLeft, _petInset, 0, 0)
            : new Thickness(fromLeft, 0, 0, (Sprite.Height - Sprite.ThoughtY - Sprite.ThoughtHeight) * _scale);
        // Only a question can be clicked: it goes to the session asking
        bool question = mark == Mark.Question;
        ThoughtImage.IsHitTestVisible = question;
        ThoughtImage.Cursor = question ? Cursors.Hand : null;
        ThoughtImage.Visibility = Visibility.Visible;
    }

    private void Thought_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Right away: Windows lets the window come to the front only while the click is the last input.
        if (AskingSession() is { } session) SessionClicked?.Invoke(session);
        e.Handled = true;
    }

    // ---------------------------------------------------------------- walking around

    /// <summary>
    /// Pet at the top of the window, with the speech bubble hanging below it, or at the bottom with the
    /// bubble above it. Does not move the window.
    /// </summary>
    private void SetPetOnTop(bool onTop)
    {
        _petOnTop = onTop;
        Grid.SetRow(PetImage, onTop ? 0 : 1);
        PetImage.VerticalAlignment = onTop ? VerticalAlignment.Top : VerticalAlignment.Stretch;
        Grid.SetRow(TearsImage, onTop ? 0 : 1);
        TearsImage.VerticalAlignment = PetImage.VerticalAlignment;
        Grid.SetRow(SplatImage, onTop ? 0 : 1);
        SplatImage.VerticalAlignment = PetImage.VerticalAlignment;
        foreach (var bed in new[] { BedBackImage, BedFrontImage })
        {
            Grid.SetRow(bed, onTop ? 0 : 1);
            bed.VerticalAlignment = PetImage.VerticalAlignment;
        }
        LayoutOverlays();
    }

    /// <summary>From the window top down to the pet's feet while the pet is at the top of the window (physical pixels).</summary>
    private double TopToFeet(double dpiY) => (_petInset + PetImage.Height) * dpiY;

    /// <summary>Climbing a screen edge: the side away from the wall (-1 left, 1 right), else 0.</summary>
    private int ClimbSide => _petOnTop && _walking && _walker.Motion == Motion.Climb ? -_walker.Direction : 0; // Direction points at the wall

    /// <summary>
    /// Emote buttons right next to the pet and the speech bubble beyond them: above the pet, or below it
    /// while the pet is at the top of the window. Climbing a screen edge, both go to the side away from the
    /// wall, which would cut them off; the buttons stand upright there and the window widens to make room.
    /// </summary>
    private void LayoutOverlays()
    {
        bool onTop = _petOnTop;
        int side = _overlaySide = ClimbSide;
        if (_usualWidth > 0)
        {
            double width = side == 0
                ? _usualWidth
                : Math.Max(_usualWidth, PetImage.Width + 2 * (4 + EmoteBarArea + BubbleBesideArea));
            if (width != Width)
            {
                double center = Left + Width / 2;
                Width = width;
                Left = center - width / 2;
            }
        }
        // Climbing, the pet moves down from the window top so the upright buttons fit centered on it.
        double barHeight = EmoteBar.Children.Count * EmoteBarArea;
        _petInset = side == 0 ? 0 : Math.Max(0, (barHeight - PetImage.Height) / 2);
        foreach (var image in new[] { PetImage, TearsImage, SplatImage, BedBackImage, BedFrontImage })
            image.Margin = new Thickness(0, onTop ? _petInset : 0, 0, 0);
        bool barShown = EmoteBar.Visibility == Visibility.Visible;
        double bar = barShown && side == 0 ? EmoteBarArea : 0;
        var align = onTop ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        EmoteBar.VerticalAlignment = align;
        EmoteBar.Orientation = side == 0 ? Orientation.Horizontal : Orientation.Vertical;
        foreach (var button in EmoteBar.Children.OfType<Button>())
            button.Margin = side == 0 ? new Thickness(2, 0, 2, 0) : new Thickness(0, 2, 0, 2);
        double beside = Width / 2 + PetImage.Width / 2 + 4;
        EmoteBar.HorizontalAlignment = side switch
        {
            > 0 => HorizontalAlignment.Left, < 0 => HorizontalAlignment.Right, _ => HorizontalAlignment.Center,
        };
        EmoteBar.Margin = side switch
        {
            > 0 => new Thickness(beside, _petInset + (PetImage.Height - barHeight) / 2, 0, 0),
            < 0 => new Thickness(0, _petInset + (PetImage.Height - barHeight) / 2, beside, 0),
            _ => onTop ? new Thickness(0, PetImage.Height + 4, 0, 0) : new Thickness(0, 0, 0, 4),
        };
        Bubble.VerticalAlignment = align;
        Bubble.Orientation = side == 0 ? Orientation.Vertical : Orientation.Horizontal;
        Bubble.HorizontalAlignment = EmoteBar.HorizontalAlignment;
        double besideBar = Width / 2 + PetImage.Width / 2 + 2 + (barShown ? EmoteBarArea : 0);
        Bubble.Margin = side switch
        {
            > 0 => new Thickness(besideBar, _petInset, 0, 0),
            < 0 => new Thickness(0, _petInset, besideBar, 0),
            _ => onTop ? new Thickness(0, PetImage.Height + 2 + bar, 0, 0) : new Thickness(0, 0, 0, 2 + bar),
        };
        BubbleTailUp.Visibility = onTop && side == 0 ? Visibility.Visible : Visibility.Collapsed;
        BubbleTailDown.Visibility = onTop ? Visibility.Collapsed : Visibility.Visible;
        BubbleTailLeft.Visibility = side > 0 ? Visibility.Visible : Visibility.Collapsed;
        BubbleTailRight.Visibility = side < 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateLayout();
    }

    /// <summary>
    /// Climbing, hanging and falling from the top, or too close to the top of the screen for the speech
    /// bubble above the pet: the pet goes to the top of the window and the bubble below it. Physical pixels.
    /// </summary>
    private bool WantsPetOnTop(double feetX, double feetY, double petHeight)
    {
        if (_walking && _walker.Motion is Motion.Climb or Motion.Hang or Motion.Fall) return true;
        double above = (Bubble.Visibility == Visibility.Visible ? Bubble.ActualHeight + 2 : 0)
                       + (EmoteBar.Visibility == Visibility.Visible ? EmoteBarArea : 0);
        if (above == 0) return false;
        above *= VisualTreeHelper.GetDpi(this).DpiScaleY;
        var screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)feetX, (int)feetY - 1));
        return feetY - petHeight - above < screen.WorkingArea.Top;
    }

    /// <summary>Moves the bubble below the pet or back above it if needed, keeping the pet where it is.</summary>
    private void UpdateBubblePlacement()
    {
        // While carried the window follows the cursor; moving it here would make the pet jump away from it.
        if (_dragging) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !IsVisible || !GetWindowRect(hwnd, out var rect)) return;
        double dpiY = VisualTreeHelper.GetDpi(this).DpiScaleY;
        double petHeight = PetImage.Height * dpiY;
        double feetX = (rect.Left + rect.Right) / 2.0;
        double feetY = _petOnTop ? rect.Top + TopToFeet(dpiY) : rect.Bottom;
        bool onTop = WantsPetOnTop(feetX, feetY, petHeight);
        if (onTop == _petOnTop) return;
        SetPetOnTop(onTop);
        int y = (int)Math.Round(feetY - (onTop ? TopToFeet(dpiY) : rect.Bottom - rect.Top));
        SetWindowPos(hwnd, IntPtr.Zero, rect.Left, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private void Walk()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !IsVisible || !GetWindowRect(hwnd, out var rect)) return;
        var now = _walkClock.Elapsed;
        double dt = Math.Clamp((now - _lastTick).TotalSeconds, 0, 0.1);
        _lastTick = now;
        if (_topmost && (now - _lastTopCheck).Duration() > TimeSpan.FromSeconds(1))
        {
            KeepOnTop(hwnd);
            if (_reaper is { IsVisible: true }) KeepOnTop(new WindowInteropHelper(_reaper).Handle);
            _lastTopCheck = now;
        }
        if (_dragging || _paused) return;

        // Hold still under the mouse so clicks and double-clicks land on the pet; a fall or jump still finishes.
        bool aiming = PetImage.IsMouseOver || EmoteBar.IsMouseOver || _emote != null || Stretching
                      || _pressPoint != null || _clickTimer.IsEnabled;
        if (aiming && !_needsPlace && _walker.Motion is not (Motion.Fall or Motion.Jump or Motion.Splat))
        {
            if (_pose.Motion is Motion.Walk or Motion.Run)
            {
                _pose = (Motion.Idle, _pose.Direction);
                Render();
            }
            return;
        }

        // Everything below is in physical pixels; the window's bottom edge is where the feet are.
        var dpi = VisualTreeHelper.GetDpi(this);
        double petWidth = PetImage.Width * dpi.DpiScaleX, petHeight = PetImage.Height * dpi.DpiScaleY;
        if (_needsPlace || now - _lastScan > TimeSpan.FromMilliseconds(250))
        {
            _surfaces.Scan(petWidth, petHeight);
            _lastScan = now;
        }
        double width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
        if (_needsPlace)
        {
            // Carried in its bed, or set down again for another reason while in it: it lands in it.
            _walker.Place(rect.Left + width / 2, _petOnTop ? rect.Top + TopToFeet(dpi.DpiScaleY) : rect.Bottom,
                _carryingBed || _walker.InBed && !_pulledOut);
            _pulledOut = false; // _carryingBed lasts until it has landed, so the bed falls with it
            _needsPlace = false;
        }

        bool canWalk = !_listening && !_surfaces.IsFullscreen(_walker.X, _walker.Y);
        GetCursorPos(out var cursor);
        _walker.Step(dt, _surfaces, _mood, canWalk, petWidth, petHeight, (cursor.X, cursor.Y));
        _lastPetWidth = petWidth;
        UpdateBed(petWidth);
        if (_walker.Gaze != _gaze || _walker.Sad != _sad)
        {
            _gaze = _walker.Gaze;
            _sad = _walker.Sad;
            Render();
        }

        // Climbing, hanging and falling from the top: pet at the top of the window, so the
        // (transparent) speech bubble area does not stick out above the screen.
        bool petOnTop = WantsPetOnTop(_walker.X, _walker.Y, petHeight);
        if (petOnTop != _petOnTop) SetPetOnTop(petOnTop);
        else if (ClimbSide != _overlaySide) LayoutOverlays(); // e.g. from the wall onto the top edge
        if (GetWindowRect(hwnd, out var sized)) width = sized.Right - sized.Left; // widened or narrowed for climbing
        double feetToTop = petOnTop ? TopToFeet(dpi.DpiScaleY) : height;
        int x = (int)Math.Round(_walker.X - width / 2), y = (int)Math.Round(_walker.Y - feetToTop);
        if (x != rect.Left || y != rect.Top)
            SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

        var pose = (_walker.Motion, _walker.Direction);
        if (pose != _pose)
        {
            var previous = _pose.Motion;
            bool splatted = pose.Motion == Motion.Splat && previous != Motion.Splat;
            if (splatted) _splatStart = DateTime.Now;
            _pose = pose;
            if (splatted) Splatted?.Invoke();
            else if (pose.Motion == Motion.Jump && previous != Motion.Jump) Jumped?.Invoke();
            Render();
        }
        else if (pose.Motion == Motion.Splat && SquashLevel() != _squashShown)
        {
            Render();
        }
    }

    /// <summary>
    /// After a long fall, over <see cref="PetWalker.SplatSeconds"/>: first a puddle with splashing drops on the
    /// splat layer (<see cref="Sprite.RenderSplat"/>, returned as 100 + step), then the pet rises back up like
    /// clay, overshooting into a tall stretch before it settles (<see cref="SpriteFrame.Squash"/> levels); 0 when done.
    /// </summary>
    private int SquashLevel()
    {
        if (_pose.Motion != Motion.Splat) return 0;
        double t = (DateTime.Now - _splatStart).TotalSeconds / PetWalker.SplatSeconds;
        const double puddle = 0.62;
        if (t < puddle) return 100 + Math.Min(Sprite.SplatSteps - 1, (int)(t / puddle * Sprite.SplatSteps));
        return t switch { < 0.71 => 2, < 0.79 => 1, < 0.9 => -1, < 0.97 => 1, _ => 0 };
    }

    private void ClampToScreen()
    {
        double left = SystemParameters.VirtualScreenLeft;
        double top = SystemParameters.VirtualScreenTop;
        double right = left + SystemParameters.VirtualScreenWidth;
        double bottom = top + SystemParameters.VirtualScreenHeight;
        Left = Math.Clamp(Left, left - Width / 2, right - Width / 2);
        Top = Math.Clamp(Top, top - BubbleArea, bottom - Height);
    }

    private void Pet_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _clickTimer.Stop();
            _pressPoint = null;
            e.Handled = true;
            DoubleClicked?.Invoke();
            return;
        }
        _pressPoint = e.GetPosition(this);
        PetImage.CaptureMouse();
        e.Handled = true;
    }

    // Left drag: ghost onto an Explorer window (or move the pet if the ghost is switched off).
    // Right drag: move the pet. A plain left click opens the overlay, a right click the menu.
    private void Pet_MouseMove(object sender, MouseEventArgs e)
    {
        if (_moveOffset != null)
        {
            MoveWithCursor();
            return;
        }
        if (_rightPressPoint is { } rightStart && e.RightButton == MouseButtonState.Pressed)
        {
            if (Moved4(e.GetPosition(this) - rightStart)) StartMove();
            return;
        }
        if (_pressPoint is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
        if (!Moved4(e.GetPosition(this) - start)) return;

        _pressPoint = null;
        if (_ghostDrag) StartGhostDrag();
        else StartMove();
    }

    /// <summary>The session that asked last, while its "?" is shown.</summary>
    private SessionView? AskingSession() =>
        _sessionMark != Mark.Question || _emote != null ? null : _sessions.LastOrDefault(s => s.State == SessionStates.Question);

    private static bool Moved4(Vector delta) => Math.Abs(delta.X) >= 4 || Math.Abs(delta.Y) >= 4;

    private void Pet_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_ghost != null)
        {
            EndGhostDrag(drop: true);
            return;
        }
        if (_moveOffset != null)
        {
            EndMove();
            return;
        }
        PetImage.ReleaseMouseCapture();
        if (_pressPoint == null) return;
        _pressPoint = null;
        _clickedAt = DateTime.Now;
        _clickTimer.Start();
    }

    private void Pet_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _suppressContextMenu = false;
        _rightPressPoint = e.GetPosition(this);
    }

    private void Pet_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _rightPressPoint = null;
        if (_moveOffset != null) EndMove();
    }

    private void Pet_LostMouseCapture(object sender, MouseEventArgs e)
    {
        // e.g. Alt+Tab or a system dialog while dragging
        if (_ghost != null) EndGhostDrag(drop: false);
        else if (_moveOffset != null) EndMove();
    }

    private void Pet_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // The right button release that ends a drag must not open the menu.
        if (!_suppressContextMenu) return;
        _suppressContextMenu = false;
        e.Handled = true;
    }

    // ---------------------------------------------------------------- moving the pet

    /// <summary>Cursor position relative to the window's top left while the pet is carried (physical pixels).</summary>
    private POINT? _moveOffset;

    private void StartMove()
    {
        HideEmotes(); // first: it may move the window
        var hwnd = new WindowInteropHelper(this).Handle;
        GetCursorPos(out var cursor);
        if (!GetWindowRect(hwnd, out var rect)) return;
        _rightPressPoint = null;
        _suppressContextMenu = true;
        _clickTimer.Stop();
        _moveOffset = new POINT { X = cursor.X - rect.Left, Y = cursor.Y - rect.Top };
        // Picked up asleep, it wakes a little. Grabbed by the blanket, the bed comes along to wherever it is put
        // down; grabbed by the head, the pet comes out and walks back to bed afterwards.
        if (InBed)
        {
            _carryingBed = Mouse.GetPosition(PetImage).Y >= PetImage.ActualHeight * Sprite.BlanketTop / Sprite.Height;
            _pulledOut = !_carryingBed;
            _groggyUntil = DateTime.Now.AddSeconds(2);
        }
        _dragging = true;
        UpdateBed(_lastPetWidth);
        PetImage.CaptureMouse();
        _pose = (Motion.Carried, _pose.Direction);
        Render();
    }

    private void MoveWithCursor()
    {
        if (_moveOffset is not { } offset) return;
        GetCursorPos(out var cursor);
        SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero, cursor.X - offset.X, cursor.Y - offset.Y, 0, 0,
            SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private void EndMove()
    {
        if (_moveOffset == null) return;
        _moveOffset = null;          // before releasing capture: LostMouseCapture must not end it twice
        PetImage.ReleaseMouseCapture();
        _dragging = false;
        if (!_walking) _pose = (Motion.Idle, _pose.Direction);
        Render();
        _needsPlace = true; // let go: fall down from here
        ClampToScreen();
        UpdateBubblePlacement();
        Moved?.Invoke();
    }

    // ---------------------------------------------------------------- ghost drag

    private void StartGhostDrag()
    {
        HideEmotes();
        _rightPressPoint = null;
        _clickTimer.Stop();

        _ghost = new GhostWindow(_scale) { Left = -10000, Top = -10000 };
        _ghost.Show();
        PetImage.CaptureMouse();
        UpdateGhost();
        _ghostTimer.Start();
    }

    private void UpdateGhost()
    {
        if (_ghost == null) return;
        if (GetAsyncKeyState(VK_ESCAPE) < 0)
        {
            EndGhostDrag(drop: false);
            return;
        }

        GetCursorPos(out var cursor);
        _ghost.MoveTo(cursor.X, cursor.Y);
        _ghost.SetHighlight(ExplorerLocator.IsShellAt(cursor.X, cursor.Y, _ghost.Handle));

        // The pet watches its ghost.
        var center = PetImage.PointToScreen(new Point(PetImage.ActualWidth / 2, 0));
        var eyes = cursor.X < center.X ? Eyes.LookLeft : Eyes.LookRight;
        if (_lookAt != eyes)
        {
            _lookAt = eyes;
            Render();
        }
    }

    private void EndGhostDrag(bool drop)
    {
        if (_ghost is not { } ghost) return;
        _ghost = null;               // before releasing capture: LostMouseCapture must not end it twice
        _ghostTimer.Stop();
        PetImage.ReleaseMouseCapture();
        _lookAt = null;
        Render();

        string? folder = null;
        bool isShell = false;
        if (drop)
        {
            GetCursorPos(out var cursor);
            folder = ExplorerLocator.FolderAt(cursor.X, cursor.Y, ghost.Handle, out isShell);
        }
        ghost.SetHighlight(folder != null);
        ghost.Vanish();
        if (drop) GhostDropped?.Invoke(folder, isShell);
    }

    // ---------------------------------------------------------------- files dropped from Explorer

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        bool files = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = files ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        SetFilesOver(files);
    }

    private void Window_DragLeave(object sender, DragEventArgs e) => SetFilesOver(false);

    private void Window_Drop(object sender, DragEventArgs e)
    {
        SetFilesOver(false);
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths) FilesDropped?.Invoke(paths);
        e.Handled = true;
    }

    private void SetFilesOver(bool over)
    {
        if (over == _filesOver) return;
        _filesOver = over;
        Render();
    }

    // ---------------------------------------------------------------- emotes

    private void Pet_MouseEnter(object sender, MouseEventArgs e)
    {
        _emoteHideTimer.Stop();
        if (!_emotesEnabled || _dragging || _ghost != null || EmoteBar.Visibility == Visibility.Visible) return;
        EmoteBar.Visibility = Visibility.Visible;
        LayoutOverlays();
        UpdateBubblePlacement();
    }

    private void Emotes_MouseLeave(object sender, MouseEventArgs e)
    {
        _emoteHideTimer.Stop();
        _emoteHideTimer.Start();
    }

    private void HideEmotes()
    {
        _emoteHideTimer.Stop();
        if (EmoteBar.Visibility != Visibility.Visible) return;
        EmoteBar.Visibility = Visibility.Collapsed;
        LayoutOverlays();
        UpdateBubblePlacement();
    }

    private void Emote_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag } || !Enum.TryParse(tag, out Emote emote)) return;
        PlayEmote(emote);
        Emoted?.Invoke(emote);
    }

    /// <summary>Plays the pet's reaction to an emote; playing also makes it hop.</summary>
    public void PlayEmote(Emote emote)
    {
        _emote = emote;
        _emoteStart = DateTime.Now;
        _refusing = emote == Emote.Feed && Unwell;
        _animationTimer.Start();
        if (emote == Emote.Play && !_petOnTop)
        {
            var shift = new TranslateTransform();
            PetImage.RenderTransform = shift;
            shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -PetImage.Height * 0.4, TimeSpan.FromSeconds(0.25))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            });
        }
        Render();
    }

    private void Bubble_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => HideBubble();

    private void HideFromAltTab()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, (style | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
    }

    /// <summary>
    /// Puts the window back among the topmost ones if Windows has pushed it below normal windows. The
    /// WS_EX_TOPMOST flag stays set then, so setting WPF's Topmost again would change nothing.
    /// </summary>
    private static void KeepOnTop(IntPtr hwnd)
    {
        // All topmost windows come first in the z-order, so a normal one above means it has slipped.
        for (var above = GetWindow(hwnd, GW_HWNDPREV); above != IntPtr.Zero; above = GetWindow(above, GW_HWNDPREV))
            if ((GetWindowLong(above, GWL_EXSTYLE) & WS_EX_TOPMOST) == 0)
            {
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                return;
            }
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const uint GW_HWNDPREV = 3;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    private const int VK_ESCAPE = 0x1B;

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }

    [DllImport("user32.dll")] private static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
}

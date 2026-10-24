using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Claudius.Core;

namespace Claudius.Views;

/// <summary>
/// The banner for a reached achievement in the middle of the main screen: the pet with its reward, the title, what it
/// unlocks and a golden trophy. It pops up with a few sparkles, stays about five seconds (as long as the mouse is on
/// it) and fades out; a click opens the achievements. It never takes the keyboard focus.
/// </summary>
public sealed class AchievementPopup : Window
{
    // The trophy, by rows: y gold, o its shade, W the shine, t/T the wooden base
    private static readonly string[] Trophy =
    [
        "..yyyyyyyyyy...",
        "oo.yWyyyyyyo.oo",
        "o..yWyyyyyyo..o",
        "o..yWyyyyyyo..o",
        ".o.yyyyyyyyo.o.",
        "..oyyyyyyyyo...",
        "...oyyyyyyo....",
        ".....oyyo......",
        "......yo.......",
        "......yo.......",
        "....tttttt.....",
        "...tTTTTTTt....",
    ];

    private static readonly Dictionary<char, uint> TrophyColors = new()
    {
        ['y'] = 0xFFF2C14E, ['o'] = 0xFFC48A2C, ['W'] = 0xFFFFF4C8, ['t'] = 0xFF7A5230, ['T'] = 0xFF5A3A20,
    };

    private static readonly Duration FadeOut = TimeSpan.FromSeconds(0.4);

    private readonly DispatcherTimer _hold = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly Grid _root = new() { Margin = new Thickness(40), RenderTransformOrigin = new Point(0.5, 0.5) };
    private bool _closing;

    public AchievementPopup(string title, string reward, string count, ImageSource picture, Brush accent, Action open)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        Title = "Claudius";
        Left = -10000;
        Top = -10000;
        Cursor = Cursors.Hand;

        var color = accent is SolidColorBrush solid ? solid.Color : Color.FromRgb(0xD9, 0x77, 0x57);
        var image = new Image { Source = picture, Width = 132, Height = 96, RenderTransform = new TranslateTransform() };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        var texts = new StackPanel
        {
            Margin = new Thickness(20, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = Strings.AchievementUnlocked, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = accent },
                new TextBlock
                {
                    Text = title, FontSize = 28, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 2, 0, 4),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0xF1, 0xEA)), TextTrimming = TextTrimming.CharacterEllipsis,
                },
                new TextBlock { Text = reward, FontSize = 15, Foreground = new SolidColorBrush(Color.FromRgb(0xB8, 0xB4, 0xAC)), TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = count, FontSize = 11.5, Margin = new Thickness(0, 6, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x7C, 0x74)) },
            },
        };
        var row = new Grid
        {
            ColumnDefinitions = { new() { Width = GridLength.Auto }, new() { Width = new GridLength(1, GridUnitType.Star) }, new() { Width = GridLength.Auto } },
        };
        var plate = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1D, 0x1B)),
            Padding = new Thickness(2, 18, 2, 18),
            Child = image,
        };
        var trophy = TrophyPicture();
        Grid.SetColumn(texts, 1);
        Grid.SetColumn(trophy, 2);
        row.Children.Add(plate);
        row.Children.Add(texts);
        row.Children.Add(trophy);
        _root.Children.Add(new Border
        {
            Width = 600,
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2A, 0x27)),
            BorderBrush = accent,
            BorderThickness = new Thickness(2.5),
            Padding = new Thickness(16),
            Effect = new DropShadowEffect { Color = color, BlurRadius = 32, ShadowDepth = 0, Opacity = 0.7 },
            Child = row,
        });
        var sparkles = new Canvas { Width = 600, IsHitTestVisible = false };
        _root.Children.Add(sparkles);
        _root.RenderTransform = new ScaleTransform(0.7, 0.7);
        _root.Opacity = 0;
        Content = _root;

        MouseLeftButtonUp += (_, _) =>
        {
            open();
            FadeAway();
        };
        MouseEnter += (_, _) => _hold.Stop();
        MouseLeave += (_, _) => { if (!_closing) _hold.Start(); };
        _hold.Tick += (_, _) => FadeAway();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, (style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW);
        };
        Loaded += (_, _) =>
        {
            // Always the middle of the main screen
            var area = SystemParameters.WorkArea;
            Left = area.Left + (area.Width - ActualWidth) / 2;
            Top = area.Top + (area.Height - ActualHeight) / 2;
            AddSparkles(sparkles, color, ((FrameworkElement)_root.Children[0]).ActualHeight);
            PopUp(image, trophy);
        };
    }

    /// <summary>Fades out and closes; once is enough.</summary>
    public void FadeAway()
    {
        if (_closing) return;
        _closing = true;
        _hold.Stop();
        var fade = new DoubleAnimation(0, FadeOut);
        fade.Completed += (_, _) => Close();
        _root.BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>Springs up and settles, the sparkles flash, the pet hops, the trophy wobbles and shines; then it waits.</summary>
    private void PopUp(Image pet, Grid trophy)
    {
        _root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.2)));
        var scale = Keys((0, 0.7), (0.2, 1.06), (0.35, 1));
        var transform = (ScaleTransform)_root.RenderTransform;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        pet.RenderTransform.BeginAnimation(TranslateTransform.YProperty, Keys((0.2, 0), (0.32, -10), (0.45, 0), (0.55, -4), (0.62, 0)));
        trophy.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, Keys((0.2, 0), (0.3, -10), (0.4, 8), (0.5, -5), (0.6, 0)));
        var shine = (Rectangle)((Canvas)trophy.Children[1]).Children[0];
        shine.RenderTransform.BeginAnimation(TranslateTransform.XProperty, Keys((0.6, -24), (1.1, 100)));
        _hold.Start();
    }

    /// <summary>Little pixel stars around the banner that flash up one after another.</summary>
    private static void AddSparkles(Canvas canvas, Color accent, double height)
    {
        var gold = Color.FromRgb(0xF2, 0xC1, 0x4E);
        (double X, double Y, double Size, Color Color)[] spots =
        [
            (10, -8, 4, gold), (570, -14, 5, gold), (614, 60, 3, accent), (-18, 110, 3, accent),
            (250, height + 14, 3, gold), (480, height + 10, 4, accent), (160, -18, 3, accent),
        ];
        for (int i = 0; i < spots.Length; i++)
        {
            var (x, y, s, color) = spots[i];
            var star = new Canvas { Opacity = 0, RenderTransform = new ScaleTransform(), RenderTransformOrigin = new Point(0.5, 0.5) };
            star.Children.Add(Pixel(-s, -3 * s, 2 * s, 6 * s, color));
            star.Children.Add(Pixel(-3 * s, -s, 6 * s, 2 * s, color));
            star.Children.Add(Pixel(-s, -s, 2 * s, 2 * s, Colors.White));
            Canvas.SetLeft(star, x);
            Canvas.SetTop(star, y);
            canvas.Children.Add(star);
            double start = 0.15 + i * 0.05;
            star.BeginAnimation(OpacityProperty, Keys((start, 0), (start + 0.12, 1), (start + 0.5, 0)));
            var grow = Keys((start, 0.4), (start + 0.12, 1.2), (start + 0.5, 0.6));
            star.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            star.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        }
    }

    private static Rectangle Pixel(double x, double y, double w, double h, Color color)
    {
        var rect = new Rectangle { Width = w, Height = h, Fill = new SolidColorBrush(color) };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        return rect;
    }

    /// <summary>The pixel trophy, six times its size, with a band of shine that runs over its gold only.</summary>
    private static Grid TrophyPicture()
    {
        int w = Trophy.Max(r => r.Length), h = Trophy.Length;
        var pixels = new uint[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < Trophy[y].Length; x++)
                if (TrophyColors.TryGetValue(Trophy[y][x], out uint c)) pixels[y * w + x] = c;
        var bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
        bitmap.Freeze();

        var image = new Image { Source = bitmap, Width = w * 6, Height = h * 6 };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        var shine = new Rectangle
        {
            Width = 10, Height = h * 6 + 20, Fill = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF)),
            RenderTransform = new TranslateTransform(-24, 0),
        };
        Canvas.SetTop(shine, -10);
        var mask = new ImageBrush(bitmap) { Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(mask, BitmapScalingMode.NearestNeighbor);
        var band = new Canvas { Width = w * 6, Height = h * 6, ClipToBounds = true, OpacityMask = mask, Children = { shine } };
        return new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            RenderTransformOrigin = new Point(0.5, 1),
            RenderTransform = new RotateTransform(),
            Children = { image, band },
        };
    }

    /// <summary>A double animation through (seconds, value) points, linear between them.</summary>
    private static DoubleAnimationUsingKeyFrames Keys(params (double At, double Value)[] points)
    {
        var animation = new DoubleAnimationUsingKeyFrames();
        foreach (var (at, value) in points)
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(value, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(at))));
        return animation;
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}

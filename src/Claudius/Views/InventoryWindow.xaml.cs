using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Claudius.Core;
using Claudius.Pet;
using Forms = System.Windows.Forms;

namespace Claudius.Views;

/// <summary>
/// The pet's inventory. Model: which model and effort new sessions start with, written to the assistant's
/// settings.json. Wardrobe: what the pet wears per model. Color: the pet's color. Everything applies right away.
/// </summary>
public partial class InventoryWindow : Window
{
    /// <summary>
    /// The models on offer, with the id written as <c>model</c>. The effort is saved under the same id, so the
    /// two always match; a full id rather than an alias, since an alias would move on to a model without a saved effort.
    /// </summary>
    private static readonly (ModelFamily Family, string Name, string? Id)[] Models =
    [
        (ModelFamily.Opus, "Opus", "claude-opus-5-5"),
        (ModelFamily.Sonnet, "Sonnet", "claude-sonnet-5-5"),
        (ModelFamily.Haiku, "Haiku", "claude-haiku-5-5"),
        (ModelFamily.Fable, "Fable", "claude-fable-5-1"),
        (ModelFamily.Other, Strings.ModelDefault, null),
    ];

    private static readonly string?[] EffortLevels = [null, "low", "medium", "high", "xhigh"];

    private static readonly string[] PresetColors =
    [
        AppSettings.DefaultPetColor, "#E5484D", "#EF7FA6", "#8E6CD9", "#5B8DEF",
        "#3FB8AF", "#6BBF59", "#E8C24A", "#A0704D", "#9A9AA6",
    ];

    /// <summary>The tab shown when the inventory is opened again.</summary>
    private static int _lastTab;

    private readonly App _app;
    private readonly AppSettings _settings;
    private readonly List<Border> _tabs = [];
    private readonly Dictionary<ModelFamily, Border> _tiles = new();
    private readonly Dictionary<ModelFamily, Border> _chips = new();
    private readonly List<(Border Tile, Outfit Item, Func<Outfit, bool> IsWorn, UIElement Lock)> _items = [];
    private readonly Achievements _achievements;
    private readonly List<(Border Swatch, string Color)> _swatches = [];
    /// <summary>Every pet picture in the window, redrawn when the color or an outfit changes.</summary>
    private readonly List<(Image Image, Func<SpriteFrame> Frame)> _pictures = [];
    /// <summary>The model set now, by its family; null for a model that is not on offer (e.g. an older one).</summary>
    private ModelFamily? _selected;
    /// <summary>The model whose outfit the wardrobe shows.</summary>
    private ModelFamily _dressing;
    private bool _loading = true;

    public InventoryWindow(App app, AppSettings settings, ModelFamily running)
    {
        InitializeComponent();
        Icon = Sprite.WindowIcon();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        _app = app;
        _settings = settings;
        _achievements = app.Achievements;
        _dressing = running;
        // Reached while the inventory is open: unlock the item right away.
        Action<Achievement> unlocked = _ => Dispatcher.BeginInvoke(() => { ShowWardrobe(); ShowAchievements(); });
        _achievements.Unlocked += unlocked;
        Closed += (_, _) => _achievements.Unlocked -= unlocked;

        string[] tabNames = [Strings.TabModel, Strings.TabWardrobe, Strings.TabColor, Strings.TabAchievements];
        for (int i = 0; i < tabNames.Length; i++)
        {
            int tab = i;
            var border = new Border
            {
                Padding = new Thickness(0, 4, 0, 6),
                BorderThickness = new Thickness(0, 0, 0, 2),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = tabNames[i], HorizontalAlignment = HorizontalAlignment.Center },
            };
            border.MouseLeftButtonUp += (_, _) => ShowTab(tab);
            _tabs.Add(border);
            TabStrip.Children.Add(border);
        }

        foreach (var model in Models)
        {
            var family = model.Family;
            var tile = Tile(Picture(() => Smiling(_settings.OutfitFor(family)), 2.5), model.Name,
                model.Id == null ? Strings.ModelDefaultTip : Strings.ModelTip(model.Name), () => Pick(family, model.Id));
            Tiles.Children.Add(_tiles[family] = tile);
        }

        BuildWardrobe();
        BuildColors();

        Run(() =>
        {
            string? current = AssistantSetup.CurrentModel();
            var family = Sprite.FamilyOf(current);
            _selected = current == null ? ModelFamily.Other : family != ModelFamily.Other ? family : null;
            if (_selected == null)
            {
                OtherModelText.Text = Strings.CurrentModelOther(current!);
                OtherModelText.Visibility = Visibility.Visible;
            }
        });
        ShowSelection();
        ShowWardrobe();
        ShowWardrobeOptions();
        ShowColor();
        ShowAchievements();
        ShowTab(_lastTab);
        _loading = false;
    }

    // ---------------------------------------------------------------- tabs and shared pieces

    private void ShowTab(int tab)
    {
        _lastTab = tab;
        ModelPanel.Visibility = tab == 0 ? Visibility.Visible : Visibility.Collapsed;
        WardrobePanel.Visibility = tab == 1 ? Visibility.Visible : Visibility.Collapsed;
        ColorPanel.Visibility = tab == 2 ? Visibility.Visible : Visibility.Collapsed;
        AchievementsPanel.Visibility = tab == 3 ? Visibility.Visible : Visibility.Collapsed;
        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].BorderBrush = i == tab ? Accent : new SolidColorBrush(Color.FromRgb(0x3A, 0x39, 0x36));
            ((TextBlock)_tabs[i].Child).Foreground = i == tab ? Accent : new SolidColorBrush(Color.FromRgb(0xB5, 0xB1, 0xA8));
        }
    }

    private Brush Accent => (Brush)FindResource("AccentBrush");

    private static SpriteFrame Smiling(Outfit outfit) => new(Eyes.Normal, Mouth.Smile, Outfit: outfit);

    private Image Picture(Func<SpriteFrame> frame, double scale)
    {
        var image = new Image { Width = Sprite.Width * scale, Height = Sprite.Height * scale, Source = Sprite.Render(frame()) };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        _pictures.Add((image, frame));
        return image;
    }

    private void RedrawPictures()
    {
        foreach (var (image, frame) in _pictures) image.Source = Sprite.Render(frame());
        Icon = Sprite.WindowIcon(); // the color may have changed
    }

    /// <summary>A clickable tile with a picture and, optionally, a name below it.</summary>
    private static Border Tile(UIElement picture, string? name, string tooltip, Action click)
    {
        var content = new StackPanel { Children = { picture } };
        if (name != null)
            content.Children.Add(new TextBlock { Text = name, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
        var tile = new Border
        {
            Margin = name != null ? new Thickness(3) : new Thickness(2),
            Padding = name != null ? new Thickness(4, 8, 4, 6) : new Thickness(3),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromRgb(0x30, 0x2F, 0x2C)),
            Cursor = Cursors.Hand,
            ToolTip = tooltip,
            Child = content,
        };
        tile.MouseLeftButtonUp += (_, _) => click();
        return tile;
    }

    private void Highlight(Border tile, bool selected) =>
        tile.BorderBrush = selected ? Accent : new SolidColorBrush(Color.FromRgb(0x3A, 0x39, 0x36));

    // ---------------------------------------------------------------- model and effort

    private void Pick(ModelFamily family, string? id)
    {
        if (_selected == family) return;
        if (!Run(() => AssistantSetup.SetModel(id))) return;
        _selected = family;
        OtherModelText.Visibility = Visibility.Collapsed;
        _loading = true;
        ShowSelection();
        _loading = false;
    }

    /// <summary>Highlights the chosen model and shows its effort.</summary>
    private void ShowSelection()
    {
        foreach (var (family, tile) in _tiles) Highlight(tile, family == _selected);

        string? id = SelectedId;
        string? level = null;
        if (id != null) Run(() => level = AssistantSetup.CurrentEffort(id));
        EffortSlider.Value = Math.Max(0, Array.IndexOf(EffortLevels, level));
        EffortSlider.IsEnabled = id != null;
        EffortHint.Visibility = id != null ? Visibility.Collapsed : Visibility.Visible;
        EffortText.Text = Strings.EffortName((int)EffortSlider.Value);
    }

    private string? SelectedId => _selected is { } family ? Models.First(m => m.Family == family).Id : null;

    private void EffortSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        int level = (int)e.NewValue;
        if (EffortText != null) EffortText.Text = Strings.EffortName(level);
        if (_loading || SelectedId is not { } id) return;
        Run(() => AssistantSetup.SetEffort(id, EffortLevels[level]));
    }

    // ---------------------------------------------------------------- wardrobe

    private Outfit Dressed => _settings.OutfitFor(_dressing);

    private void BuildWardrobe()
    {
        foreach (var model in Models)
        {
            var family = model.Family;
            var chip = new Border
            {
                Margin = new Thickness(3, 0, 3, 0),
                Padding = new Thickness(0, 4, 0, 4),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(2),
                Background = new SolidColorBrush(Color.FromRgb(0x30, 0x2F, 0x2C)),
                Cursor = Cursors.Hand,
                ToolTip = family == ModelFamily.Other ? Strings.OtherModelsTip : null,
                Child = new TextBlock
                {
                    Text = family == ModelFamily.Other ? Strings.OtherModels : model.Name,
                    HorizontalAlignment = HorizontalAlignment.Center,
                },
            };
            chip.MouseLeftButtonUp += (_, _) =>
            {
                _dressing = family;
                ShowWardrobe();
            };
            FamilyChips.Children.Add(_chips[family] = chip);
        }
        WardrobePreview.Source = Sprite.Render(Smiling(Dressed));
        _pictures.Add((WardrobePreview, () => Smiling(Dressed)));

        foreach (var hat in Enum.GetValues<Hat>())
            AddItem(Sprite.IsMarioCostume(new(hat)) ? MarioItems : Sprite.IsFootballHelmet(hat) ? HelmetItems : HatItems, Strings.HatName(hat), new Outfit(hat), o => o.Hat == hat, o => o with { Hat = hat });
        foreach (var face in Enum.GetValues<FaceWear>())
            AddItem(Sprite.IsMarioCostume(new(Face: face)) ? MarioItems : FaceItems, Strings.FaceName(face), new Outfit(Face: face), o => o.Face == face, o => o with { Face = face });
        foreach (var neck in Enum.GetValues<Neckwear>())
            AddItem(Sprite.IsMarioCostume(new(Neck: neck)) ? MarioItems : Sprite.IsFanScarf(neck) ? FanScarfItems : NeckItems, Strings.NeckName(neck), new Outfit(Neck: neck), o => o.Neck == neck, o => o with { Neck = neck });
    }

    /// <summary>
    /// One item to pick for a slot, shown on the pet on its own. Until its achievement is reached it is faded,
    /// marked with a lock, and a click tells how to get it.
    /// </summary>
    private void AddItem(WrapPanel slot, string name, Outfit alone, Func<Outfit, bool> isWorn, Func<Outfit, Outfit> wear)
    {
        var lockMark = new TextBlock
        {
            Text = "🔒",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, -2, -3),
        };
        var picture = new Grid { Children = { Picture(() => Smiling(alone), 2), lockMark } };
        var tile = Tile(picture, null, name, () =>
        {
            if (_achievements.LockedBy(alone) is { } achievement)
            {
                LockText.Text = Strings.LockedItem(name, Strings.AchievementHint(achievement.Id));
                LockText.Visibility = Visibility.Visible;
                return;
            }
            LockText.Visibility = Visibility.Collapsed;
            Dress(wear(Dressed));
        });
        _items.Add((tile, alone, isWorn, lockMark));
        slot.Children.Add(tile);
    }

    private void Dress(Outfit outfit)
    {
        if (outfit == Sprite.DefaultOutfit(_dressing)) _settings.Outfits.Remove(_dressing);
        else _settings.Outfits[_dressing] = outfit;
        _app.ApplySettings(save: true);
        ShowWardrobe();
    }

    private void ResetOutfit_Click(object sender, RoutedEventArgs e) => Dress(Sprite.DefaultOutfit(_dressing));

    /// <summary>The items scroll below the fan scarves, so the window keeps a sensible height and its buttons in view.</summary>
    private void ItemRows_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double height = FanScarfItems.TranslatePoint(new Point(0, FanScarfItems.ActualHeight), ItemRows).Y;
        if (height > 0 && (double.IsNaN(ItemScroll.Height) || Math.Abs(ItemScroll.Height - height) > 0.5)) ItemScroll.Height = height;
    }

    private const string NoChoice = "–";

    private void ShowWardrobeOptions()
    {
        ModelOutfitsBox.IsChecked = _settings.ModelOutfits;
        SeasonalBox.IsChecked = _settings.SeasonalAccessories;
        BirthdayDayBox.ItemsSource = new[] { NoChoice }.Concat(Enumerable.Range(1, 31).Select(d => d.ToString(CultureInfo.InvariantCulture))).ToList();
        BirthdayMonthBox.ItemsSource = new[] { NoChoice }.Concat(Strings.Culture.DateTimeFormat.MonthNames.Take(12)).ToList();
        bool set = Occasions.TryParseDay(_settings.Birthday, out var day);
        BirthdayDayBox.SelectedIndex = set ? day.Day : 0;
        BirthdayMonthBox.SelectedIndex = set ? day.Month : 0;
    }

    private void WardrobeOption_Click(object sender, RoutedEventArgs e)
    {
        _settings.ModelOutfits = ModelOutfitsBox.IsChecked == true;
        _settings.SeasonalAccessories = SeasonalBox.IsChecked == true;
        _app.ApplySettings(save: true);
    }

    /// <summary>Saves the birthday ("MM-dd") once day and month are both set, or clears it when one is left empty.</summary>
    private void Birthday_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        int day = BirthdayDayBox.SelectedIndex, month = BirthdayMonthBox.SelectedIndex;
        string? birthday = day <= 0 || month <= 0 ? null : $"{month:00}-{day:00}";
        if (birthday != null && !Occasions.TryParseDay(birthday, out _))
        {
            ShowError(Strings.BirthdayInvalid);
            return;
        }
        ErrorText.Visibility = Visibility.Collapsed;
        if (birthday == _settings.Birthday) return;
        _settings.Birthday = birthday;
        _app.ApplySettings(save: true);
    }

    private void ShowWardrobe()
    {
        foreach (var (family, chip) in _chips) Highlight(chip, family == _dressing);
        var outfit = Dressed;
        foreach (var (tile, item, isWorn, lockMark) in _items)
        {
            Highlight(tile, isWorn(outfit));
            bool locked = _achievements.LockedBy(item) != null;
            tile.Opacity = locked ? 0.45 : 1;
            lockMark.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
        }
        RedrawPictures();
    }

    // ---------------------------------------------------------------- color

    private void BuildColors()
    {
        ColorPreview.Source = Sprite.Render(Smiling(Outfit.None));
        _pictures.Add((ColorPreview, () => Smiling(Outfit.None)));
        foreach (string color in PresetColors)
        {
            Sprite.TryParseColor(color, out uint argb);
            var swatch = new Border
            {
                Width = 30,
                Height = 30,
                Margin = new Thickness(0, 0, 8, 6),
                CornerRadius = new CornerRadius(15),
                BorderThickness = new Thickness(3),
                Background = new SolidColorBrush(ToColor(argb)),
                Cursor = Cursors.Hand,
                ToolTip = color,
            };
            swatch.MouseLeftButtonUp += (_, _) => SetColor(color);
            _swatches.Add((swatch, color));
            Swatches.Children.Add(swatch);
        }
        TerminalMascotBox.IsChecked = _settings.TerminalMascotColor;
    }

    private void ShowColor()
    {
        ColorBox.Text = _settings.PetColor;
        foreach (var (swatch, color) in _swatches)
            swatch.BorderBrush = string.Equals(color, _settings.PetColor, StringComparison.OrdinalIgnoreCase)
                ? Brushes.White : Brushes.Transparent;
    }

    private void SetColor(string text)
    {
        if (!Sprite.TryParseColor(text, out uint argb))
        {
            ShowError(Strings.ColorError);
            return;
        }
        ErrorText.Visibility = Visibility.Collapsed;
        string color = Sprite.ToHex(argb);
        if (color != _settings.PetColor)
        {
            _settings.PetColor = color;
            _app.ApplySettings(save: true);
        }
        ShowColor();
        // The accent color follows the pet's.
        ShowTab(_lastTab);
        ShowSelection();
        ShowWardrobe();
        ShowAchievements();
    }

    private void ColorBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SetColor(ColorBox.Text);
    }

    private void ColorBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (ColorBox.Text != _settings.PetColor) SetColor(ColorBox.Text);
    }

    private void PickColor_Click(object sender, RoutedEventArgs e)
    {
        if (!Sprite.TryParseColor(_settings.PetColor, out uint argb)) argb = Sprite.DefaultBodyColor;
        using var dialog = new Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(unchecked((int)argb)),
        };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
            SetColor(Sprite.ToHex(unchecked((uint)dialog.Color.ToArgb())));
    }

    private void DefaultColor_Click(object sender, RoutedEventArgs e) => SetColor(AppSettings.DefaultPetColor);

    private void TerminalMascotBox_Click(object sender, RoutedEventArgs e)
    {
        _settings.TerminalMascotColor = TerminalMascotBox.IsChecked == true;
        _app.ApplySettings(save: true);
    }

    private static Color ToColor(uint argb) => Color.FromRgb((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    // ---------------------------------------------------------------- achievements

    /// <summary>The list of achievements: reached ones with their date, open ones with how far along they are.</summary>
    private void ShowAchievements()
    {
        AchievementList.Children.Clear();
        int done = Achievements.All.Count(a => _achievements.IsUnlocked(a.Id));
        AchievementCountText.Text = Strings.AchievementCount(done, Achievements.All.Length);
        // Reached first, newest on top; then the open ones, closest to done first.
        var ordered = Achievements.All
            .OrderBy(a => _achievements.IsUnlocked(a.Id) ? 0 : 1)
            .ThenByDescending(a => _achievements.UnlockedAt(a.Id))
            .ThenByDescending(a => (double)_achievements.Progress(a) / a.Target);
        foreach (var achievement in ordered) AchievementList.Children.Add(AchievementRow(achievement));
    }

    private Border AchievementRow(Achievement achievement)
    {
        bool unlocked = _achievements.IsUnlocked(achievement.Id);
        var image = new Image
        {
            Width = Sprite.Width * 2,
            Height = Sprite.Height * 2,
            Source = Sprite.Render(Smiling(achievement.Reward)),
            Opacity = unlocked ? 1 : 0.45,
            VerticalAlignment = VerticalAlignment.Center,
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);

        var text = new StackPanel { Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = Strings.AchievementTitle(achievement.Id),
            FontWeight = FontWeights.SemiBold,
            Foreground = unlocked ? Accent : new SolidColorBrush(Color.FromRgb(0xF5, 0xF4, 0xEF)),
        });
        text.Children.Add(new TextBlock
        {
            Text = Strings.AchievementHint(achievement.Id),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0xB5, 0xB1, 0xA8)),
        });
        text.Children.Add(new TextBlock
        {
            Text = unlocked && _achievements.UnlockedAt(achievement.Id) is { } at
                ? Strings.AchievementDone(at) + " · " + Strings.RewardName(achievement)
                : Strings.AchievementReward(Strings.RewardName(achievement)),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x73, 0x6B)),
        });

        var status = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = unlocked ? 16 : 12,
            Text = unlocked ? "✓" : achievement.Target > 1 ? $"{_achievements.Progress(achievement)}/{achievement.Target}" : "🔒",
            Foreground = unlocked ? Accent : new SolidColorBrush(Color.FromRgb(0xB5, 0xB1, 0xA8)),
        };

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(text, 1);
        Grid.SetColumn(status, 2);
        row.Children.Add(image);
        row.Children.Add(text);
        row.Children.Add(status);
        return new Border
        {
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(10, 8, 12, 8),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(unlocked ? 2 : 1),
            BorderBrush = unlocked ? Accent : new SolidColorBrush(Color.FromRgb(0x3A, 0x39, 0x36)),
            Background = new SolidColorBrush(Color.FromRgb(0x30, 0x2F, 0x2C)),
            Child = row,
        };
    }

    // ---------------------------------------------------------------- errors and window

    /// <summary>Reads or writes the assistant's settings; a broken or locked file is shown instead of thrown.</summary>
    private bool Run(Action action)
    {
        try
        {
            action();
            ErrorText.Visibility = Visibility.Collapsed;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                       or JsonException or InvalidOperationException)
        {
            ShowError(Strings.AssistantSettingsNotWritten + ex.Message);
            return false;
        }
    }

    private void ShowError(string text)
    {
        ErrorText.Text = text;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Title_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // DragMove keeps the mouse until it is released, so the close button would never see its click.
        if (e.OriginalSource == CloseButton) return;
        try { DragMove(); } catch (InvalidOperationException) { }
    }

    private void Close_Click(object sender, MouseButtonEventArgs e) => Close();
}

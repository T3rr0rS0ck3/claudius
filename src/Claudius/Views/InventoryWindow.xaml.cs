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
    private readonly List<(Border Tile, Func<Outfit, bool> IsWorn)> _items = [];
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
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        _app = app;
        _settings = settings;
        _dressing = running;

        string[] tabNames = [Strings.TabModel, Strings.TabWardrobe, Strings.TabColor];
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
            AddItem(HatItems, Strings.HatName(hat), new Outfit(hat), o => o.Hat == hat, o => o with { Hat = hat });
        foreach (var face in Enum.GetValues<FaceWear>())
            AddItem(FaceItems, Strings.FaceName(face), new Outfit(Face: face), o => o.Face == face, o => o with { Face = face });
        foreach (var neck in Enum.GetValues<Neckwear>())
            AddItem(NeckItems, Strings.NeckName(neck), new Outfit(Neck: neck), o => o.Neck == neck, o => o with { Neck = neck });
    }

    /// <summary>One item to pick for a slot, shown on the pet on its own.</summary>
    private void AddItem(WrapPanel slot, string name, Outfit alone, Func<Outfit, bool> isWorn, Func<Outfit, Outfit> wear)
    {
        var tile = Tile(Picture(() => Smiling(alone), 2), null, name, () => Dress(wear(Dressed)));
        _items.Add((tile, isWorn));
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
        foreach (var (tile, isWorn) in _items) Highlight(tile, isWorn(outfit));
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

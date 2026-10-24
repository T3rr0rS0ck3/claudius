using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Claudius.Core;
using Claudius.Pet;

namespace Claudius.Views;

/// <summary>
/// The pet's inventory: one outfit per AI model picks the model new sessions start with, the slider its
/// effort. Both are written to the assistant's settings.json right away.
/// </summary>
public partial class InventoryWindow : Window
{
    /// <summary>
    /// The models on offer, with the id written as <c>model</c>. The effort is saved under the same id, so the
    /// two always match; a full id rather than an alias, since an alias would move on to a model without a saved effort.
    /// </summary>
    private static readonly (Outfit Outfit, string Name, string? Id)[] Models =
    [
        (Outfit.Crown, "Opus", "claude-opus-5-5"),
        (Outfit.Sunglasses, "Sonnet", "claude-sonnet-5-5"),
        (Outfit.Flower, "Haiku", "claude-haiku-5-5"),
        (Outfit.WizardHat, "Fable", "claude-fable-5-1"),
        (Outfit.None, Strings.ModelDefault, null),
    ];

    private static readonly string?[] EffortLevels = [null, "low", "medium", "high", "xhigh"];

    private readonly Dictionary<Outfit, Border> _tiles = new();
    /// <summary>The model set now, by its outfit; null for a model that is not on offer (e.g. an older one).</summary>
    private Outfit? _selected;
    private bool _loading = true;

    public InventoryWindow()
    {
        InitializeComponent();
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
        foreach (var model in Models) Tiles.Children.Add(_tiles[model.Outfit] = Tile(model.Outfit, model.Name, model.Id));

        Run(() =>
        {
            string? current = AssistantSetup.CurrentModel();
            var outfit = Sprite.OutfitFor(current);
            _selected = current == null ? Outfit.None : outfit != Outfit.None ? outfit : null;
            if (_selected == null)
            {
                OtherModelText.Text = Strings.CurrentModelOther(current!);
                OtherModelText.Visibility = Visibility.Visible;
            }
        });
        ShowSelection();
        _loading = false;
    }

    private Border Tile(Outfit outfit, string name, string? id)
    {
        var image = new Image
        {
            Source = Sprite.Render(new SpriteFrame(Eyes.Normal, Mouth.Smile, Outfit: outfit)),
            Width = Sprite.Width * 2.5,
            Height = Sprite.Height * 2.5,
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        var tile = new Border
        {
            Margin = new Thickness(3),
            Padding = new Thickness(4, 8, 4, 6),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromRgb(0x30, 0x2F, 0x2C)),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = id == null ? Strings.ModelDefaultTip : Strings.ModelTip(name),
            Child = new StackPanel
            {
                Children =
                {
                    image,
                    new TextBlock { Text = name, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center },
                },
            },
        };
        tile.MouseLeftButtonUp += (_, _) => Pick(outfit, id);
        return tile;
    }

    private void Pick(Outfit outfit, string? id)
    {
        if (_selected == outfit) return;
        if (!Run(() => AssistantSetup.SetModel(id))) return;
        _selected = outfit;
        OtherModelText.Visibility = Visibility.Collapsed;
        _loading = true;
        ShowSelection();
        _loading = false;
    }

    /// <summary>Highlights the chosen tile and shows that model's effort.</summary>
    private void ShowSelection()
    {
        foreach (var (outfit, tile) in _tiles)
            tile.BorderBrush = outfit == _selected ? (Brush)FindResource("AccentBrush") : new SolidColorBrush(Color.FromRgb(0x3A, 0x39, 0x36));

        string? id = SelectedId;
        string? level = null;
        if (id != null) Run(() => level = AssistantSetup.CurrentEffort(id));
        EffortSlider.Value = Math.Max(0, Array.IndexOf(EffortLevels, level));
        EffortSlider.IsEnabled = id != null;
        EffortHint.Visibility = id != null ? Visibility.Collapsed : Visibility.Visible;
        EffortText.Text = Strings.EffortName((int)EffortSlider.Value);
    }

    private string? SelectedId => _selected is { } outfit ? Models.First(m => m.Outfit == outfit).Id : null;

    private void EffortSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        int level = (int)e.NewValue;
        if (EffortText != null) EffortText.Text = Strings.EffortName(level);
        if (_loading || SelectedId is not { } id) return;
        Run(() => AssistantSetup.SetEffort(id, EffortLevels[level]));
    }

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
            ErrorText.Text = Strings.AssistantSettingsNotWritten + ex.Message;
            ErrorText.Visibility = Visibility.Visible;
            return false;
        }
    }

    private void Title_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // DragMove keeps the mouse until it is released, so the close button would never see its click.
        if (e.OriginalSource == CloseButton) return;
        try { DragMove(); } catch (InvalidOperationException) { }
    }

    private void Close_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => Close();
}

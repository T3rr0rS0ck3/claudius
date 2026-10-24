using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Claudius.Pet;

public enum Eyes { Normal, Blink, Closed, Happy, LookLeft, LookRight, LookUp, LookDown, Wide, Big }
public enum Mouth { None, Smile, Small, Wavy, Open }
public enum Arms { Down, Up, Wave, TypeLeft, TypeRight, Mic }
public enum Mark
{
    None, Dots1, Dots2, Dots3, Exclaim, Zzz1, Zzz2, Zzz3, Listen1, Listen2, Listen3, Question,
    Heart1, Heart2, Cookie, Crumbs, Ball1, Ball2, Ball3,
}
public enum Tint { Normal, Hot, Pale, Ghost }
/// <summary>What the pet wears, one item per slot: on its head, on its face and around its neck.</summary>
public readonly record struct Outfit(Hat Hat = Hat.None, FaceWear Face = FaceWear.None, Neckwear Neck = Neckwear.None)
{
    public static Outfit None => default;
}
[JsonConverter(typeof(JsonStringEnumConverter<Hat>))]
public enum Hat { None, Crown, Flower, WizardHat, TopHat, CowboyHat, ChefHat, Cap, Headphones, HardHat, VikingHelmet, PropellerCap, Halo }
[JsonConverter(typeof(JsonStringEnumConverter<FaceWear>))]
public enum FaceWear { None, Sunglasses, NerdGlasses, Monocle, Moustache, EyePatch }
[JsonConverter(typeof(JsonStringEnumConverter<Neckwear>))]
public enum Neckwear { None, BowTie, Scarf, Bell }
/// <summary>The AI model families the pet dresses up for; <see cref="Other"/> for unknown models and none.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ModelFamily>))]
public enum ModelFamily { Opus, Sonnet, Haiku, Fable, Other }
/// <summary>Headwear for the season or the night; it replaces the outfit's headwear (not its sunglasses).</summary>
public enum Accessory { None, WitchHat, SantaHat, PartyHat, BunnyEars, Nightcap }
/// <summary>The bed's parts: behind the pet (head and foot ends, pillow) and in front of it (blanket), or all.</summary>
public enum BedLayer { Back, Front, Whole }

/// <summary>Everything that describes one rendered frame of the pet.</summary>
public readonly record struct SpriteFrame(
    Eyes Eyes,
    Mouth Mouth = Mouth.None,
    Arms Arms = Arms.Down,
    int Bob = 0,
    int Shake = 0,
    int Legs = 0,
    Mark Mark = Mark.None,
    int Sweat = 0,
    bool Blush = false,
    Tint Tint = Tint.Normal,
    Outfit Outfit = default,
    Accessory Accessory = Accessory.None,
    int Squash = 0,
    int Sick = 0);

/// <summary>
/// Procedural pixel-art renderer for the pet: a chunky orange block
/// with two dark eyes, little side arms and four stubby legs.
/// </summary>
public static class Sprite
{
    public const int Width = 22;
    public const int Height = 16;

    private const uint EyeColor = 0xFF1F1A17;
    private const uint White = 0xFFFFFFFF;
    private const uint SweatColor = 0xFF67B7F0;
    private const uint BlushColor = 0xFFF2A28C;
    private const uint MarkColor = 0xFF8C90B8;
    private const uint AlertColor = 0xFFE5484D;
    private const uint QuestionColor = 0xFFE5C14B;
    private const uint HeartColor = 0xFFEF5D7A;
    private const uint CookieColor = 0xFFC98D4F;
    private const uint ChipColor = 0xFF5B3A22;
    private const uint BallColor = 0xFF5B9BE0;
    private const uint MicColor = 0xFFC9CAD4;
    private const uint HandleColor = 0xFF3A3A42;
    private const uint GoldColor = 0xFFF2C14E;
    private const uint GoldShade = 0xFFC48A2C;
    private const uint JewelColor = 0xFFE5484D;
    private const uint LensColor = 0xFF22232B;
    private const uint GlintColor = 0xFF8FA6CC;
    private const uint PetalColor = 0xFFFFFFFF;
    private const uint LeafColor = 0xFF5FAE4E;
    private const uint HatColor = 0xFF7B5CC4;
    private const uint HatShade = 0xFF5A3F9A;
    private const uint WitchColor = 0xFF3B2A4F;
    private const uint BandOrange = 0xFFE8862E;
    private const uint StickColor = 0xFF7A5230;
    private const uint StrawColor = 0xFFD9B45A;
    private const uint BindingColor = 0xFF8C3A2E;
    private const uint SantaRed = 0xFFD8343C;
    private const uint FurWhite = 0xFFF4F1EA;
    private const uint EarPink = 0xFFF2A2B4;
    private const uint NightcapBlue = 0xFF4F6FC4;
    private const uint NightcapShade = 0xFF3A55A0;
    private const uint BedWood = 0xFF9A6644;
    private const uint BedWoodShade = 0xFF6E4630;
    private const uint BlanketColor = 0xFF6C7FD6;
    private const uint BlanketShade = 0xFF5162B8;
    private const uint SickColor = 0xFF96AF6E;
    private const uint SickShade = 0xFF6E8550;
    private const uint RobeColor = 0xFF34303C;
    private const uint RobeShade = 0xFF221F28;
    private const uint HoodDark = 0xFF0E0C10;
    private const uint SkullGlow = 0xFFE8F0FF;
    private const uint BladeColor = 0xFFD6DAE2;
    private const uint BladeShade = 0xFF9AA0AC;
    private const uint ScytheHandle = 0xFF6B4A2E;
    private const uint BoneColor = 0xFFE6E0D0;
    private const uint SmokeColor = 0xFFC4BCD4;
    private const uint SmokeShade = 0xFF8F87A3;
    private const uint FrameColor = 0xFF24222A;
    private const uint MoustacheColor = 0xFF4A3426;
    private const uint ScarfColor = 0xFF3F8F5A;
    private const uint ScarfShade = 0xFF2E6B43;
    private const uint TopHatColor = 0xFF2E2B35;
    private const uint TopHatShine = 0xFF555062;
    private const uint CowboyColor = 0xFF9A6A3E;
    private const uint CowboyShade = 0xFF6E4A2A;
    private const uint ChefShade = 0xFFD6D2C8;
    private const uint CapColor = 0xFF3D6FD0;
    private const uint CapShade = 0xFF2C539F;
    private const uint HelmetColor = 0xFFA6ABB8;
    private const uint HelmetShade = 0xFF737887;

    /// <summary>Warm orange, the default body color.</summary>
    public const uint DefaultBodyColor = 0xFFD97757;

    private static readonly Dictionary<SpriteFrame, BitmapSource> Cache = new();
    private static uint _bodyColor;
    private static (uint Body, uint Shade) _normal, _hot, _pale;

    static Sprite() => SetBodyColor(DefaultBodyColor);

    /// <summary>Incremented whenever the body color changes, so other caches (tray icons) can refresh.</summary>
    public static int Version { get; private set; }

    /// <summary>Sets the pet's body color (ARGB); shade and the hot/pale mood tints are derived from it.</summary>
    public static void SetBodyColor(uint argb)
    {
        argb |= 0xFF000000;
        if (argb == _bodyColor) return;
        _bodyColor = argb;
        var (h, s, l) = ToHsl(argb);
        // Offsets measured from the original hand-picked orange palette.
        (uint, uint) Pair(double hue, double sat, double light) =>
            (FromHsl(hue, sat, light), FromHsl(hue, sat * 0.76, light - 0.114));
        _normal = Pair(h, s, l);
        _hot = Pair(h - 3, Math.Min(1, s + 0.14), l - 0.025);
        _pale = Pair(h + 2, s * 0.67, Math.Min(0.9, l + 0.037));
        Cache.Clear();
        SplatCache.Clear(); // the puddle has the body's color
        Version++;
    }

    /// <summary>The family of an AI model, by name ("Opus 4.1") or id ("…-sonnet-4-5"); unknown models and none are Other.</summary>
    public static ModelFamily FamilyOf(string? model) =>
        model == null ? ModelFamily.Other
        : model.Contains("opus", StringComparison.OrdinalIgnoreCase) ? ModelFamily.Opus
        : model.Contains("sonnet", StringComparison.OrdinalIgnoreCase) ? ModelFamily.Sonnet
        : model.Contains("haiku", StringComparison.OrdinalIgnoreCase) ? ModelFamily.Haiku
        : model.Contains("fable", StringComparison.OrdinalIgnoreCase) ? ModelFamily.Fable
        : ModelFamily.Other;

    /// <summary>
    /// What a model wears until it is dressed differently in the inventory: Opus a crown, Sonnet sunglasses,
    /// Haiku a flower, Fable a wizard's hat, other models nothing.
    /// </summary>
    public static Outfit DefaultOutfit(ModelFamily family) => family switch
    {
        ModelFamily.Opus => new(Hat.Crown),
        ModelFamily.Sonnet => new(Face: FaceWear.Sunglasses),
        ModelFamily.Haiku => new(Hat.Flower),
        ModelFamily.Fable => new(Hat.WizardHat),
        _ => Outfit.None,
    };

    public static BitmapSource Render(SpriteFrame frame)
    {
        if (Cache.TryGetValue(frame, out var cached)) return cached;
        var bitmap = new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, Width, Height), Pixels(frame), Width * 4, 0);
        bitmap.Freeze();
        Cache[frame] = bitmap;
        return bitmap;
    }

    /// <summary>BGRA pixels, row-major, Width x Height.</summary>
    public static uint[] Pixels(SpriteFrame f)
    {
        var px = new uint[Width * Height];
        void Set(int x, int y, uint c)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height) px[y * Width + x] = c;
        }

        (uint body, uint shade) = f.Tint switch
        {
            Tint.Hot => _hot,
            Tint.Pale => _pale,
            Tint.Ghost => (0xFFE4ECF8u, 0xFFB3C2DBu),
            _ => _normal,
        };
        // A full context window: the pet turns greener and greener
        if (f.Sick > 0 && f.Tint != Tint.Ghost)
        {
            double k = f.Sick switch { 1 => 0.25, 2 => 0.5, _ => 0.7 };
            (body, shade) = (Blend(body, SickColor, k), Blend(shade, SickShade, k));
        }

        int bx = 2 + f.Shake;   // left edge incl. arms
        int by = 5 + f.Bob;     // top edge of the body
        int c0 = bx + 2;        // left edge of the body (14 px wide, 9 px tall)
        int Mirror(int x) => 2 * bx + 17 - x;

        if (f.Squash != 0)
        {
            // Squashed after a long fall, like clay: the higher Squash (1..4), the wider and flatter, feet on the
            // ground; -1 is the overshoot, stretched up narrow and tall, before it settles back into shape.
            var (w, h, legs) = f.Squash switch { 4 => (20, 4, 0), 3 => (18, 5, 0), 2 => (16, 7, 1), 1 => (15, 8, 1), _ => (12, 10, 2) };
            int left = 11 + f.Shake - w / 2, right = left + w - 1, bottom = Height - 1 - legs, top = bottom - h + 1;
            for (int y = top; y <= bottom; y++)
            {
                int inset = y == top ? 1 : 0; // rounded shoulders
                for (int x = left + inset; x <= right - inset; x++) Set(x, y, y == bottom ? shade : body);
            }
            foreach (int lx in (int[])[left + 1, left + 3, right - 3, right - 1])
                for (int y = bottom + 1; y <= Height - 1; y++) Set(lx, y, shade);

            int eyeY = top + Math.Max(1, h / 3), eyeL = left + w * 3 / 14, eyeR = right - w * 3 / 14;
            if (f.Squash >= 3)
            {
                // Eyes squeezed into lines
                Set(eyeL - 1, eyeY, EyeColor); Set(eyeL, eyeY, EyeColor);
                Set(eyeR, eyeY, EyeColor); Set(eyeR + 1, eyeY, EyeColor);
            }
            else if (f.Squash >= 1)
            {
                for (int i = -1; i <= 1; i++) { Set(eyeL + i, eyeY, EyeColor); Set(eyeR + i, eyeY, EyeColor); }
            }
            else
            {
                Set(eyeL, eyeY, EyeColor); Set(eyeL, eyeY + 1, EyeColor);
                Set(eyeR, eyeY, EyeColor); Set(eyeR, eyeY + 1, EyeColor);
            }
            if (h >= 6)
            {
                int my = bottom - 2, mx = (left + right) / 2;
                Set(mx - 1, my, EyeColor); Set(mx, my + 1, EyeColor); Set(mx + 1, my, EyeColor);
            }
            // Headwear rides on the squashed head.
            c0 = left + (w - 14) / 2;
            by = top;
        }
        else
        {
        // Body
        for (int y = by; y <= by + 8; y++)
            for (int x = c0; x <= c0 + 13; x++)
                Set(x, y, y == by + 8 ? shade : body);

        // Legs (1/3 left, 10/12 right); Legs = 1 or 2 lifts alternating pairs
        int[] legCols = [1, 3, 10, 12];
        for (int i = 0; i < legCols.Length; i++)
        {
            bool lifted = f.Legs != 0 && (i % 2 == 0) == (f.Legs == 1);
            for (int y = by + 9; y <= Height - 1 - (lifted ? 1 : 0); y++)
                Set(c0 + legCols[i], y, shade);
        }

        // Arms
        void ArmDown(bool left, int low)
        {
            for (int y = by + 4 + low; y <= by + 5 + low; y++)
                for (int x = bx; x <= bx + 1; x++)
                    Set(left ? x : Mirror(x), y, body);
        }
        void ArmUp(bool left)
        {
            (int x, int y)[] pts = [(bx + 1, by + 3), (bx + 1, by + 2), (bx, by + 2), (bx, by + 1)];
            foreach (var (x, y) in pts) Set(left ? x : Mirror(x), y, body);
        }
        switch (f.Arms)
        {
            case Arms.Up: ArmUp(true); ArmUp(false); break;
            case Arms.Wave: ArmUp(true); ArmDown(false, 0); break;
            case Arms.TypeLeft: ArmDown(true, 1); ArmDown(false, 0); break;
            case Arms.TypeRight: ArmDown(true, 0); ArmDown(false, 1); break;
            default: ArmDown(true, 0); ArmDown(false, 0); break;
        }

        // Eyes: o = direction towards the outside of the face
        void Eye(int ex, int o)
        {
            switch (f.Eyes)
            {
                case Eyes.Blink:
                    Set(ex, by + 3, EyeColor); Set(ex + o, by + 3, EyeColor); break;
                case Eyes.Closed:
                    Set(ex - o, by + 3, EyeColor); Set(ex, by + 3, EyeColor); Set(ex + o, by + 3, EyeColor); break;
                case Eyes.Happy:
                    Set(ex + o, by + 3, EyeColor); Set(ex, by + 2, EyeColor); Set(ex - o, by + 3, EyeColor); break;
                case Eyes.LookLeft:
                    Set(ex - 1, by + 2, EyeColor); Set(ex - 1, by + 3, EyeColor); break;
                case Eyes.LookRight:
                    Set(ex + 1, by + 2, EyeColor); Set(ex + 1, by + 3, EyeColor); break;
                case Eyes.LookUp:
                    Set(ex, by + 1, EyeColor); Set(ex, by + 2, EyeColor); break;
                case Eyes.LookDown:
                    Set(ex, by + 3, EyeColor); Set(ex, by + 4, EyeColor); break;
                case Eyes.Wide:
                    for (int y = by + 2; y <= by + 3; y++) { Set(ex, y, EyeColor); Set(ex - o, y, EyeColor); }
                    Set(ex, by + 2, White);
                    break;
                case Eyes.Big:
                    for (int y = by + 1; y <= by + 3; y++) { Set(ex, y, EyeColor); Set(ex - o, y, EyeColor); }
                    Set(ex, by + 1, White);
                    break;
                default:
                    Set(ex, by + 2, EyeColor); Set(ex, by + 3, EyeColor); break;
            }
        }
        // Behind sunglasses the eyes stay hidden, even where looking up or down would peek past the lenses
        if (f.Outfit.Face != FaceWear.Sunglasses)
        {
            Eye(c0 + 3, -1);
            Eye(c0 + 10, 1);
        }

        // Mouth
        switch (f.Mouth)
        {
            case Mouth.Smile:
                Set(c0 + 5, by + 5, EyeColor); Set(c0 + 6, by + 6, EyeColor);
                Set(c0 + 7, by + 6, EyeColor); Set(c0 + 8, by + 5, EyeColor);
                break;
            case Mouth.Small:
                Set(c0 + 6, by + 6, EyeColor); Set(c0 + 7, by + 6, EyeColor);
                break;
            case Mouth.Wavy:
                Set(c0 + 5, by + 6, EyeColor); Set(c0 + 6, by + 5, EyeColor);
                Set(c0 + 7, by + 6, EyeColor); Set(c0 + 8, by + 5, EyeColor);
                break;
            case Mouth.Open:
                for (int y = by + 5; y <= by + 6; y++) { Set(c0 + 6, y, EyeColor); Set(c0 + 7, y, EyeColor); }
                break;
        }

        // Microphone held upright in the right hand, next to the face: a silver head with a dark grille
        if (f.Arms == Arms.Mic)
        {
            Glyph(c0 + 15, MicColor, by + 1, "XXX", "XXX");
            Glyph(c0 + 15, HandleColor, by + 1, ".X.", "X.X");
            for (int y = by + 3; y <= by + 6; y++) Set(c0 + 16, y, HandleColor);
        }

        if (f.Blush)
        {
            Set(c0 + 1, by + 4, BlushColor); Set(c0 + 2, by + 4, BlushColor);
            Set(c0 + 11, by + 4, BlushColor); Set(c0 + 12, by + 4, BlushColor);
        }

        }

        // Outfit, one item per slot. Face and neck need the unsquashed body; a seasonal or night accessory takes the
        // head. Headwear sits on the left half of the head, out of the way of the marks above its middle and right.
        if (f.Squash == 0)
        {
            switch (f.Outfit.Face)
            {
                // Dark sunglasses over both eyes, with a glint on each lens (the eyes are left out above)
                case FaceWear.Sunglasses:
                    Glyph(c0, LensColor, by + 1, "XXXXXXXXXXXXXX", ".XXXXX..XXXXX.", "..XXX....XXX..");
                    Set(c0 + 2, by + 2, GlintColor);
                    Set(c0 + 9, by + 2, GlintColor);
                    break;
                // Square black frames around the eyes, joined by a bridge
                case FaceWear.NerdGlasses:
                    Glyph(c0 + 1, FrameColor, by + 1, "XXXXX..XXXXX", "X...XXXX...X", "X...X..X...X", "XXXXX..XXXXX");
                    break;
                // A golden ring around the right eye, its chain hanging down the side
                case FaceWear.Monocle:
                    Glyph(c0 + 8, GoldColor, by, ".XXX.", "X...X", "X...X", "X...X", ".XXX.");
                    Glyph(c0 + 12, GoldShade, by + 5, "X.", ".X", ".X");
                    break;
                // A curled moustache between the eyes and the mouth
                case FaceWear.Moustache:
                    Glyph(c0 + 3, MoustacheColor, by + 4, ".XXXXXX.", "X......X");
                    break;
                // A black patch over the left eye, its strap running across the face
                case FaceWear.EyePatch:
                    Glyph(c0, FrameColor, by, "......X", ".....X.", "XXXXX..", "..XXX..", "..XXX..", "...X...");
                    break;
            }
            switch (f.Outfit.Neck)
            {
                // A red bow tie with a dark knot below the mouth
                case Neckwear.BowTie:
                    Glyph(c0 + 4, SantaRed, by + 7, "XX..XX", "XX..XX");
                    Glyph(c0 + 6, BindingColor, by + 7, "XX", "XX");
                    break;
                // A striped green scarf, one end hanging down between the legs
                case Neckwear.Scarf:
                    Glyph(c0, ScarfColor, by + 7, "XXXXXXXXXXXXXX", "XXXXXXXXXXXXXX");
                    Glyph(c0, FurWhite, by + 7, "..X...X...X...", "...X...X...X..");
                    Glyph(c0 + 7, ScarfColor, by + 9, "XX", "XX");
                    Glyph(c0 + 7, ScarfShade, by + 10, "XX");
                    break;
                // A red collar with a little golden bell
                case Neckwear.Bell:
                    Glyph(c0, SantaRed, by + 7, "XXXXXXXXXXXXXX");
                    Glyph(c0 + 6, GoldColor, by + 8, "XX", "XX");
                    Glyph(c0 + 6, GoldShade, by + 9, "XX");
                    break;
            }
        }
        switch (f.Accessory != Accessory.None ? Hat.None : f.Outfit.Hat)
        {
            // A golden crown with a red jewel, sitting on the head
            case Hat.Crown:
                Glyph(c0, GoldColor, by - 3, "X.X.X", "XXXXX", "XXXXX");
                Glyph(c0, GoldShade, by - 3, ".....", ".....", "XXXXX");
                Set(c0 + 2, by - 2, JewelColor);
                break;
            // A little daisy tucked onto the head
            case Hat.Flower:
                Glyph(c0 + 1, PetalColor, by - 3, ".X.", "X.X", ".X.");
                Set(c0 + 2, by - 2, GoldColor);
                Set(c0 + 3, by - 1, LeafColor);
                break;
            // A pointed wizard's hat with a little star, its tip bent to the side
            case Hat.WizardHat:
                Glyph(c0 - 1, HatColor, by - 5, "....X.", "...XX.", "..XXX.", ".XXXX.");
                Glyph(c0 - 1, HatShade, by - 1, "XXXXXX");
                Set(c0 + 2, by - 3, GoldColor);
                break;
            // A tall black top hat with a red band
            case Hat.TopHat:
                Glyph(c0, TopHatColor, by - 5, "XXXXX", "XXXXX", "XXXXX");
                Glyph(c0, TopHatShine, by - 5, ".X...", ".X...", ".X...");
                Glyph(c0, SantaRed, by - 2, "XXXXX");
                Glyph(c0 - 1, TopHatColor, by - 1, "XXXXXXX");
                break;
            // A brown cowboy hat with a dented crown and a brim curling up at both ends
            case Hat.CowboyHat:
                Glyph(c0, CowboyColor, by - 4, "XX.XX", "XXXXX");
                Glyph(c0 - 2, CowboyColor, by - 2, "X......X", "XXXXXXXX");
                Glyph(c0, CowboyShade, by - 2, "XXXXX");
                break;
            // A puffy white chef's hat
            case Hat.ChefHat:
                Glyph(c0 - 1, FurWhite, by - 5, ".XX.XX.", "XXXXXXX", "XXXXXXX", ".XXXXX.");
                Glyph(c0 - 1, ChefShade, by - 3, "..X.X..", ".......", ".......", ".XXXXX.");
                break;
            // A blue cap with a white button, its peak pointing to the left
            case Hat.Cap:
                Glyph(c0, CapColor, by - 3, ".XXX.", "XXXXX", "XXXXX");
                Glyph(c0 - 3, CapShade, by - 1, "XXX");
                Set(c0 + 2, by - 4, FurWhite);
                break;
            // Headphones: a band over the head and a red cup on each side
            case Hat.Headphones:
                Glyph(c0, HandleColor, by - 1, ".XXXXXXXXXXXX.", "X............X");
                Glyph(c0 - 1, AlertColor, by + 1, "XX", "XX", "XX");
                Glyph(c0 + 13, AlertColor, by + 1, "XX", "XX", "XX");
                break;
            // A yellow hard hat with a ridge down the middle and a brim all around
            case Hat.HardHat:
                Glyph(c0 - 1, GoldColor, by - 4, "..XXX..", ".XXXXX.", ".XXXXX.");
                Glyph(c0 - 1, GoldShade, by - 4, "...X...", "...X...", "...X...", "XXXXXXX");
                break;
            // A grey viking helmet with two white horns curving up
            case Hat.VikingHelmet:
                Glyph(c0, HelmetColor, by - 3, ".XXX.", "XXXXX", "XXXXX");
                Glyph(c0, HelmetShade, by - 1, "XXXXX");
                Glyph(c0 - 2, BoneColor, by - 4, "X.......X", "X.......X", ".X.....X.");
                break;
            // A colorful beanie with a little propeller on top
            case Hat.PropellerCap:
                Glyph(c0, SantaRed, by - 2, ".X...", "XX...");
                Glyph(c0, GoldColor, by - 2, "..X..", "..X..");
                Glyph(c0, BallColor, by - 2, "...X.", "...XX");
                Set(c0 + 2, by - 3, HandleColor);
                Glyph(c0, LeafColor, by - 4, "XX...");
                Glyph(c0, BallColor, by - 4, "...XX");
                break;
            // A golden halo floating above the head
            case Hat.Halo:
                Glyph(c0, GoldColor, by - 4, ".XXXX.", "X....X", ".XXXX.");
                break;
        }

        // Accessory, in the same place as the outfit's headwear
        switch (f.Accessory)
        {
            // Halloween: a crooked witch's hat with an orange band, and a broom in the right hand
            // (put away while the hand holds the microphone or the pet is squashed flat)
            case Accessory.WitchHat:
                Glyph(c0 - 2, WitchColor, by - 5, "....X...", "...XX...", "..XXX...", "..XXXX..", "XXXXXXXX");
                Glyph(c0 - 2, BandOrange, by - 2, "..XXXX..");
                if (f.Arms != Arms.Mic && f.Squash == 0)
                {
                    for (int y = by + 1; y <= by + 6; y++) Set(c0 + 16, y, StickColor);
                    Glyph(c0 + 15, BindingColor, by + 7, "XXX");
                    Glyph(c0 + 15, StrawColor, by + 8, "XXX", "XXX", "X.X");
                }
                break;
            // Christmas: a red cap with a white brim, its tip and bobble flopping to the left
            case Accessory.SantaHat:
                Glyph(c0 - 2, SantaRed, by - 4, ".XX....", ".XXXX..", ".XXXXX.");
                Glyph(c0 - 2, FurWhite, by - 4, "X......", ".......", ".......", "XXXXXXX");
                break;
            // New Year's Eve and birthdays: a striped party cone leaning to the left, a golden pompom on its tip
            case Accessory.PartyHat:
                Glyph(c0 - 2, GoldColor, by - 5, "X");
                Glyph(c0 - 2, HeartColor, by - 4, ".X.....", "..X.X..", "..X.X.X", "....X.X");
                Glyph(c0 - 2, BallColor, by - 4, "..X....", ".X.X...", "...X.X.", "...X.X.");
                break;
            // Easter: two bunny ears with pink insides
            case Accessory.BunnyEars:
                Glyph(c0, FurWhite, by - 5, "XX.XX", "X..X.", "X..X.", "X..X.", "XX.XX");
                Glyph(c0, EarPink, by - 4, ".X..X", ".X..X", ".X..X");
                break;
            // Night: a long blue nightcap, its tip hanging down beside the head with a white bobble
            case Accessory.Nightcap:
                Glyph(c0 - 2, NightcapBlue, by - 4, "..XXX..", ".XXXXX.", "X.XXXXX");
                Glyph(c0 - 2, NightcapShade, by - 1, "X.XXXXX"); // the gap keeps the hanging tip apart
                Set(c0 - 2, by, FurWhite);
                break;
        }

        // Sweat drops next to the head
        if (f.Sweat >= 1) { Set(c0 + 14, by - 2, SweatColor); Set(c0 + 14, by - 1, SweatColor); }
        if (f.Sweat >= 2) { Set(c0 - 1, by - 2, SweatColor); Set(c0 - 1, by - 1, SweatColor); }

        // Marks above the head
        void Z(int x, int y)
        {
            for (int i = 0; i < 5; i++) { Set(x + i, y, MarkColor); Set(x + i, y + 4, MarkColor); }
            Set(x + 3, y + 1, MarkColor);
            Set(x + 2, y + 2, MarkColor);
            Set(x + 1, y + 3, MarkColor);
        }
        switch (f.Mark)
        {
            case Mark.Dots1 or Mark.Dots2 or Mark.Dots3:
                int count = f.Mark - Mark.Dots1 + 1;
                (int x, int y)[] dots = [(c0 + 10, 3), (c0 + 12, 2), (c0 + 14, 1)];
                for (int i = 0; i < count; i++) Set(dots[i].x, dots[i].y, MarkColor);
                break;
            case Mark.Exclaim:
                for (int y = 0; y <= 1; y++) { Set(c0 + 6, y, AlertColor); Set(c0 + 7, y, AlertColor); }
                Set(c0 + 6, 3, AlertColor); Set(c0 + 7, 3, AlertColor);
                break;
            // One Z drifting away from the head, then a short pause
            case Mark.Zzz1: Z(c0 + 11, 0); break;
            case Mark.Zzz2: Z(c0 + 13, 0); break;
            case Mark.Zzz3: break;
            // The assistant sessions: a question is waiting
            case Mark.Question:
                Glyph(c0 + 5, QuestionColor, 0, "XXX", "..X", ".XX", "...", ".X.");
                break;
            // Emotes: hearts drifting up, a cookie on its way into the mouth, crumbs on the chin, a juggled ball
            case Mark.Heart1: Glyph(c0 + 9, HeartColor, 1, "XX.XX", "XXXXX", ".XXX.", "..X.."); break;
            case Mark.Heart2: Glyph(c0 + 11, HeartColor, 0, "XX.XX", "XXXXX", ".XXX.", "..X.."); break;
            case Mark.Cookie:
                Glyph(c0 + 5, CookieColor, 0, ".XXX.", "XXXXX", "XXXXX", ".XXX.");
                Glyph(c0 + 5, ChipColor, 0, ".....", ".X...", "...X.", ".....");
                break;
            case Mark.Crumbs:
                Set(c0 + 5, by + 7, CookieColor); Set(c0 + 9, by + 7, CookieColor); Set(c0 + 7, by + 8, CookieColor);
                break;
            case Mark.Ball1: Glyph(c0 + 2, BallColor, 1, ".XX.", "XXXX", "XXXX", ".XX."); break;
            case Mark.Ball2: Glyph(c0 + 5, BallColor, 0, ".XX.", "XXXX", "XXXX", ".XX."); break;
            case Mark.Ball3: Glyph(c0 + 8, BallColor, 1, ".XX.", "XXXX", "XXXX", ".XX."); break;
            // Sound waves next to the head, growing outwards: the pet is listening
            case Mark.Listen1 or Mark.Listen2 or Mark.Listen3:
                int waves = f.Mark - Mark.Listen1 + 1;
                Arc(c0 + 11, 1, 3);
                if (waves >= 2) Arc(c0 + 13, 0, 5);
                if (waves >= 3) Arc(c0 + 16, 0, 5);
                break;
        }

        // Rows of a small pixel glyph above the head, starting at row top, X = pixel
        void Glyph(int x, uint color, int top, params string[] rows)
        {
            for (int y = 0; y < rows.Length; y++)
                for (int i = 0; i < rows[y].Length; i++)
                    if (rows[y][i] == 'X') Set(x + i, top + y, color);
        }

        // ")" shaped arc, h pixels tall
        void Arc(int x, int top, int h)
        {
            Set(x, top, MarkColor);
            for (int y = top + 1; y < top + h - 1; y++) Set(x + 1, y, MarkColor);
            Set(x, top + h - 1, MarkColor);
        }

        return px;
    }

    private static readonly Dictionary<(int Phase, bool Left, int Poof), BitmapSource> ReaperCache = new();

    /// <summary>Steps of the smoke cloud the grim reaper vanishes and reappears in (<see cref="RenderReaper"/>).</summary>
    public const int PoofSteps = 3;

    /// <summary>
    /// The grim reaper that turns up next to the pet when a session's context window is nearly full: a dark
    /// hooded robe floating above the ground, two glowing eyes in the hood and a scythe, in the pet's 22x16 grid.
    /// It looks right, the scythe held out ahead; <paramref name="left"/> mirrors it to look left.
    /// <paramref name="phase"/> 0..3 bobs it up and down and flutters the hem. <paramref name="poof"/> 1..
    /// <see cref="PoofSteps"/> wraps it in a growing cloud of smoke, at the last step nothing but smoke is left.
    /// </summary>
    public static BitmapSource RenderReaper(int phase, bool left, int poof = 0) =>
        ReaperCache.TryGetValue((phase, left, poof), out var cached)
            ? cached
            : ReaperCache[(phase, left, poof)] = ToBitmap(ReaperPixels(phase, left, poof));

    /// <summary>BGRA pixels of <see cref="RenderReaper"/>, row-major, Width x Height.</summary>
    public static uint[] ReaperPixels(int phase, bool left, int poof = 0)
    {
        var px = new uint[Width * Height];
        if (poof >= PoofSteps)
        {
            Smoke(px, 3, 3, "....XXX..XX....", "..X.......XXX..", ".XX.X........X.", "X.....X.X.....X",
                "..X..........X.", "X......X.....X.", ".X..X.....X..X.", "..XX..X...XX...", "....X....X.....");
            return px;
        }
        int bob = phase is 1 or 2 ? 1 : 0;
        void Set(int x, int y, uint c)
        {
            y += bob;
            if (left) x = Width - 1 - x;
            if (x >= 0 && x < Width && y >= 0 && y < Height) px[y * Width + x] = c;
        }
        void Span(int y, int from, int to, uint c) { for (int x = from; x <= to; x++) Set(x, y, c); }

        // The robe, widening from the hood down to a ragged hem
        (int From, int To)[] robe = [(9, 13), (8, 14), (7, 15), (7, 15), (7, 15), (7, 15), (6, 15), (6, 15), (5, 15), (5, 15), (4, 15), (4, 15)];
        for (int i = 0; i < robe.Length; i++) Span(i + 2, robe[i].From, robe[i].To, RobeColor);
        Set(4, 13, RobeShade); Set(15, 13, RobeShade);
        for (int x = 4; x <= 15; x++)
            if ((x + phase / 2) % 3 != 0) Set(x, 14, RobeShade);
        // The dark hood with two glowing eyes
        Span(4, 9, 13, HoodDark); Span(5, 9, 13, HoodDark); Span(6, 9, 13, HoodDark); Span(7, 10, 12, HoodDark);
        Set(11, 5, SkullGlow); Set(13, 5, SkullGlow); // looking ahead
        // The scythe: a long handle held by a bony hand, the blade curving over the head
        for (int y = 0; y <= 14; y++) Set(17, y, ScytheHandle);
        Set(16, 8, BoneColor); Set(16, 9, BoneColor);
        Span(0, 9, 16, BladeColor);
        Set(7, 1, BladeColor); Set(8, 1, BladeColor); Set(9, 1, BladeShade);
        Set(6, 2, BladeShade);

        if (poof == 1)
            Smoke(px, 7, 6, "..XXX..", ".XXXXX.", "XXXXXXX", ".XXXXX.", "..XXX..");
        else if (poof == 2)
            Smoke(px, 3, 3, "....XXX..XX....", "..XXXXXXXXXXX..", ".XXXXXXXXXXXXX.", "XXXXXXXXXXXXXXX", "XXXXXXXXXXXXXXX",
                "XXXXXXXXXXXXXXX", ".XXXXXXXXXXXXX.", "..XXXXXXXXXXX..", "....XX..XXX....");
        return px;
    }

    /// <summary>A puff of smoke from rows of X, shaded where its underside shows.</summary>
    private static void Smoke(uint[] px, int left, int top, params string[] rows)
    {
        for (int y = 0; y < rows.Length; y++)
            for (int i = 0; i < rows[y].Length; i++)
            {
                if (rows[y][i] != 'X') continue;
                bool underside = y == rows.Length - 1 || i >= rows[y + 1].Length || rows[y + 1][i] != 'X';
                int x = left + i, row = top + y;
                if (x >= 0 && x < Width && row >= 0 && row < Height) px[row * Width + x] = underside ? SmokeShade : SmokeColor;
            }
    }

    private static BitmapSource ToBitmap(uint[] px)
    {
        var bitmap = new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, Width, Height), px, Width * 4, 0);
        bitmap.Freeze();
        return bitmap;
    }

    private static uint Blend(uint from, uint to, double k)
    {
        uint Channel(int shift)
        {
            double a = from >> shift & 0xFF, b = to >> shift & 0xFF;
            return (uint)Math.Round(a + (b - a) * k) << shift;
        }
        return 0xFF000000 | Channel(16) | Channel(8) | Channel(0);
    }

    /// <summary>Extra pixels on each side of the splat layer, for the puddle and the drops splashing off it.</summary>
    public const int SplatMargin = 14;
    /// <summary>Steps of the splat drawn on the splat layer; after them the pet rises back up (<see cref="SpriteFrame.Squash"/>).</summary>
    public const int SplatSteps = 8;
    private static readonly Dictionary<int, BitmapSource> SplatCache = new();

    /// <summary>
    /// A long fall turns the pet into a puddle, <see cref="SplatMargin"/> wider on each side than the pet:
    /// steps 0..3 it spreads out flat while three drops a side splash off in arcs and land, steps 4..7 the drops
    /// slide back and the puddle pulls itself together, a little higher each step.
    /// </summary>
    public static BitmapSource RenderSplat(int step)
    {
        if (SplatCache.TryGetValue(step, out var cached)) return cached;
        int width = Width + 2 * SplatMargin;
        var px = new uint[width * Height];
        void Set(int x, int y, uint c)
        {
            if (x >= 0 && x < width && y >= 0 && y < Height) px[y * width + x] = c;
        }
        var (body, shade) = _normal;
        int cx = SplatMargin + 11, bottom = Height - 1;

        // The puddle: wide and flat at first, then narrower and taller; its top edge wobbles.
        int[] widths = [24, 28, 30, 30, 26, 22, 18, 16], heights = [2, 2, 2, 2, 3, 3, 4, 5];
        int w = widths[step], h = heights[step], left = cx - w / 2, right = left + w - 1;
        for (int y = bottom - h + 1; y <= bottom; y++)
        {
            int fromTop = y - (bottom - h + 1);
            int inset = fromTop == 0 ? 2 : fromTop == 1 && h > 2 ? 1 : 0;
            for (int x = left + inset; x <= right - inset; x++)
            {
                // A couple of dents in the top edge, moving as it wobbles
                if (fromTop == 0 && (x - left + step) % 7 == 0) continue;
                Set(x, y, y == bottom ? shade : body);
            }
        }
        // Eyes peeking out of the puddle
        int eyeY = bottom - h + 1 + (h > 2 ? 1 : 0), eye = Math.Max(2, w / 6);
        foreach (int ex in (int[])[cx - eye - 1, cx - eye, cx + eye - 1, cx + eye]) Set(ex, eyeY, EyeColor);

        // Three drops a side: out in arcs (steps 0..3), then sliding back along the ground (4..7).
        (int Reach, int Peak)[] drops = [(4, 3), (7, 5), (10, 2)];
        foreach (var (reach, peak) in drops)
            foreach (int side in (int[])[-1, 1])
            {
                int edge = side < 0 ? left : right;
                double x, y;
                if (step < 4)
                {
                    double t = (step + 1) / 4.0;
                    x = edge + side * reach * t;
                    y = bottom - 4 * peak * t * (1 - t);
                }
                else
                {
                    double r = (step - 3) / 4.0;
                    int landed = (side < 0 ? cx - widths[3] / 2 : cx - widths[3] / 2 + widths[3] - 1) + side * reach;
                    x = landed + (edge - landed) * r;
                    y = bottom;
                }
                Set((int)Math.Round(x), (int)Math.Round(y), body);
            }

        var bitmap = new WriteableBitmap(width, Height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, width, Height), px, width * 4, 0);
        bitmap.Freeze();
        SplatCache[step] = bitmap;
        return bitmap;
    }

    /// <summary>Extra pixels on each side of the tear layer, so wailing tears can fly out past the pet's picture.</summary>
    public const int TearMargin = 6;
    private static readonly Dictionary<(int Phase, int Bob), BitmapSource> TearCache = new();

    /// <summary>
    /// Wailing: tears spurting from both eyes in a wide arc out to the sides, on a layer
    /// <see cref="TearMargin"/> wider on each side than the pet. <paramref name="phase"/> 0..2 moves the
    /// drops along the arc; <paramref name="bob"/> follows the body like in <see cref="Pixels"/>.
    /// </summary>
    public static BitmapSource RenderTears(int phase, int bob)
    {
        if (TearCache.TryGetValue((phase, bob), out var cached)) return cached;
        int width = Width + 2 * TearMargin;
        var px = new uint[width * Height];
        void Set(int x, int y)
        {
            if (x >= 0 && x < width && y >= 0 && y < Height) px[y * width + x] = SweatColor;
        }
        int c0 = TearMargin + 4, by = 5 + bob; // the body's left edge and top, as in Pixels()
        (int dx, int dy)[] arc = [(3, -1), (1, -3), (-1, -4), (-3, -4), (-5, -3), (-7, -1), (-8, 1), (-9, 3), (-9, 5), (-9, 7), (-9, 9)];
        for (int i = phase; i < arc.Length; i += 3)
        {
            Set(c0 + arc[i].dx, by + arc[i].dy);       // left eye, spraying left
            Set(c0 + 13 - arc[i].dx, by + arc[i].dy);  // right eye, mirrored
        }
        Set(c0 + 3, by + 4);
        Set(c0 + 10, by + 4);

        var bitmap = new WriteableBitmap(width, Height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, width, Height), px, width * 4, 0);
        bitmap.Freeze();
        TearCache[(phase, bob)] = bitmap;
        return bitmap;
    }

    private static readonly Dictionary<BedLayer, BitmapSource> BedCache = new();

    /// <summary>The blanket's top row in the bed: grabbing the pet below it takes the bed along.</summary>
    public const int BlanketTop = 10;

    /// <summary>
    /// A wooden bed in the pet's own 22x16 grid, the pet standing in it with its feet on the floor: head end on
    /// the left, foot end on the right, a pillow behind the head and a striped blanket up to its chest.
    /// </summary>
    public static BitmapSource RenderBed(BedLayer layer)
    {
        if (BedCache.TryGetValue(layer, out var cached)) return cached;
        var px = new uint[Width * Height];
        void Glyph(int x, uint color, int top, params string[] rows)
        {
            for (int y = 0; y < rows.Length; y++)
                for (int i = 0; i < rows[y].Length; i++)
                    if (rows[y][i] == 'X' && x + i < Width && top + y < Height) px[(top + y) * Width + x + i] = color;
        }
        if (layer != BedLayer.Front)
        {
            // Head end with a rounded top, foot end lower; the pillow peeks out behind the head.
            Glyph(0, BedWood, 6, ".XX", "XXX", "XXX", "XXX", "XXX", "XXX", "XXX", "XXX", "XXX", ".X.");
            Glyph(0, BedWoodShade, 6, ".X.", ".X.", ".X.", ".X.", ".X.", ".X.", ".X.", ".X.", ".X.");
            Glyph(3, White, 8, "XXX", "XXX");
        }
        if (layer != BedLayer.Back)
        {
            Glyph(3, White, BlanketTop, "XXXXXXXXXXXXXXXX");
            for (int y = 11; y <= 14; y++) Glyph(3, y % 2 == 0 ? BlanketShade : BlanketColor, y, "XXXXXXXXXXXXXXXX");
            Glyph(3, BedWoodShade, 15, "XXXXXXXXXXXXXXXX"); // the side rail hides the pet's feet
            Glyph(19, BedWood, 9, "XXX", "XXX", "XXX", "XXX", "XXX", "XXX", ".X.");
            Glyph(19, BedWoodShade, 9, ".X.", ".X.", ".X.", ".X.", ".X.", ".X.");
        }
        var bitmap = new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, Width, Height), px, Width * 4, 0);
        bitmap.Freeze();
        BedCache[layer] = bitmap;
        return bitmap;
    }

    /// <summary>Small tray icon showing the pet for the given frame.</summary>
    public static System.Drawing.Icon CreateIcon(SpriteFrame frame)
    {
        // Crop to the creature itself (arms + body + legs) and scale up with hard edges.
        const int cropX = 2, cropY = 4, cropW = 18, cropH = 12;
        var pixels = Pixels(frame);
        using var source = new System.Drawing.Bitmap(cropW, cropH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        for (int y = 0; y < cropH; y++)
            for (int x = 0; x < cropW; x++)
                source.SetPixel(x, y, System.Drawing.Color.FromArgb(unchecked((int)pixels[(y + cropY) * Width + x + cropX])));

        using var target = new System.Drawing.Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(target))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            int h = 32 * cropH / cropW;
            g.DrawImage(source, new System.Drawing.Rectangle(0, (32 - h) / 2, 32, h));
        }
        return System.Drawing.Icon.FromHandle(target.GetHicon());
    }

    // ---------------------------------------------------------------- colors

    /// <summary>Parses "#RRGGBB" (or "RRGGBB") into an opaque ARGB value.</summary>
    public static bool TryParseColor(string? text, out uint argb)
    {
        argb = 0;
        string hex = text?.Trim().TrimStart('#') ?? "";
        if (hex.Length != 6 || !uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint rgb)) return false;
        argb = 0xFF000000 | rgb;
        return true;
    }

    public static string ToHex(uint argb) => $"#{argb & 0xFFFFFF:X6}";

    private static (double H, double S, double L) ToHsl(uint argb)
    {
        double r = (argb >> 16 & 0xFF) / 255.0, g = (argb >> 8 & 0xFF) / 255.0, b = (argb & 0xFF) / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2, d = max - min;
        if (d == 0) return (0, 0, l);
        double s = d / (1 - Math.Abs(2 * l - 1));
        double h = max == r ? (g - b) / d % 6 : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h * 60, s, l);
    }

    private static uint FromHsl(double h, double s, double l)
    {
        h = (h % 360 + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        l = Math.Clamp(l, 0, 1);
        double c = (1 - Math.Abs(2 * l - 1)) * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = l - c / 2;
        (double r, double g, double b) = (int)(h / 60) switch
        {
            0 => (c, x, 0d),
            1 => (x, c, 0d),
            2 => (0d, c, x),
            3 => (0d, x, c),
            4 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        uint Byte(double v) => (uint)Math.Round(Math.Clamp(v + m, 0, 1) * 255);
        return 0xFF000000 | Byte(r) << 16 | Byte(g) << 8 | Byte(b);
    }
}

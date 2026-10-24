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
public enum Hat
{
    None, Crown, Flower, WizardHat, TopHat, CowboyHat, ChefHat, Cap, Headphones, HardHat, VikingHelmet, PropellerCap, Halo,
    // Football helmets in the colors of the NFL teams
    Cardinals, Falcons, Ravens, Bills, Panthers, Bears, Bengals, Browns, Cowboys, Broncos, Lions, Packers, Texans, Colts,
    Jaguars, Chiefs, Raiders, Chargers, Rams, Dolphins, Vikings, Patriots, Saints, Giants, Jets, Eagles, Steelers,
    FortyNiners, Seahawks, Buccaneers, Titans, Commanders,
    // The Super Mario characters
    Mario, Luigi, Wario, Waluigi, Peach, Daisy, Rosalina, Toad, Toadette, Bowser, Birdo,
    // Disney characters
    MickeyEars, Minnie, Donald, Goofy, Sorcerer, Elsa, SnowWhite, Ariel, DaisyDuck, Huey, Dewey, Louie, Chip, Simba, Timon,
    // Harry Potter characters
    Hermione, Ron, Snape, Hagrid, Draco, Bellatrix, SortingHat, McGonagall, Hedwig, Dobby,
}
[JsonConverter(typeof(JsonStringEnumConverter<FaceWear>))]
public enum FaceWear
{
    None, Sunglasses, NerdGlasses, Monocle, Moustache, EyePatch, ShyGuy, WallE, Eve, Pumbaa,
    HarryPotter, Dumbledore, Luna, DeathEater, Voldemort,
}
[JsonConverter(typeof(JsonStringEnumConverter<Neckwear>))]
public enum Neckwear
{
    None, BowTie, Scarf, Bell,
    // Fan scarves in the colors of the football clubs, by town
    Augsburg, UnionBerlin, Bremen, Dortmund, Elversberg, Frankfurt, Freiburg, Schalke, Hamburg, Cologne, Leverkusen,
    Mainz, Gladbach, Bayern, Paderborn, Stuttgart,
    // Donkey Kong's tie
    DonkeyKong,
    // Dale's Hawaiian shirt
    Dale,
    // The Hogwarts house scarves
    Gryffindor, Slytherin, Ravenclaw, Hufflepuff,
}
/// <summary>What the pet eats when it is fed.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Snack>))]
public enum Snack { Cookie, Beans }
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
    int Sick = 0,
    Snack Snack = Snack.Cookie);

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
    private const uint CloudOutline = 0xFF4A4A55;
    private const uint AlertColor = 0xFFE5484D;
    private const uint QuestionColor = 0xFFC9961A; // dark enough to stand out on its white cloud
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

    /// <summary>
    /// A fan scarf in a club's colors: its two rows around the neck and the end hanging down with its fringe,
    /// each 'a', 'b' or 'c' a pixel in that color.
    /// </summary>
    private readonly record struct FanScarf(uint A, uint B, uint C, string Top, string Bottom, string End, string Fringe);

    private const uint ClubWhite = 0xFFF4F4F4;
    private const uint ClubBlack = 0xFF1C1C1E;

    private static readonly Dictionary<Neckwear, FanScarf> FanScarves = new()
    {
        [Neckwear.Augsburg] = new(0xFFBA3733, 0xFF46714D, ClubWhite, "aacbbcaacbbcaa", "aacbbcaacbbcaa", "ac", "cc"),
        [Neckwear.UnionBerlin] = new(0xFFEB1923, ClubWhite, 0, "aaaaaaaaaaaaaa", "bbbbbbbbbbbbbb", "aa", "bb"),
        [Neckwear.Bremen] = new(0xFF1D9053, ClubWhite, 0, "aaabbbaaabbbaa", "aaabbbaaabbbaa", "aa", "bb"),
        [Neckwear.Dortmund] = new(0xFFFDE100, ClubBlack, 0, "aaabbbaaabbbaa", "aaabbbaaabbbaa", "aa", "bb"),
        [Neckwear.Elversberg] = new(ClubBlack, ClubWhite, 0, "aabbaabbaabbaa", "aabbaabbaabbaa", "aa", "bb"),
        [Neckwear.Frankfurt] = new(ClubBlack, 0xFFE1000F, ClubWhite, "aabacaabacaaba", "aabacaabacaaba", "aa", "bb"),
        [Neckwear.Freiburg] = new(0xFFD2001C, ClubWhite, ClubBlack, "aacaaacaaacaaa", "aacaaacaaacaaa", "aa", "bb"),
        [Neckwear.Schalke] = new(0xFF004D9D, ClubWhite, 0, "aaabaaabaaabaa", "aaabaaabaaabaa", "aa", "bb"),
        [Neckwear.Hamburg] = new(0xFF0A3F86, ClubWhite, ClubBlack, "aaaabbbccccaaa", "aaaabbbccccaaa", "cc", "bb"),
        [Neckwear.Cologne] = new(0xFFED1C24, ClubWhite, 0, "bbbabbbabbbabb", "bbbabbbabbbabb", "bb", "aa"),
        [Neckwear.Leverkusen] = new(0xFFE32221, ClubBlack, 0, "aaabbbaaabbbaa", "aaabbbaaabbbaa", "aa", "bb"),
        [Neckwear.Mainz] = new(0xFFC3141E, ClubWhite, 0, "ababababababab", "bababababababa", "ab", "ba"),
        [Neckwear.Gladbach] = new(ClubBlack, ClubWhite, 0xFF00A859, "aabcbaabcbaabc", "aabcbaabcbaabc", "aa", "bb"),
        [Neckwear.Bayern] = new(0xFFDC052D, ClubWhite, 0, "aaabbbaaabbbaa", "aaabbbaaabbbaa", "aa", "bb"),
        [Neckwear.Paderborn] = new(0xFF005CA9, ClubBlack, 0, "aaabbbaaabbbaa", "aaabbbaaabbbaa", "aa", "bb"),
        [Neckwear.Stuttgart] = new(0xFFE32219, ClubWhite, 0, "bbbbbbbbbbbbbb", "aaaaaaaaaaaaaa", "bb", "aa"),
    };

    /// <summary>Whether this is one of the fan scarves in a football club's colors.</summary>
    public static bool IsFanScarf(Neckwear neck) => FanScarves.ContainsKey(neck);

    /// <summary>A football helmet in a team's colors: its shell, the stripe down the middle and the face mask.</summary>
    private readonly record struct FootballHelmet(uint Shell, uint Stripe, uint Mask);

    private static readonly Dictionary<Hat, FootballHelmet> FootballHelmets = new()
    {
        [Hat.Cardinals] = new(ClubWhite, 0xFF97233F, ClubBlack),
        [Hat.Falcons] = new(ClubBlack, 0xFFA71930, ClubBlack),
        [Hat.Ravens] = new(ClubBlack, 0xFF4B2C8C, ClubBlack),
        [Hat.Bills] = new(ClubWhite, 0xFFC60C30, 0xFF00338D),
        [Hat.Panthers] = new(0xFFA5ACAF, 0xFF0085CA, ClubBlack),
        [Hat.Bears] = new(0xFF0B162A, 0xFFC83803, 0xFF0B162A),
        [Hat.Bengals] = new(0xFFFB4F14, ClubBlack, ClubBlack),
        [Hat.Browns] = new(0xFFFF3C00, 0xFF311D00, 0xFF311D00),
        [Hat.Cowboys] = new(0xFFB0B7BC, 0xFF041E42, 0xFF041E42),
        [Hat.Broncos] = new(0xFF002244, 0xFFFB4F14, 0xFF002244),
        [Hat.Lions] = new(0xFFB0B7BC, 0xFF0076B6, 0xFF0076B6),
        [Hat.Packers] = new(0xFFFFB612, 0xFF203731, 0xFF203731),
        [Hat.Texans] = new(0xFF03202F, 0xFFA71930, 0xFF03202F),
        [Hat.Colts] = new(ClubWhite, 0xFF002C5F, 0xFF002C5F),
        [Hat.Jaguars] = new(ClubBlack, 0xFF006778, ClubBlack),
        [Hat.Chiefs] = new(0xFFE31837, 0xFFE31837, ClubWhite),
        [Hat.Raiders] = new(0xFFA5ACAF, ClubBlack, ClubBlack),
        [Hat.Chargers] = new(ClubWhite, 0xFF0080C6, 0xFFFFC20E),
        [Hat.Rams] = new(0xFF003594, 0xFFFFA300, 0xFF003594),
        [Hat.Dolphins] = new(ClubWhite, 0xFF008E97, 0xFF008E97),
        [Hat.Vikings] = new(0xFF4F2683, 0xFFFFC62F, 0xFF4F2683),
        [Hat.Patriots] = new(0xFFB0B7BC, 0xFFC60C30, 0xFF002244),
        [Hat.Saints] = new(0xFFD3BC8D, ClubBlack, ClubBlack),
        [Hat.Giants] = new(0xFF0B2265, 0xFFA71930, 0xFFA5ACAF),
        [Hat.Jets] = new(0xFF125740, ClubWhite, 0xFF125740),
        [Hat.Eagles] = new(0xFF004C54, 0xFFA5ACAF, ClubBlack),
        [Hat.Steelers] = new(ClubBlack, 0xFFFFB612, ClubBlack),
        [Hat.FortyNiners] = new(0xFFB3995D, 0xFFAA0000, 0xFFAA0000),
        [Hat.Seahawks] = new(0xFF002244, 0xFF69BE28, 0xFF002244),
        [Hat.Buccaneers] = new(0xFF34302B, 0xFFD50A0A, ClubBlack),
        [Hat.Titans] = new(0xFF0C2340, 0xFF4B92DB, 0xFF0C2340),
        [Hat.Commanders] = new(0xFF5A1414, 0xFFFFB612, 0xFF5A1414),
    };

    /// <summary>Whether this is one of the football helmets in an NFL team's colors.</summary>
    public static bool IsFootballHelmet(Hat hat) => FootballHelmets.ContainsKey(hat);

    /// <summary>
    /// A plumber's cap: its color, the round emblem on the front and the letter on it, five pixels wide and three
    /// tall (X = letter).
    /// </summary>
    private readonly record struct PlumberCap(uint Cap, uint Emblem, uint Letter, string[] Glyph);

    private static readonly Dictionary<Hat, PlumberCap> PlumberCaps = new()
    {
        [Hat.Mario] = new(0xFFE52521, ClubWhite, 0xFFE52521, ["XX.XX", "X.X.X", "X...X"]),
        [Hat.Luigi] = new(0xFF3DA33F, ClubWhite, 0xFF3DA33F, [".X...", ".X...", ".XXX."]),
        [Hat.Wario] = new(0xFFF5C400, ClubWhite, 0xFF2E5FC9, ["X...X", "X.X.X", "XX.XX"]),
        [Hat.Waluigi] = new(0xFF5F2C82, 0xFFF5C400, 0xFF5F2C82, [".XXX.", ".X...", ".X..."]),
    };

    /// <summary>A princess's crown and hair: <paramref name="Locks"/> rows of hair down the sides of the head.</summary>
    private readonly record struct Princess(uint Crown, uint CrownShade, uint Jewel, uint SideJewel, uint Hair, uint HairShade, int Locks);

    private static readonly Dictionary<Hat, Princess> Princesses = new()
    {
        [Hat.Peach] = new(GoldColor, GoldShade, 0xFF3B6FE0, JewelColor, 0xFFFFDD6E, 0xFFE0B64A, 6),
        [Hat.Daisy] = new(GoldColor, GoldShade, 0xFF2BB3A0, 0xFF2BB3A0, 0xFFC8662A, 0xFF9C4A1C, 3),
        [Hat.Rosalina] = new(0xFFD9E2EE, 0xFFA6B2C4, 0xFF3B6FE0, JewelColor, 0xFFF4EED8, 0xFFD2C9A8, 6),
    };

    /// <summary>
    /// How far a hat is moved right from where its picture starts, so that it sits in the middle of the head; the
    /// ones that already span the whole head stay where they are, and so does the flower tucked in at the side.
    /// </summary>
    private static int HatShift(Hat hat) => hat switch
    {
        Hat.WizardHat or Hat.CowboyHat => 5,
        Hat.Birdo => 3,
        Hat.Crown or Hat.TopHat or Hat.ChefHat or Hat.Cap or Hat.HardHat or Hat.VikingHelmet
            or Hat.PropellerCap or Hat.Halo or Hat.Mario or Hat.Luigi or Hat.Wario or Hat.Waluigi => 4,
        _ => 0,
    };

    private static readonly Dictionary<Neckwear, FanScarf> HouseScarves = new()
    {
        [Neckwear.Gryffindor] = new(0xFF8A1C1C, 0xFFEEBA30, 0, "aaababaaababaa", "aaababaaababaa", "aa", "bb"),
        [Neckwear.Slytherin] = new(0xFF1A472A, 0xFFAAAAAA, 0, "aaababaaababaa", "aaababaaababaa", "aa", "bb"),
        [Neckwear.Ravenclaw] = new(0xFF1E2F6B, 0xFF946B2D, 0, "aaababaaababaa", "aaababaaababaa", "aa", "bb"),
        [Neckwear.Hufflepuff] = new(0xFFECB939, 0xFF372E29, 0, "aaababaaababaa", "aaababaaababaa", "aa", "bb"),
    };

    /// <summary>Whether this costume item belongs to a Harry Potter character (or a Hogwarts house).</summary>
    public static bool IsWizardingCostume(Outfit item) =>
        item.Hat is >= Hat.Hermione and <= Hat.Dobby || item.Face is >= FaceWear.HarryPotter and <= FaceWear.Voldemort
        || HouseScarves.ContainsKey(item.Neck);

    /// <summary>Whether this costume item belongs to a Disney character.</summary>
    public static bool IsDisneyCostume(Outfit item) =>
        item.Hat is >= Hat.MickeyEars and <= Hat.Timon || item.Face is FaceWear.WallE or FaceWear.Eve or FaceWear.Pumbaa
        || item.Neck == Neckwear.Dale;

    /// <summary>Whether this costume item belongs to a Super Mario character.</summary>
    public static bool IsMarioCostume(Outfit item) =>
        item.Hat is >= Hat.Mario and <= Hat.Birdo || item.Face == FaceWear.ShyGuy || item.Neck == Neckwear.DonkeyKong;

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
        if (f.Outfit.Face is not (FaceWear.Sunglasses or FaceWear.ShyGuy or FaceWear.WallE or FaceWear.Eve or FaceWear.Luna
            or FaceWear.DeathEater or FaceWear.Voldemort))
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

        // Microphone held upright in the left hand, next to the face: a silver head with a dark grille (the left,
        // because the thought cloud floats up on the right)
        if (f.Arms == Arms.Mic)
        {
            Glyph(c0 - 4, MicColor, by + 1, "XXX", "XXX");
            Glyph(c0 - 4, HandleColor, by + 1, ".X.", "X.X");
            for (int y = by + 3; y <= by + 6; y++) Set(c0 - 3, y, HandleColor);
        }

        if (f.Blush)
        {
            Set(c0 + 1, by + 4, BlushColor); Set(c0 + 2, by + 4, BlushColor);
            Set(c0 + 11, by + 4, BlushColor); Set(c0 + 12, by + 4, BlushColor);
        }

        }

        // Outfit, one item per slot. Face and neck need the unsquashed body; a seasonal or night accessory takes the head.
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
                // Shy Guy's round white mask over the whole face, with two dark eye holes and a mouth hole
                case FaceWear.ShyGuy:
                    Glyph(c0 + 1, FurWhite, by + 1, "..XXXXXXXX..", ".XXXXXXXXXX.", "XXXXXXXXXXXX", "XXXXXXXXXXXX", ".XXXXXXXXXX.", "..XXXXXXXX..");
                    Glyph(c0 + 1, EyeColor, by + 2, "...X....X...", "...X....X...", "............", ".....XX.....");
                    break;
                // WALL-E's eyes: two grey binocular boxes with dark lenses, joined in the middle
                case FaceWear.WallE:
                    Glyph(c0 + 1, HelmetColor, by + 1, "XXXXX..XXXXX", "XXXXXXXXXXXX", "XXXXX..XXXXX", "XXXXX..XXXXX");
                    Glyph(c0 + 1, LensColor, by + 2, ".XXX....XXX.", ".XXX....XXX.");
                    Set(c0 + 2, by + 2, GlintColor);
                    Set(c0 + 9, by + 2, GlintColor);
                    break;
                // Pumbaa's snout with two nostrils, between two white tusks curving up
                case FaceWear.Pumbaa:
                    Glyph(c0 + 4, 0xFF8A5A44, by + 4, ".XXXX.", "XXXXXX", ".XXXX.");
                    Glyph(c0 + 5, EyeColor, by + 5, "X..X");
                    Glyph(c0 + 2, FurWhite, by + 3, "X........X", "X........X", ".X......X.");
                    break;
                // Harry's round black glasses, and the lightning scar on his forehead
                case FaceWear.HarryPotter:
                    Glyph(c0 + 1, FrameColor, by + 1, ".XXX....XXX.", "X...XXXX...X", "X...X..X...X", ".XXX....XXX.");
                    Glyph(c0 + 5, 0xFF9C3324, by, "..X", ".XX", ".X.");
                    break;
                // Dumbledore's long silver beard below a moustache, reaching down between the legs
                case FaceWear.Dumbledore:
                    Glyph(c0 + 2, 0xFFE8E8EC, by + 4, ".XXXXXXXX.", "XXX....XXX", "XXXX..XXXX", ".XXXXXXXX.", "..XXXXXX..", "...XXXX...", "....XX....");
                    Glyph(c0 + 2, 0xFFC4C4CE, by + 7, ".X......X.", "..X....X..", "...X..X...");
                    break;
                // Luna's spectrespecs: big pink frames with colorful lenses
                case FaceWear.Luna:
                    Glyph(c0 + 1, 0xFFD0408E, by + 1, ".XXX....XXX.", "X...XXXX...X", "X...X..X...X", ".XXX....XXX.");
                    Glyph(c0 + 2, 0xFFF48FBF, by + 2, "XXX", "XXX");
                    Glyph(c0 + 9, 0xFF5EC8F2, by + 2, "XXX", "XXX");
                    Glyph(c0 + 2, 0xFFF5C400, by + 2, "..X......X");
                    break;
                // A Death Eater's silver mask with dark eye holes and engraved swirls
                case FaceWear.DeathEater:
                    Glyph(c0 + 1, 0xFFC9CED8, by + 1, "..XXXXXXXX..", ".XXXXXXXXXX.", "XXXXXXXXXXXX", "XXXXXXXXXXXX", ".XXXXXXXXXX.", "..XXXXXXXX..");
                    Glyph(c0 + 1, 0xFF7E8594, by + 1, ".....XX.....", "X..........X", ".X..X..X..X.", "............", "..X......X..", ".....XX.....");
                    Glyph(c0 + 1, EyeColor, by + 2, "..XX....XX..", "..XX....XX..");
                    break;
                // Voldemort's pale face with red slit eyes, slits for a nose and a thin mouth
                case FaceWear.Voldemort:
                    Glyph(c0 + 1, 0xFFE4E9DC, by + 1, ".XXXXXXXXXX.", "XXXXXXXXXXXX", "XXXXXXXXXXXX", "XXXXXXXXXXXX", "XXXXXXXXXXXX", ".XXXXXXXXXX.");
                    Glyph(c0 + 1, 0xFFD02020, by + 2, "..X......X..", "..X......X..");
                    Glyph(c0 + 1, 0xFF7E8594, by + 4, ".....X.X....", "....XXXX....");
                    break;
                // EVE's face: a black visor with two glowing blue eyes
                case FaceWear.Eve:
                    Glyph(c0 + 1, EyeColor, by + 1, ".XXXXXXXXXX.", "XXXXXXXXXXXX", "XXXXXXXXXXXX", ".XXXXXXXXXX.");
                    Glyph(c0 + 1, 0xFF5EC8F2, by + 2, "..XX....XX..", "..XX....XX..");
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
                // Donkey Kong's red tie, hanging down between the legs, with a yellow mark
                case Neckwear.DonkeyKong:
                    Glyph(c0 + 5, SantaRed, by + 7, ".XX.", "XXXX", ".XX.", ".XX.");
                    Glyph(c0 + 6, BindingColor, by + 7, "XX");
                    Glyph(c0 + 6, GoldColor, by + 8, "X");
                    break;
                // Dale's red Hawaiian shirt with yellow flowers and a white collar
                case Neckwear.Dale:
                    Glyph(c0, SantaRed, by + 7, "XXXXXXXXXXXXXX", "XXXXXXXXXXXXXX");
                    Glyph(c0, 0xFFF5C400, by + 7, ".X........X...", "...X....X....X");
                    Glyph(c0 + 5, FurWhite, by + 7, "X..X", ".XX.");
                    break;
                // A fan scarf in a club's colors, one end hanging down between the legs
                case var club when FanScarves.TryGetValue(club, out var scarf) || HouseScarves.TryGetValue(club, out scarf):
                    Stripes(c0, by + 7, scarf, scarf.Top, scarf.Bottom);
                    Stripes(c0 + 7, by + 9, scarf, scarf.End, scarf.Fringe);
                    break;
            }
        }
        // Headwear sits in the middle of the head (Shift moves the smaller items, drawn from its left edge, there).
        var hat = f.Accessory != Accessory.None ? Hat.None : f.Outfit.Hat;
        int h0 = c0 + HatShift(hat);
        switch (hat)
        {
            // A golden crown with a red jewel, sitting on the head
            case Hat.Crown:
                Glyph(h0, GoldColor, by - 3, "X.X.X", "XXXXX", "XXXXX");
                Glyph(h0, GoldShade, by - 3, ".....", ".....", "XXXXX");
                Set(h0 + 2, by - 2, JewelColor);
                break;
            // A little daisy tucked onto the head
            case Hat.Flower:
                Glyph(h0 + 1, PetalColor, by - 3, ".X.", "X.X", ".X.");
                Set(h0 + 2, by - 2, GoldColor);
                Set(h0 + 3, by - 1, LeafColor);
                break;
            // A pointed wizard's hat with a little star, its tip bent to the side
            case Hat.WizardHat:
                Glyph(h0 - 1, HatColor, by - 5, "....X.", "...XX.", "..XXX.", ".XXXX.");
                Glyph(h0 - 1, HatShade, by - 1, "XXXXXX");
                Set(h0 + 2, by - 3, GoldColor);
                break;
            // A tall black top hat with a red band
            case Hat.TopHat:
                Glyph(h0, TopHatColor, by - 5, "XXXXX", "XXXXX", "XXXXX");
                Glyph(h0, TopHatShine, by - 5, ".X...", ".X...", ".X...");
                Glyph(h0, SantaRed, by - 2, "XXXXX");
                Glyph(h0 - 1, TopHatColor, by - 1, "XXXXXXX");
                break;
            // A brown cowboy hat with a dented crown and a brim curling up at both ends
            case Hat.CowboyHat:
                Glyph(h0, CowboyColor, by - 4, "XX.XX", "XXXXX");
                Glyph(h0 - 2, CowboyColor, by - 2, "X......X", "XXXXXXXX");
                Glyph(h0, CowboyShade, by - 2, "XXXXX");
                break;
            // A puffy white chef's hat
            case Hat.ChefHat:
                Glyph(h0 - 1, FurWhite, by - 5, ".XX.XX.", "XXXXXXX", "XXXXXXX", ".XXXXX.");
                Glyph(h0 - 1, ChefShade, by - 3, "..X.X..", ".......", ".......", ".XXXXX.");
                break;
            // A blue cap with a white button, its peak pointing to the left
            case Hat.Cap:
                Glyph(h0, CapColor, by - 3, ".XXX.", "XXXXX", "XXXXX");
                Glyph(h0 - 3, CapShade, by - 1, "XXX");
                Set(h0 + 2, by - 4, FurWhite);
                break;
            // Headphones: a band over the head and a red cup on each side
            case Hat.Headphones:
                Glyph(h0, HandleColor, by - 1, ".XXXXXXXXXXXX.", "X............X");
                Glyph(h0 - 1, AlertColor, by + 1, "XX", "XX", "XX");
                Glyph(h0 + 13, AlertColor, by + 1, "XX", "XX", "XX");
                break;
            // A yellow hard hat with a ridge down the middle and a brim all around
            case Hat.HardHat:
                Glyph(h0 - 1, GoldColor, by - 4, "..XXX..", ".XXXXX.", ".XXXXX.");
                Glyph(h0 - 1, GoldShade, by - 4, "...X...", "...X...", "...X...", "XXXXXXX");
                break;
            // A grey viking helmet with two white horns curving up
            case Hat.VikingHelmet:
                Glyph(h0, HelmetColor, by - 3, ".XXX.", "XXXXX", "XXXXX");
                Glyph(h0, HelmetShade, by - 1, "XXXXX");
                Glyph(h0 - 2, BoneColor, by - 4, "X.......X", "X.......X", ".X.....X.");
                break;
            // A colorful beanie with a little propeller on top
            case Hat.PropellerCap:
                Glyph(h0, SantaRed, by - 2, ".X...", "XX...");
                Glyph(h0, GoldColor, by - 2, "..X..", "..X..");
                Glyph(h0, BallColor, by - 2, "...X.", "...XX");
                Set(h0 + 2, by - 3, HandleColor);
                Glyph(h0, LeafColor, by - 4, "XX...");
                Glyph(h0, BallColor, by - 4, "...XX");
                break;
            // A golden halo floating above the head
            case Hat.Halo:
                Glyph(h0, GoldColor, by - 4, ".XXXX.", "X....X", ".XXXX.");
                break;
            // A plumber's cap with a round emblem and its letter on the front, the brim reaching out on both sides
            case var plumber when PlumberCaps.TryGetValue(plumber, out var cap):
                Glyph(h0 - 2, cap.Cap, by - 5, "..XXXXX..", ".XXXXXXX.", "XXXXXXXXX", "XXXXXXXXX");
                Glyph(h0, cap.Emblem, by - 4, ".XXX.", "XXXXX", "XXXXX");
                Glyph(h0, cap.Letter, by - 4, cap.Glyph);
                Glyph(h0 - 3, Blend(cap.Cap, 0xFF000000, 0.25), by - 1, "XXXXXXXXXXX");
                break;
            // A princess's crown with jewels on her hair, which frames the face and hangs down the sides
            case var princess when Princesses.TryGetValue(princess, out var royal):
                Glyph(h0 - 1, royal.Hair, by - 1, ".XXXXXXXXXXXXXX.");
                Glyph(h0 - 1, royal.Hair, by, "XXXXXXXXXXXXXXXX");
                for (int i = 1; i <= royal.Locks && f.Squash == 0; i++)
                    Glyph(h0 - 1, i == royal.Locks ? royal.HairShade : royal.Hair, by + i, "XX............XX");
                if (princess == Hat.Rosalina && f.Squash == 0) // her long bang over the left eye
                    Glyph(h0 + 1, royal.Hair, by + 1, "XXXX", "XXXX", ".XXX", "..X.");
                Glyph(h0 + 4, royal.Crown, by - 4, "X.XX.X", "XXXXXX", "XXXXXX");
                Glyph(h0 + 4, royal.CrownShade, by - 2, "XXXXXX");
                Glyph(h0 + 4, royal.SideJewel, by - 3, ".X..X.");
                Glyph(h0 + 4, royal.Jewel, by - 3, "..XX..");
                break;
            // Toad's and Toadette's big mushroom caps, white with red spots or pink with white spots
            case Hat.Toad or Hat.Toadette:
            {
                var (top, spots) = f.Outfit.Hat == Hat.Toad ? (FurWhite, SantaRed) : (0xFFF48FBF, FurWhite);
                Glyph(h0 - 1, top, by - 4, ".....XXXXXX.....", "...XXXXXXXXXX...", ".XXXXXXXXXXXXXX.", "XXXXXXXXXXXXXXXX");
                Glyph(h0 - 1, spots, by - 4, "......XXXX......", "...XX......XX...", "..XXX......XXX..", "................");
                Glyph(h0 - 1, top == FurWhite ? ChefShade : 0xFFD06E9E, by, "XXXXXXXXXXXXXXXX");
                break;
            }
            // Bowser's two horns and his red spiky hair
            case Hat.Bowser:
                Glyph(h0 - 1, BoneColor, by - 3, "X..............X", ".X............X.", ".XX..........XX.");
                Glyph(h0 + 3, 0xFFE2591B, by - 3, "X.X.X.X.", "XXXXXXXX", "XXXXXXXX");
                break;
            // Birdo's big red bow with white dots
            case Hat.Birdo:
                Glyph(h0, SantaRed, by - 4, "XX...XX", "XXX.XXX", "XXXXXXX", "XXX.XXX", "XX...XX");
                Glyph(h0, BindingColor, by - 2, "...X...");
                Glyph(h0, FurWhite, by - 3, ".X...X.", ".......", "X.....X");
                break;
            // Mickey's round black ears; Minnie's with a red bow with white dots between them
            case Hat.MickeyEars or Hat.Minnie:
                Glyph(c0, EyeColor, by - 4, ".XX.", "XXXX", "XXXX", ".XX.");
                Glyph(c0 + 10, EyeColor, by - 4, ".XX.", "XXXX", "XXXX", ".XX.");
                if (hat == Hat.Minnie)
                {
                    Glyph(c0 + 4, SantaRed, by - 3, "XX..XX", "XXXXXX", "XX..XX");
                    Glyph(c0 + 6, BindingColor, by - 2, "XX");
                    Glyph(c0 + 4, FurWhite, by - 3, ".X....", "......", "....X.");
                }
                break;
            // Donald's blue sailor cap with a black band, its ribbon fluttering to the right
            case Hat.Donald:
                Glyph(c0 + 3, 0xFF3B6FD4, by - 3, ".XXXXXX.", "XXXXXXXX");
                Glyph(c0 + 3, EyeColor, by - 1, "XXXXXXXX");
                Glyph(c0 + 11, EyeColor, by - 1, "X.", ".X");
                break;
            // Goofy's crumpled green hat with a black band
            case Hat.Goofy:
                Glyph(c0 + 4, 0xFF3F8F3A, by - 5, ".XXX..", ".XXXX.", "XXXXXX");
                Glyph(c0 + 4, EyeColor, by - 2, "XXXXXX");
                Glyph(c0 + 3, 0xFF3F8F3A, by - 1, "XXXXXXXX");
                break;
            // The sorcerer's apprentice: a tall blue pointed hat with a golden moon and star
            case Hat.Sorcerer:
                Glyph(c0 + 3, 0xFF2E4FB8, by - 5, "...X...", "..XXX..", "..XXX..", ".XXXXX.", "XXXXXXX");
                Set(c0 + 7, by - 4, GoldColor);
                Glyph(c0 + 4, GoldColor, by - 2, "X.", ".X");
                break;
            // Elsa: platinum hair with a long braid over the left shoulder and an icy crown
            case Hat.Elsa:
            {
                uint hair = 0xFFF2EBD3, braid = 0xFFD2C9A8;
                Glyph(c0 - 1, hair, by - 1, ".XXXXXXXXXXXXXX.", "XXXXXXXXXXXXXXXX");
                if (f.Squash == 0)
                {
                    Glyph(c0 + 13, hair, by + 1, "XX", "XX");
                    Glyph(c0 - 1, hair, by + 1, "XX", "XX", "XX", "XX", "XX", ".X");
                    Glyph(c0 - 1, braid, by + 2, "X.", "..", ".X", "..", "X.");
                }
                Glyph(c0 + 4, 0xFFB8E4F5, by - 3, "X.XX.X", "XXXXXX");
                Glyph(c0 + 6, 0xFF3B6FE0, by - 2, "XX");
                break;
            }
            // Snow White: a short black bob with a red bow on top
            case Hat.SnowWhite:
                Glyph(c0 - 1, 0xFF1E1E2E, by - 1, ".XXXXXXXXXXXXXX.", "XXXXXXXXXXXXXXXX");
                if (f.Squash == 0) Glyph(c0 - 1, 0xFF1E1E2E, by + 1, "XX............XX", "XX............XX");
                Glyph(c0 + 4, SantaRed, by - 3, "XX..XX", "XXXXXX");
                Glyph(c0 + 6, BindingColor, by - 3, "XX");
                break;
            // Ariel: long red hair, swept up high over the head and down both sides
            case Hat.Ariel:
                Glyph(c0 - 1, 0xFFE2382A, by - 2, "...XXXXXXXXX....", ".XXXXXXXXXXXXXX.", "XXXXXXXXXXXXXXXX");
                for (int i = 1; i <= 6 && f.Squash == 0; i++)
                    Glyph(c0 - 1, i == 6 ? 0xFFB02A20 : 0xFFE2382A, by + i, "XX............XX");
                break;
            // Daisy Duck's big purple bow
            case Hat.DaisyDuck:
                Glyph(c0 + 3, 0xFFC47BD6, by - 3, "XXX..XXX", "XXXXXXXX", "XXX..XXX");
                Glyph(c0 + 6, 0xFF9A55B0, by - 2, "XX");
                break;
            // Huey's, Dewey's and Louie's caps: red, blue and green
            case Hat.Huey or Hat.Dewey or Hat.Louie:
            {
                uint color = hat switch { Hat.Huey => SantaRed, Hat.Dewey => CapColor, _ => 0xFF3F8F3A };
                Glyph(c0 + 4, color, by - 4, ".XXXX.", "XXXXXX", "XXXXXX");
                Glyph(c0 + 3, Blend(color, 0xFF000000, 0.25), by - 1, "XXXXXXXX");
                break;
            }
            // Chip's brown fedora with a dark band
            case Hat.Chip:
                Glyph(c0 + 4, CowboyColor, by - 4, ".XXXX.", "XXXXXX");
                Glyph(c0 + 4, 0xFF3A2414, by - 2, "XXXXXX");
                Glyph(c0 + 2, CowboyShade, by - 1, "XXXXXXXXXX");
                break;
            // Simba's mane: a shaggy orange-brown mane around the whole head
            case Hat.Simba:
                Glyph(c0 - 1, 0xFFC0702A, by - 2, "..X.X.X..X.X.X..", ".XXXXXXXXXXXXXX.", "XXXXXXXXXXXXXXXX");
                for (int i = 1; i <= 5 && f.Squash == 0; i++)
                    Glyph(c0 - 1, i == 5 ? 0xFF8A4A1C : 0xFFC0702A, by + i, "XX............XX");
                break;
            // Timon's red tuft of hair, sticking up
            case Hat.Timon:
                Glyph(c0 + 4, 0xFFC2452D, by - 3, "X.X.X.", ".XXXX.", "XXXXXX");
                break;
            // Hermione's and Bellatrix's big bushy hair, brown or wild black
            case Hat.Hermione or Hat.Bellatrix:
            {
                var (hair, tips) = hat == Hat.Hermione ? (0xFF7A4A26u, 0xFF5A3418u) : (0xFF18181E, 0xFF34343E);
                Glyph(c0 - 2, hair, by - 2, hat == Hat.Hermione ? "...XXXXXXXXXXXX..." : "..X.XXXX.XXXX.X...",
                    ".XXXXXXXXXXXXXXXX.", "XXXXXXXXXXXXXXXXXX");
                for (int i = 1; i <= 5 && f.Squash == 0; i++)
                    Glyph(c0 - 2, i == 5 ? tips : hair, by + i, i == 5 ? ".XX............XX." : "XXX............XXX");
                break;
            }
            // Ron's red hair
            case Hat.Ron:
                Glyph(c0 - 1, 0xFFD9622B, by - 1, ".XXXXXXXXXXXXXX.", "XXXXXXXXXXXXXXXX");
                if (f.Squash == 0) Glyph(c0 - 1, 0xFFD9622B, by + 1, "XX.X........X.XX", "XX............XX");
                break;
            // Snape's greasy black hair, hanging down like curtains beside the face
            case Hat.Snape:
                Glyph(c0 - 1, 0xFF1C1C22, by - 1, ".XXXXXXXXXXXXXX.", "XXXXXXXXXXXXXXXX");
                for (int i = 1; i <= 6 && f.Squash == 0; i++) Glyph(c0 - 1, 0xFF1C1C22, by + i, "XXX..........XXX");
                break;
            // Hagrid's wild dark hair and a big bushy beard around the mouth
            case Hat.Hagrid:
            {
                uint hair = 0xFF2B2018;
                Glyph(c0 - 1, hair, by - 2, "..X.XXXXXXXX.X..", ".XXXXXXXXXXXXXX.", "XXXXXXXXXXXXXXXX");
                if (f.Squash == 0)
                {
                    Glyph(c0 - 1, hair, by + 1, "XX............XX", "XX............XX", "XX............XX");
                    Glyph(c0 - 1, hair, by + 4, "XXX..........XXX", "XXXX........XXXX", "XXXXXX....XXXXXX", ".XXXXXXXXXXXXXX.", "..XXXXXXXXXXXX..");
                }
                break;
            }
            // Draco's slicked-back platinum hair
            case Hat.Draco:
                Glyph(c0 - 1, 0xFFE8E2C8, by - 2, "....XXXXXXXX....", "..XXXXXXXXXXXX..", "XXXXXXXXXXXXXXXX");
                Glyph(c0 - 1, 0xFFC9C1A0, by - 1, "...X..X..X..X...");
                break;
            // The Sorting Hat: a floppy, patched brown hat with a creased face
            case Hat.SortingHat:
                Glyph(c0 + 2, 0xFF8A6A44, by - 5, ".......XX.", ".....XXXX.", "....XXXXX.", "..XXXXXXX.", "XXXXXXXXXX");
                Glyph(c0 + 2, 0xFF5E4628, by - 3, ".....X.X..", "....XXX...", "XXXXXXXXXX");
                break;
            // McGonagall's tall dark green witch's hat
            case Hat.McGonagall:
                Glyph(c0 + 3, 0xFF1F4D2E, by - 5, "....X...", "...XX...", "...XXX..", "..XXXX..", "XXXXXXXX");
                Glyph(c0 + 3, 0xFF2E6B43, by - 2, "..XXXX..");
                break;
            // Hedwig, the snowy owl, sitting on the head
            case Hat.Hedwig:
                Glyph(c0 + 4, FurWhite, by - 5, ".XXXX.", "XXXXXX", "XXXXXX", "XXXXXX", ".XXXX.");
                Glyph(c0 + 4, EyeColor, by - 4, ".X..X.", "......", "...X..", "X.....");
                Glyph(c0 + 6, 0xFFE0A030, by - 3, "XX");
                break;
            // Dobby's big floppy ears, sticking out to the sides
            case Hat.Dobby:
                if (f.Squash == 0)
                {
                    Glyph(c0 - 4, 0xFFB9B49A, by, "XXXX", ".XXX", "..XX");
                    Glyph(c0 + 14, 0xFFB9B49A, by, "XXXX", "XXX.", "XX..");
                    Glyph(c0 - 4, 0xFFD9A0A0, by, ".XX.", "..X.");
                    Glyph(c0 + 14, 0xFFD9A0A0, by, ".XX.", ".X..");
                }
                break;
            // A football helmet in a team's colors around the whole head: the shell with a stripe down the middle,
            // ear flaps down the sides and a face mask bar below the eyes (just the shell on a squashed pet)
            case var team when FootballHelmets.TryGetValue(team, out var helmet):
                Glyph(h0 + 4, helmet.Shell, by - 2, "XXXXXX");
                Glyph(h0 + 1, helmet.Shell, by - 1, "XXXXXXXXXXXX");
                Glyph(h0 - 1, helmet.Shell, by, "XXXXXXXXXXXXXXXX");
                Glyph(h0 + 6, helmet.Stripe, by - 2, "XX", "XX", "XX");
                if (f.Squash == 0)
                {
                    uint flap = Blend(helmet.Shell, 0xFF000000, 0.22);
                    Glyph(h0 - 1, flap, by + 1, "XX............XX", "XX............XX", "XX............XX");
                    Glyph(h0, helmet.Mask, by + 4, "XXXXXXXXXXXXXX");
                }
                break;
        }

        // Accessory, in the same place as the outfit's headwear, in the middle of the head
        switch (f.Accessory)
        {
            // Halloween: a crooked witch's hat with an orange band, and a broom in the left hand
            // (put away while the hand holds the microphone or the pet is squashed flat)
            case Accessory.WitchHat:
                Glyph(c0 + 3, WitchColor, by - 5, "....X...", "...XX...", "..XXX...", "..XXXX..", "XXXXXXXX");
                Glyph(c0 + 3, BandOrange, by - 2, "..XXXX..");
                if (f.Arms != Arms.Mic && f.Squash == 0)
                {
                    for (int y = by + 1; y <= by + 6; y++) Set(c0 - 3, y, StickColor);
                    Glyph(c0 - 4, BindingColor, by + 7, "XXX");
                    Glyph(c0 - 4, StrawColor, by + 8, "XXX", "XXX", "X.X");
                }
                break;
            // Christmas: a red cap with a white brim, its tip and bobble flopping to the left
            case Accessory.SantaHat:
                Glyph(c0 + 2, SantaRed, by - 4, ".XX....", ".XXXX..", ".XXXXX.");
                Glyph(c0 + 2, FurWhite, by - 4, "X......", ".......", ".......", "XXXXXXX");
                break;
            // New Year's Eve and birthdays: a striped party cone leaning to the left, a golden pompom on its tip
            case Accessory.PartyHat:
                Glyph(c0 + 2, GoldColor, by - 5, "X");
                Glyph(c0 + 2, HeartColor, by - 4, ".X.....", "..X.X..", "..X.X.X", "....X.X");
                Glyph(c0 + 2, BallColor, by - 4, "..X....", ".X.X...", "...X.X.", "...X.X.");
                break;
            // Easter: two bunny ears with pink insides
            case Accessory.BunnyEars:
                Glyph(c0 + 4, FurWhite, by - 5, "XX.XX", "X..X.", "X..X.", "X..X.", "XX.XX");
                Glyph(c0 + 4, EarPink, by - 4, ".X..X", ".X..X", ".X..X");
                break;
            // Night: a long blue nightcap, its tip flopping over to the left and hanging down beside the head with
            // a white bobble
            case Accessory.Nightcap:
                Glyph(c0 - 1, NightcapBlue, by - 4, "..XXXXXX....", ".XX.XXXXXX..", "X...XXXXXXX.");
                Glyph(c0 - 1, NightcapShade, by - 1, "X..XXXXXXXXX"); // the gap keeps the hanging tip apart
                Set(c0 - 1, by, FurWhite);
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
            // "!" and "?" are not drawn here but in a thought cloud beside the head (RenderThought)
            // One Z drifting away from the head, then a short pause
            case Mark.Zzz1: Z(c0 + 11, 0); break;
            case Mark.Zzz2: Z(c0 + 13, 0); break;
            case Mark.Zzz3: break;
            // Emotes: hearts drifting up, a cookie on its way into the mouth, crumbs on the chin, a juggled ball
            case Mark.Heart1: Glyph(c0 + 11, HeartColor, 0, "XX.XX", "XXXXX", ".XXX.", "..X.."); break;
            case Mark.Heart2: Glyph(c0 + 13, HeartColor, 0, "XX.XX", "XXXXX", ".XXX.", "..X.."); break;
            case Mark.Cookie when f.Snack == Snack.Beans:
                // A handful of jelly beans in every flavour and color
                Glyph(c0 + 5, 0xFFE5484D, 0, "XX...");
                Glyph(c0 + 5, 0xFFF5C400, 1, "..XX.");
                Glyph(c0 + 5, 0xFF5FAE4E, 2, "XX...");
                Glyph(c0 + 5, 0xFF9A55B0, 2, "...XX");
                Glyph(c0 + 5, 0xFFE8862E, 3, ".XX..");
                break;
            case Mark.Cookie:
                Glyph(c0 + 5, CookieColor, 0, ".XXX.", "XXXXX", "XXXXX", ".XXX.");
                Glyph(c0 + 5, ChipColor, 0, ".....", ".X...", "...X.", ".....");
                break;
            case Mark.Crumbs when f.Snack == Snack.Beans:
                Set(c0 + 5, by + 7, 0xFFE5484D); Set(c0 + 9, by + 7, 0xFF5FAE4E); Set(c0 + 7, by + 8, 0xFFF5C400);
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

        // Rows of a fan scarf's pattern, a..c = its colors
        void Stripes(int x, int top, FanScarf scarf, params string[] rows)
        {
            for (int y = 0; y < rows.Length; y++)
                for (int i = 0; i < rows[y].Length; i++)
                    Set(x + i, top + y, rows[y][i] switch { 'a' => scarf.A, 'b' => scarf.B, _ => scarf.C });
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

    private static BitmapSource ToBitmap(uint[] px, int width = Width, int height = Height)
    {
        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), px, width * 4, 0);
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

    /// <summary>Size of the thought cloud's picture (<see cref="RenderThought"/>).</summary>
    public const int ThoughtWidth = 17, ThoughtHeight = 17;
    /// <summary>
    /// Where the thought cloud's picture starts, in the pet's grid: up and to the right of the head, beyond the
    /// pet's picture, with the little puffs at its bottom left just above the head's corner.
    /// </summary>
    public const int ThoughtX = 18, ThoughtY = -12;
    private static readonly Dictionary<(Mark Mark, bool Left), BitmapSource> ThoughtCache = new();

    /// <summary>
    /// A thought cloud with a "!" (<see cref="Mark.Exclaim"/>) or a "?" (<see cref="Mark.Question"/>) in it: bumpy,
    /// white with a dark outline, two little puffs trailing down to the head. <paramref name="left"/> mirrors it for
    /// the left of the head (the sign itself stays the right way round).
    /// </summary>
    public static BitmapSource RenderThought(Mark mark, bool left)
    {
        if (ThoughtCache.TryGetValue((mark, left), out var cached)) return cached;
        var px = new uint[ThoughtWidth * ThoughtHeight];
        void Set(int x, int y, uint c)
        {
            if (left) x = ThoughtWidth - 1 - x;
            if (x >= 0 && x < ThoughtWidth && y >= 0 && y < ThoughtHeight) px[y * ThoughtWidth + x] = c;
        }
        // The cloud: overlapping circles, outlined where they meet the outside
        (double X, double Y, double R)[] blobs =
            [(5.2, 3.6, 2.3), (8.6, 2.4, 2.4), (12.0, 3.4, 2.3), (3.6, 6.2, 2.2), (14.0, 6.2, 2.1), (6.4, 8.4, 2.1), (10.6, 8.6, 2.2), (8.8, 5.8, 3.4)];
        bool Inside(int x, int y) => blobs.Any(b => (x - b.X) * (x - b.X) + (y - b.Y) * (y - b.Y) <= b.R * b.R);
        for (int y = 0; y < ThoughtHeight; y++)
            for (int x = 0; x < ThoughtWidth; x++)
                if (Inside(x, y))
                    Set(x, y, Inside(x - 1, y) && Inside(x + 1, y) && Inside(x, y - 1) && Inside(x, y + 1) ? White : CloudOutline);
        // Two puffs trailing down and left to the head
        Set(2, 12, CloudOutline); Set(1, 13, CloudOutline); Set(3, 13, CloudOutline); Set(2, 14, CloudOutline); Set(2, 13, White);
        Set(0, 16, CloudOutline);
        // The sign in the middle of the cloud, not mirrored
        string[] sign = mark == Mark.Question ? [".XXX.", "XX.XX", "..XX.", ".....", "..X.."] : ["XX", "XX", "XX", "..", "XX"];
        uint color = mark == Mark.Question ? QuestionColor : AlertColor;
        int signLeft = 9 - (sign[0].Length + 1) / 2;
        if (left) signLeft = ThoughtWidth - signLeft - sign[0].Length;
        for (int y = 0; y < sign.Length; y++)
            for (int i = 0; i < sign[y].Length; i++)
                if (sign[y][i] == 'X') px[(3 + y) * ThoughtWidth + signLeft + i] = color;
        return ThoughtCache[(mark, left)] = ToBitmap(px, ThoughtWidth, ThoughtHeight);
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

    /// <summary>
    /// The icon of the app's windows in the taskbar: the smiling pet in its current color, cropped to the creature
    /// and scaled up with hard edges onto a square, like the tray icon.
    /// </summary>
    public static BitmapSource WindowIcon()
    {
        const int cropX = 2, cropY = 4, cropW = 18, cropH = 12, k = 3, size = cropW * k;
        var pixels = Pixels(new SpriteFrame(Eyes.Normal, Mouth.Smile));
        var px = new uint[size * size];
        int top = (size - cropH * k) / 2;
        for (int y = 0; y < cropH * k; y++)
            for (int x = 0; x < size; x++)
                px[(top + y) * size + x] = pixels[(cropY + y / k) * Width + cropX + x / k];
        return ToBitmap(px, size, size);
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

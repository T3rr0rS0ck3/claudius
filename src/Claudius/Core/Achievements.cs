using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Claudius.Pet;
using Claudius.Shared;

namespace Claudius.Core;

[JsonConverter(typeof(JsonStringEnumConverter<AchievementId>))]
public enum AchievementId
{
    FirstSession, Sessions10, Sessions100, Feed, Pat, Play, Tickle,
    NightOwl, BreakTaken, ReaperEscaped, LimitReached, ModelsTried, Celebration, FootballFan, HardLandings, Wahoo, OnceUponATime, EveryFlavour, TheForce, Konami, LostSock,
}

/// <summary>
/// An achievement and the costume item it unlocks (an outfit with one slot set). <paramref name="Counter"/> and
/// <paramref name="Target"/> are set for those reached by counting, e.g. 25 cookies.
/// </summary>
public sealed record Achievement(AchievementId Id, Outfit Reward, string? Counter = null, int Target = 1)
{
    /// <summary>
    /// Whether reaching it makes this costume item wearable: its reward, for the football fan every fan scarf, for
    /// the hard landings every football helmet, for Wahoo! every Super Mario costume, for Once upon a time every
    /// Disney costume, for Every flavour every Harry Potter costume, for May the Force every Star Wars costume and
    /// for the Konami Code every gaming item.
    /// The lost sock unlocks no costume but the sock as the pet.
    /// </summary>
    public bool Unlocks(Outfit item) => item != Outfit.None && item == Reward
        || Id == AchievementId.FootballFan && Sprite.IsFanScarf(item.Neck)
        || Id == AchievementId.HardLandings && Sprite.IsFootballHelmet(item.Hat)
        || Id == AchievementId.Wahoo && Sprite.IsMarioCostume(item)
        || Id == AchievementId.OnceUponATime && Sprite.IsDisneyCostume(item)
        || Id == AchievementId.EveryFlavour && Sprite.IsWizardingCostume(item)
        || Id == AchievementId.TheForce && Sprite.IsStarWarsCostume(item)
        || Id == AchievementId.Konami && Sprite.IsGamingItem(item);
}

/// <summary>
/// What the pet and its owner have done together, and which costumes that unlocked. Kept in achievements.json in
/// the data folder; nothing leaves the computer. Costumes without an achievement are available from the start.
/// </summary>
public sealed class Achievements
{
    public const string SessionsCounter = "Sessions";
    public const int ModelsNeeded = 3;
    public const int EmotesNeeded = 25;
    /// <summary>Games of ball for all fan scarves.</summary>
    public const int BallGamesNeeded = 1000;
    /// <summary>Counts the long falls that end in a splat.</summary>
    public const string FallsCounter = "Falls";
    /// <summary>Long falls for all football helmets.</summary>
    public const int FallsNeeded = 100;
    /// <summary>Counts the pet's jumps and hops.</summary>
    public const string JumpsCounter = "Jumps";
    /// <summary>Jumps for all Super Mario costumes.</summary>
    public const int JumpsNeeded = 500;
    /// <summary>Pats for all Disney costumes.</summary>
    public const int PatsNeeded = 100;
    /// <summary>Counts the feedings while the beans are the snack.</summary>
    public const string BeansCounter = "Beans";
    /// <summary>Feedings with beans for all Harry Potter costumes.</summary>
    public const int BeansNeeded = 100;
    /// <summary>Counts the times the pet is picked up and moved with the mouse.</summary>
    public const string DragsCounter = "Drags";
    /// <summary>Moves with the mouse for all Star Wars costumes.</summary>
    public const int DragsNeeded = 100;

    public static readonly Achievement[] All =
    [
        new(AchievementId.FirstSession, new(Hat.Headphones), SessionsCounter, 1),
        new(AchievementId.Sessions10, new(Hat.HardHat), SessionsCounter, 10),
        new(AchievementId.Sessions100, new(Hat.VikingHelmet), SessionsCounter, 100),
        new(AchievementId.Feed, new(Hat.ChefHat), nameof(Emote.Feed), EmotesNeeded),
        new(AchievementId.Pat, new(Neck: Neckwear.Bell), nameof(Emote.Pat), EmotesNeeded),
        new(AchievementId.Play, new(Hat.PropellerCap), nameof(Emote.Play), EmotesNeeded),
        new(AchievementId.FootballFan, new(Neck: Neckwear.Dortmund), nameof(Emote.Play), BallGamesNeeded),
        new(AchievementId.Tickle, new(Face: FaceWear.Moustache), nameof(Emote.Tickle), EmotesNeeded),
        new(AchievementId.NightOwl, new(Neck: Neckwear.Scarf)),
        new(AchievementId.BreakTaken, new(Hat.CowboyHat)),
        new(AchievementId.ReaperEscaped, new(Hat.Halo)),
        new(AchievementId.LimitReached, new(Face: FaceWear.EyePatch)),
        new(AchievementId.ModelsTried, new(Face: FaceWear.Monocle), Target: ModelsNeeded),
        new(AchievementId.Celebration, new(Hat.TopHat)),
        new(AchievementId.HardLandings, new(Hat.Packers), FallsCounter, FallsNeeded),
        new(AchievementId.Wahoo, new(Hat.Mario), JumpsCounter, JumpsNeeded),
        new(AchievementId.OnceUponATime, new(Hat.MickeyEars), nameof(Emote.Pat), PatsNeeded),
        new(AchievementId.EveryFlavour, new(Hat.SortingHat), BeansCounter, BeansNeeded),
        new(AchievementId.TheForce, new(Face: FaceWear.DarthVader), DragsCounter, DragsNeeded),
        new(AchievementId.Konami, new(Hand: HeldItem.SnesController)),
        // Reached with all the others
        new(AchievementId.LostSock, Outfit.None, Target: Enum.GetValues<AchievementId>().Length - 1),
    ];

    /// <summary>How many session ids are remembered so that each session is counted once.</summary>
    private const int RememberedSessions = 200;

    private static string FilePath => Path.Combine(DataPaths.DataDir, "achievements.json");

    private Data _data = new();

    /// <summary>Raised once per achievement, when it is reached.</summary>
    public event Action<Achievement>? Unlocked;

    public static Achievements Load()
    {
        var achievements = new Achievements();
        try
        {
            if (File.Exists(FilePath))
                achievements._data = JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath), JsonOptions) ?? new();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Log.Write("Erfolge konnten nicht gelesen werden: " + ex.Message);
        }
        achievements._data.Counters ??= [];
        achievements._data.Unlocked ??= [];
        achievements._data.SeenSessions ??= [];
        achievements._data.Models ??= [];
        return achievements;
    }

    public bool IsUnlocked(AchievementId id) => _data.Unlocked.ContainsKey(id);

    public DateTime? UnlockedAt(AchievementId id) => _data.Unlocked.TryGetValue(id, out var at) ? at : null;

    /// <summary>How far along an achievement is, capped at its target.</summary>
    public int Progress(Achievement achievement)
    {
        if (IsUnlocked(achievement.Id)) return achievement.Target;
        int value = achievement.Id == AchievementId.ModelsTried ? _data.Models.Count
            : achievement.Id == AchievementId.LostSock ? _data.Unlocked.Keys.Count(id => id != AchievementId.LostSock)
            : achievement.Counter != null ? _data.Counters.GetValueOrDefault(achievement.Counter) : 0;
        return Math.Min(value, achievement.Target);
    }

    /// <summary>The achievement that unlocks this costume item, while it is still locked; null when it can be worn.</summary>
    public Achievement? LockedBy(Outfit item) =>
        All.FirstOrDefault(a => a.Unlocks(item) && !IsUnlocked(a.Id));

    /// <summary>A session took place; each one is counted once, however often it reports.</summary>
    public void SessionSeen(string id)
    {
        if (_data.SeenSessions.Contains(id)) return;
        _data.SeenSessions.Add(id);
        if (_data.SeenSessions.Count > RememberedSessions) _data.SeenSessions.RemoveAt(0);
        Count(SessionsCounter);
    }

    public void ModelSeen(ModelFamily family)
    {
        if (family == ModelFamily.Other || _data.Models.Contains(family)) return;
        _data.Models.Add(family);
        if (_data.Models.Count >= ModelsNeeded) Unlock(AchievementId.ModelsTried, save: false);
        Save();
    }

    public void Count(string counter)
    {
        int value = _data.Counters.GetValueOrDefault(counter) + 1;
        _data.Counters[counter] = value;
        foreach (var achievement in All.Where(a => a.Counter == counter && value >= a.Target))
            Unlock(achievement.Id, save: false);
        Save();
    }

    public void Unlock(AchievementId id) => Unlock(id, save: true);

    private void Unlock(AchievementId id, bool save)
    {
        if (IsUnlocked(id)) return;
        _data.Unlocked[id] = DateTime.Now;
        if (save) Save();
        Unlocked?.Invoke(All.First(a => a.Id == id));
        if (All.All(a => a.Id == AchievementId.LostSock || IsUnlocked(a.Id))) Unlock(AchievementId.LostSock, save);
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(DataPaths.DataDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_data, JsonOptions), new UTF8Encoding(false));
        }
        catch (IOException ex)
        {
            Log.Write("Erfolge konnten nicht gespeichert werden: " + ex.Message);
        }
    }

    private sealed class Data
    {
        public Dictionary<string, int> Counters { get; set; } = [];
        public Dictionary<AchievementId, DateTime> Unlocked { get; set; } = [];
        public List<string> SeenSessions { get; set; } = [];
        public List<ModelFamily> Models { get; set; } = [];
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}

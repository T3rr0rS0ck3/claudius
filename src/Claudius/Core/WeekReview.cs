using Claudius.Shared;

namespace Claudius.Core;

/// <summary>How the last finished week went: its final usage and the day most of it was used on.</summary>
public sealed record WeekReview(long ResetsAt, double Percent, DateTime? StrongestDay)
{
    public DateTimeOffset ResetsAtTime => DateTimeOffset.FromUnixTimeSeconds(ResetsAt);

    /// <summary>
    /// The most recent week window whose reset has passed, from the history; null if the history holds none.
    /// The strongest day is the local day the weekly usage rose the most on.
    /// </summary>
    public static WeekReview? Last(IReadOnlyList<HistorySample> history, DateTimeOffset now)
    {
        long nowSeconds = now.ToUnixTimeSeconds();
        long? reset = null;
        foreach (var h in history)
        {
            if (h.Week == null || h.WeekReset is not { } r || r > nowSeconds) continue;
            if (reset == null || r > reset + 600) reset = r;
        }
        if (reset == null) return null;

        // Same window, as in the forecast: resets_at may jitter slightly between responses.
        var points = history
            .Where(h => h.Week != null && h.WeekReset is { } r && Math.Abs(r - reset.Value) <= 600)
            .OrderBy(h => h.Time)
            .Select(h => (Day: DateTimeOffset.FromUnixTimeSeconds(h.Time).LocalDateTime.Date, Percent: h.Week!.Value))
            .ToList();

        // What the first sample shows was used since the window started; count it for that sample's day.
        var perDay = new Dictionary<DateTime, double>();
        double previous = 0;
        foreach (var (day, percent) in points)
        {
            if (percent > previous) perDay[day] = perDay.GetValueOrDefault(day) + percent - previous;
            previous = Math.Max(previous, percent);
        }
        DateTime? strongest = perDay.Count == 0 ? null : perDay.MaxBy(d => d.Value).Key;
        return new WeekReview(reset.Value, Math.Clamp(previous, 0, 100), strongest);
    }
}

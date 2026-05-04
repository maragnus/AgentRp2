namespace AgentRp.Services;

public static class RelativeDateFormatter
{
    public static string FormatDate(DateTime utcDate, DateTime? now = null)
    {
        if (utcDate == default)
            return "";

        var current = (now ?? DateTime.UtcNow).Date;
        var target = utcDate.Date;
        var deltaDays = (target - current).Days;
        var label = target.ToString("MMM d, yyyy");
        return deltaDays switch
        {
            0 => $"{label} (today)",
            -1 => $"{label} (yesterday)",
            1 => $"{label} (tomorrow)",
            < 0 => $"{label} ({Math.Abs(deltaDays)} days ago)",
            _ => $"{label} (in {deltaDays} days)"
        };
    }

    public static string FormatDuration(double durationSeconds) =>
        durationSeconds <= 0
            ? ""
            : durationSeconds < 1
                ? $"{durationSeconds * 1000:0}ms"
                : $"{durationSeconds:0.0}s";
}

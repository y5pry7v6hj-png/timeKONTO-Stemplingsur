using System.Globalization;
using System.Text;
using Stemplingsur.Models;

namespace Stemplingsur.Services;

public sealed class CalendarService
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly Dictionary<string, CacheItem> _cache = new(StringComparer.Ordinal);
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public CalendarService(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsForDateAsync(
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var urls = GetConfiguredCalendarUrls();
        if (urls.Count == 0)
            return Array.Empty<CalendarEvent>();

        var workDate = date.Date;
        var nextDate = workDate.AddDays(1);
        var cutoff = ApiSettings.GetCalendarWorkdayCutoff();
        var result = new List<CalendarEvent>();

        foreach (var url in urls)
        {
            var ics = await GetCalendarTextAsync(url, forceRefresh: false, cancellationToken: cancellationToken);

            // Hendelser på valgt arbeidsdato.
            result.AddRange(ParseEvents(ics, workDate));

            // Natt til neste kalenderdato hører til forrige arbeidsdag.
            // Heldagshendelser flyttes aldri bakover.
            if (cutoff > TimeSpan.Zero)
            {
                result.AddRange(ParseEvents(ics, nextDate)
                    .Where(e => !e.IsAllDay &&
                                e.Start.Date == nextDate &&
                                e.Start.TimeOfDay < cutoff));
            }
        }

        return result
            .GroupBy(e => new
            {
                e.Summary,
                e.Start,
                e.End,
                e.IsAllDay,
                e.Location,
                e.Description
            })
            .Select(g => g.First())
            .OrderBy(e => e.IsAllDay ? DateTime.MinValue : e.Start)
            .ThenBy(e => e.Summary, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task TestAsync(CancellationToken cancellationToken = default)
    {
        var urls = GetConfiguredCalendarUrls();
        if (urls.Count == 0)
            throw new InvalidOperationException("WebCal/ICS-adressen er tom.");

        for (var i = 0; i < urls.Count; i++)
        {
            try
            {
                await GetCalendarTextAsync(urls[i], forceRefresh: true, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Kalender {i + 1} kunne ikke leses: {ex.Message}", ex);
            }
        }
    }

    public static IReadOnlyList<string> GetConfiguredCalendarUrls()
    {
        var configured = ApiSettings.CalendarUrl;
        if (string.IsNullOrWhiteSpace(configured))
            return Array.Empty<string>();

        return configured
            .Replace("\r", "\n")
            .Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(NormalizeCalendarUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<string> GetCalendarTextAsync(
        string url,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (!forceRefresh &&
            _cache.TryGetValue(url, out var cached) &&
            now < cached.ExpiresAt)
            return cached.Content;

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (!forceRefresh &&
                _cache.TryGetValue(url, out cached) &&
                now < cached.ExpiresAt)
                return cached.Content;

            using var response = await _http.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!content.Contains("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Adressen svarte, men innholdet ser ikke ut som en ICS/WebCal-kalender.");

            _cache[url] = new CacheItem(content, DateTimeOffset.UtcNow.Add(CacheDuration));
            return content;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private static string NormalizeCalendarUrl(string value)
    {
        var url = value.Trim();

        if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url["webcal://".Length..];
        else if (url.StartsWith("webcals://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url["webcals://".Length..];

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidOperationException("WebCal-adressen må være en gyldig webcal://, http:// eller https:// adresse.");

        return uri.ToString();
    }

    private static IEnumerable<CalendarEvent> ParseEvents(string ics, DateTime targetDate)
    {
        var unfolded = UnfoldLines(ics);
        Dictionary<string, List<IcsProperty>>? current = null;

        foreach (var rawLine in unfolded)
        {
            var line = rawLine.TrimEnd();

            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                current = new(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (current is not null)
                {
                    var item = BuildEvent(current, targetDate);
                    if (item is not null)
                        yield return item;
                }

                current = null;
                continue;
            }

            if (current is null)
                continue;

            var colon = FindValueSeparator(line);
            if (colon <= 0)
                continue;

            var left = line[..colon];
            var value = line[(colon + 1)..];

            var parts = left.Split(';');
            var name = parts[0].Trim();
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 1; i < parts.Length; i++)
            {
                var eq = parts[i].IndexOf('=');
                if (eq > 0)
                    parameters[parts[i][..eq]] = parts[i][(eq + 1)..].Trim('"');
            }

            if (!current.TryGetValue(name, out var values))
            {
                values = [];
                current[name] = values;
            }

            values.Add(new IcsProperty(value, parameters));
        }
    }

    private static CalendarEvent? BuildEvent(
        Dictionary<string, List<IcsProperty>> props,
        DateTime targetDate)
    {
        var startProp = First(props, "DTSTART");
        if (startProp is null)
            return null;

        if (!TryParseDate(startProp, out var start, out var isAllDay))
            return null;

        DateTime? end = null;
        var endProp = First(props, "DTEND");
        if (endProp is not null && TryParseDate(endProp, out var parsedEnd, out _))
            end = parsedEnd;

        if (IsExcluded(props, targetDate))
            return null;

        var rrule = First(props, "RRULE")?.Value;
        if (!OccursOnDate(start, end, isAllDay, rrule, targetDate))
            return null;

        var effectiveStart = GetOccurrenceStart(start, rrule, targetDate);

        DateTime? effectiveEnd = null;
        if (end is not null)
        {
            var duration = end.Value - start;
            effectiveEnd = effectiveStart + duration;
        }

        var summary = DecodeText(First(props, "SUMMARY")?.Value ?? "Kalenderhendelse");
        var location = DecodeText(First(props, "LOCATION")?.Value ?? string.Empty);
        var description = DecodeText(First(props, "DESCRIPTION")?.Value ?? string.Empty);

        return new CalendarEvent(
            summary,
            effectiveStart,
            effectiveEnd,
            isAllDay,
            location,
            description);
    }

    private static bool OccursOnDate(
        DateTime start,
        DateTime? end,
        bool isAllDay,
        string? rrule,
        DateTime targetDate)
    {
        if (string.IsNullOrWhiteSpace(rrule))
        {
            if (isAllDay)
            {
                var exclusiveEnd = end?.Date ?? start.Date.AddDays(1);
                return targetDate >= start.Date && targetDate < exclusiveEnd;
            }

            var actualEnd = end ?? start;
            return targetDate >= start.Date && targetDate <= actualEnd.Date;
        }

        if (targetDate < start.Date)
            return false;

        var rule = ParseRule(rrule);

        if (rule.TryGetValue("UNTIL", out var untilRaw) &&
            TryParseRuleDate(untilRaw, out var until) &&
            targetDate > until.Date)
            return false;

        var interval = 1;
        if (rule.TryGetValue("INTERVAL", out var intervalRaw))
            int.TryParse(intervalRaw, out interval);
        interval = Math.Max(1, interval);

        if (!rule.TryGetValue("FREQ", out var freq))
            return targetDate == start.Date;

        switch (freq.ToUpperInvariant())
        {
            case "DAILY":
                return (targetDate - start.Date).Days % interval == 0;

            case "WEEKLY":
            {
                var days = rule.TryGetValue("BYDAY", out var byDay)
                    ? byDay.Split(',').Select(ParseDayOfWeek).Where(d => d is not null).Select(d => d!.Value).ToHashSet()
                    : new HashSet<DayOfWeek> { start.DayOfWeek };

                var weekDiff = (int)((targetDate - StartOfWeek(start.Date)).TotalDays / 7);
                return weekDiff >= 0 && weekDiff % interval == 0 && days.Contains(targetDate.DayOfWeek);
            }

            case "MONTHLY":
            {
                var monthDiff = (targetDate.Year - start.Year) * 12 + targetDate.Month - start.Month;
                if (monthDiff < 0 || monthDiff % interval != 0)
                    return false;

                if (rule.TryGetValue("BYMONTHDAY", out var byMonthDay))
                {
                    return byMonthDay.Split(',')
                        .Select(v => int.TryParse(v, out var day) ? day : -1)
                        .Contains(targetDate.Day);
                }

                return targetDate.Day == start.Day;
            }

            case "YEARLY":
            {
                var yearDiff = targetDate.Year - start.Year;
                if (yearDiff < 0 || yearDiff % interval != 0)
                    return false;

                var month = start.Month;
                var day = start.Day;

                if (rule.TryGetValue("BYMONTH", out var byMonth) && int.TryParse(byMonth.Split(',')[0], out var parsedMonth))
                    month = parsedMonth;
                if (rule.TryGetValue("BYMONTHDAY", out var byMonthDay) && int.TryParse(byMonthDay.Split(',')[0], out var parsedDay))
                    day = parsedDay;

                return targetDate.Month == month && targetDate.Day == day;
            }

            default:
                return targetDate == start.Date;
        }
    }

    private static DateTime GetOccurrenceStart(DateTime originalStart, string? rrule, DateTime targetDate)
    {
        if (string.IsNullOrWhiteSpace(rrule) || originalStart.Date == targetDate)
            return originalStart;

        return targetDate.Date + originalStart.TimeOfDay;
    }

    private static bool IsExcluded(Dictionary<string, List<IcsProperty>> props, DateTime targetDate)
    {
        if (!props.TryGetValue("EXDATE", out var excluded))
            return false;

        foreach (var prop in excluded)
        {
            foreach (var value in prop.Value.Split(','))
            {
                if (TryParseDate(new IcsProperty(value, prop.Parameters), out var date, out _) &&
                    date.Date == targetDate)
                    return true;
            }
        }

        return false;
    }

    private static Dictionary<string, string> ParseRule(string value)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in value.Split(';'))
        {
            var eq = item.IndexOf('=');
            if (eq > 0)
                result[item[..eq]] = item[(eq + 1)..];
        }
        return result;
    }

    private static DayOfWeek? ParseDayOfWeek(string value)
    {
        var token = value.Trim();
        if (token.Length > 2)
            token = token[^2..];

        return token.ToUpperInvariant() switch
        {
            "MO" => DayOfWeek.Monday,
            "TU" => DayOfWeek.Tuesday,
            "WE" => DayOfWeek.Wednesday,
            "TH" => DayOfWeek.Thursday,
            "FR" => DayOfWeek.Friday,
            "SA" => DayOfWeek.Saturday,
            "SU" => DayOfWeek.Sunday,
            _ => null
        };
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = ((7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7);
        return date.AddDays(-diff).Date;
    }

    private static bool TryParseRuleDate(string value, out DateTime result)
    {
        var prop = new IcsProperty(value, new Dictionary<string, string>());
        return TryParseDate(prop, out result, out _);
    }

    private static bool TryParseDate(IcsProperty prop, out DateTime result, out bool isAllDay)
    {
        result = default;
        isAllDay = prop.Parameters.TryGetValue("VALUE", out var valueType) &&
                   valueType.Equals("DATE", StringComparison.OrdinalIgnoreCase);

        var raw = prop.Value.Trim();

        if (isAllDay || raw.Length == 8)
        {
            isAllDay = true;
            return DateTime.TryParseExact(
                raw[..Math.Min(8, raw.Length)],
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result);
        }

        string[] formats =
        [
            "yyyyMMdd'T'HHmmss'Z'",
            "yyyyMMdd'T'HHmm'Z'",
            "yyyyMMdd'T'HHmmss",
            "yyyyMMdd'T'HHmm"
        ];

        if (!DateTime.TryParseExact(
                raw,
                formats,
                CultureInfo.InvariantCulture,
                raw.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
                    ? DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
                    : DateTimeStyles.None,
                out var parsed))
            return false;

        if (raw.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
            parsed = parsed.ToLocalTime();

        result = parsed;
        return true;
    }

    private static IcsProperty? First(Dictionary<string, List<IcsProperty>> props, string name) =>
        props.TryGetValue(name, out var values) ? values.FirstOrDefault() : null;

    private static IEnumerable<string> UnfoldLines(string text)
    {
        string? current = null;

        foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && current is not null)
            {
                current += line[1..];
                continue;
            }

            if (current is not null)
                yield return current;

            current = line;
        }

        if (current is not null)
            yield return current;
    }

    private static int FindValueSeparator(string line)
    {
        var escaped = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '\\')
            {
                escaped = !escaped;
                continue;
            }

            if (line[i] == ':' && !escaped)
                return i;

            escaped = false;
        }

        return -1;
    }

    private static string DecodeText(string value) =>
        value
            .Replace("\\n", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("\\,", ",")
            .Replace("\\;", ";")
            .Replace("\\\\", "\\");

    private sealed record IcsProperty(
        string Value,
        IReadOnlyDictionary<string, string> Parameters);
    private sealed record CacheItem(string Content, DateTimeOffset ExpiresAt);
}

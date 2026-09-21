namespace Stemplingsur.Services;

public static class ApiSettings
{
    public const string DefaultBaseUrl = "https://admin.pts.bar/api/index.php";
    public const string DefaultAdminPin = "2020";

    private const string ApiKeySecureName = "Stemplingsur.ApiKey";
    private const string ApiKeyFallbackName = "Stemplingsur.ApiKey.Fallback";

    public static string BaseUrl
    {
        get => Preferences.Default.Get(nameof(BaseUrl), DefaultBaseUrl);
        set => Preferences.Default.Set(nameof(BaseUrl), (value ?? string.Empty).Trim());
    }

    public static async Task<string> GetApiKeyAsync()
    {
        try
        {
            var secureValue = await SecureStorage.Default.GetAsync(ApiKeySecureName);
            if (!string.IsNullOrWhiteSpace(secureValue))
                return secureValue;
        }
        catch
        {
            // Mac Catalyst kan mangle Keychain-entitlement i utviklingsbuild.
        }

        return Preferences.Default.Get(ApiKeyFallbackName, string.Empty);
    }

    public static async Task<bool> SetApiKeyAsync(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();

        // Fallback gjør at utviklingsbuild og plattformer uten tilgjengelig
        // SecureStorage fortsatt kan brukes. SecureStorage forsøkes alltid først.
        Preferences.Default.Set(ApiKeyFallbackName, trimmed);

        try
        {
            await SecureStorage.Default.SetAsync(ApiKeySecureName, trimmed);
            return true;
        }
        catch
        {
            return false;
        }
    }


    public static string CalendarUrl
    {
        get => Preferences.Default.Get(nameof(CalendarUrl), string.Empty);
        set => Preferences.Default.Set(nameof(CalendarUrl), (value ?? string.Empty).Trim());
    }

    public static string CalendarWorkdayCutoff
    {
        get => Preferences.Default.Get(nameof(CalendarWorkdayCutoff), "06:00");
        set => Preferences.Default.Set(nameof(CalendarWorkdayCutoff), (value ?? string.Empty).Trim());
    }

    public static TimeSpan GetCalendarWorkdayCutoff()
    {
        return TimeSpan.TryParseExact(CalendarWorkdayCutoff, @"hh\:mm", null, out var parsed) &&
               parsed >= TimeSpan.Zero && parsed < TimeSpan.FromDays(1)
            ? parsed
            : TimeSpan.FromHours(6);
    }

    public static string QuickTime1
    {
        get => Preferences.Default.Get(nameof(QuickTime1), "18:45");
        set => Preferences.Default.Set(nameof(QuickTime1), (value ?? string.Empty).Trim());
    }

    public static string QuickTime2
    {
        get => Preferences.Default.Get(nameof(QuickTime2), "19:00");
        set => Preferences.Default.Set(nameof(QuickTime2), (value ?? string.Empty).Trim());
    }

    public static string QuickTime3
    {
        get => Preferences.Default.Get(nameof(QuickTime3), "21:00");
        set => Preferences.Default.Set(nameof(QuickTime3), (value ?? string.Empty).Trim());
    }

    public static string QuickTime4
    {
        get => Preferences.Default.Get(nameof(QuickTime4), "22:00");
        set => Preferences.Default.Set(nameof(QuickTime4), (value ?? string.Empty).Trim());
    }


    public static string QuickOutTime1
    {
        get => Preferences.Default.Get(nameof(QuickOutTime1), "19:00");
        set => Preferences.Default.Set(nameof(QuickOutTime1), (value ?? string.Empty).Trim());
    }

    public static string QuickOutTime2
    {
        get => Preferences.Default.Get(nameof(QuickOutTime2), "02:30");
        set => Preferences.Default.Set(nameof(QuickOutTime2), (value ?? string.Empty).Trim());
    }

    public static string QuickOutTime3
    {
        get => Preferences.Default.Get(nameof(QuickOutTime3), "02:45");
        set => Preferences.Default.Set(nameof(QuickOutTime3), (value ?? string.Empty).Trim());
    }

    public static string QuickOutTime4
    {
        get => Preferences.Default.Get(nameof(QuickOutTime4), "03:00");
        set => Preferences.Default.Set(nameof(QuickOutTime4), (value ?? string.Empty).Trim());
    }

    public static bool QuickTime1Visible => !string.IsNullOrWhiteSpace(QuickTime1);
    public static bool QuickTime2Visible => !string.IsNullOrWhiteSpace(QuickTime2);
    public static bool QuickTime3Visible => !string.IsNullOrWhiteSpace(QuickTime3);
    public static bool QuickTime4Visible => !string.IsNullOrWhiteSpace(QuickTime4);

    public static bool QuickOutTime1Visible => !string.IsNullOrWhiteSpace(QuickOutTime1);
    public static bool QuickOutTime2Visible => !string.IsNullOrWhiteSpace(QuickOutTime2);
    public static bool QuickOutTime3Visible => !string.IsNullOrWhiteSpace(QuickOutTime3);
    public static bool QuickOutTime4Visible => !string.IsNullOrWhiteSpace(QuickOutTime4);

    public static string AdminPin
    {
        get => Preferences.Default.Get(nameof(AdminPin), DefaultAdminPin);
        set => Preferences.Default.Set(nameof(AdminPin), (value ?? string.Empty).Trim());
    }
}

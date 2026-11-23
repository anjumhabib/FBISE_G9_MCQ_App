using McqExam.Domain;

namespace McqExam.Application;

public static class ClientDevice
{
    public static DeviceType Classify(string? agent)
    {
        if (string.IsNullOrWhiteSpace(agent)) return DeviceType.Unknown;

        bool Has(string value) => agent.Contains(value, StringComparison.OrdinalIgnoreCase);
        if (Has("bot") || Has("spider") || Has("crawler")) return DeviceType.Bot;
        if (Has("iPad") || Has("Tablet") || (Has("Android") && !Has("Mobile"))) return DeviceType.Tablet;
        if (Has("Mobile") || Has("iPhone") || Has("iPod") || Has("Windows Phone")) return DeviceType.Mobile;
        if (Has("Windows") || Has("Macintosh") || Has("Linux") || Has("CrOS")) return DeviceType.Desktop;
        return DeviceType.Unknown;
    }

    public static string Browser(string? agent)
    {
        bool Has(string value) => agent?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
        return Has("Edg/") || Has("EdgA/") || Has("EdgiOS/") ? "Edge" :
            Has("OPR/") || Has("Opera") ? "Opera" :
            Has("Firefox/") || Has("FxiOS/") ? "Firefox" :
            Has("SamsungBrowser/") ? "Samsung Internet" :
            Has("Chrome/") || Has("CriOS/") ? "Chrome" :
            Has("Safari/") ? "Safari" : "Unknown";
    }
}

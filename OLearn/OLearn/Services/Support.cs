using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace OLearn.Services;

public class TestModeOptions
{
    public bool Enabled { get; set; }
    public int DefaultLeaseMinutes { get; set; }
}

/// <summary>Settings an administrator can change while the app runs (test mode only).</summary>
public class RuntimeSettings
{
    public RuntimeSettings(IOptions<TestModeOptions> o)
    {
        TestMode = o.Value.Enabled;
        TestLeaseMinutes = o.Value.Enabled ? o.Value.DefaultLeaseMinutes : 0;
    }
    public bool TestMode { get; }
    /// <summary>When above 0 and test mode is on, new leases last this many minutes so that expiry can be observed.</summary>
    public int TestLeaseMinutes { get; set; }
}

public static class Fmt
{
    /// <summary>UTC ISO 8601 with milliseconds. JavaScript parses it and produces the same shape.</summary>
    public static string Iso(DateTime d) =>
        DateTime.SpecifyKind(d, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public static DateTime ParseUtc(string? s, DateTime fallback) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : fallback;
}

public static class PasswordService
{
    // PBKDF2-SHA256, 100,000 iterations. Stored as iterations.salt.hash (base64).
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"100000.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations)) return false;
        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

using System.Security.Cryptography;
using System.Text;

namespace OLearn.Services;

/// <summary>
/// Signs leases and derives file keys. The private signing key and the master secret stay on the server
/// (App_Data/keys) and are never sent to a device (NFR-08). The browser embeds only the public key,
/// which this service writes to wwwroot/js/lease-public-key.js.
/// </summary>
public sealed class LeaseCryptoService : IDisposable
{
    private readonly ECDsa _ecdsa;
    private readonly byte[] _master;
    private readonly object _lock = new();

    public string PublicKeyBase64 { get; }

    public LeaseCryptoService(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data", "keys");
        Directory.CreateDirectory(dir);
        var signingFile = Path.Combine(dir, "lease-signing.pkcs8");
        var masterFile = Path.Combine(dir, "file-key-master.bin");

        _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        if (File.Exists(signingFile))
            _ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(File.ReadAllText(signingFile)), out _);
        else
            File.WriteAllText(signingFile, Convert.ToBase64String(_ecdsa.ExportPkcs8PrivateKey()));

        if (File.Exists(masterFile)) _master = Convert.FromBase64String(File.ReadAllText(masterFile));
        else
        {
            _master = RandomNumberGenerator.GetBytes(32);
            File.WriteAllText(masterFile, Convert.ToBase64String(_master));
        }

        PublicKeyBase64 = Convert.ToBase64String(_ecdsa.ExportSubjectPublicKeyInfo());

        var jsDir = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "js");
        Directory.CreateDirectory(jsDir);
        File.WriteAllText(Path.Combine(jsDir, "lease-public-key.js"),
            "// Generated at start-up from the server signing key. Contains the PUBLIC key only.\n" +
            $"globalThis.OLEARN_PUBLIC_KEY = \"{PublicKeyBase64}\";\n");
    }

    /// <summary>ECDSA P-256 over SHA-256. SignData returns IEEE P1363 (r||s), which is what Web Crypto verifies.</summary>
    public string Sign(string payload)
    {
        lock (_lock)
        {
            return Convert.ToBase64String(_ecdsa.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256));
        }
    }

    /// <summary>The same key is derived again when a lease is renewed, so the file need not be downloaded again.</summary>
    public byte[] DeriveFileKey(int userId, int resourceVersionId) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, _master, 32, null,
            Encoding.UTF8.GetBytes($"olearn-file-key|{userId}|{resourceVersionId}"));

    public void Dispose() => _ecdsa.Dispose();
}

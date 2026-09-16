using System.IO;
using System.IO.Abstractions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LogGrep.Services;

/// <summary>
/// Where the app keeps what it is told. Everything lives in one folder under the user's roaming
/// profile: the settings, and later the rules file the app builds from Blizzard's journal, so that
/// a person who wants to look at either knows one place to go.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    private readonly IFileSystem _fileSystem;

    public SettingsService() : this(new FileSystem())
    {
    }

    public SettingsService(IFileSystem fileSystem, string? dataDirectory = null)
    {
        _fileSystem = fileSystem;
        DataDirectory = dataDirectory ?? _fileSystem.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LogGrep");
    }

    public string DataDirectory { get; }

    public string SettingsPath => _fileSystem.Path.Combine(DataDirectory, "settings.json");

    /// <summary>Where the rules built from the journal will be written. Nothing writes it yet.</summary>
    public string RulesPath => _fileSystem.Path.Combine(DataDirectory, "rules.txt");

    /// <summary>The journal as it arrived, kept so the rules can be rebuilt without asking again.</summary>
    public string JournalFolder => _fileSystem.Path.Combine(DataDirectory, "journal");

    /// <summary>A missing or unreadable file reads as empty settings rather than as a failure.</summary>
    public AppSettings Load()
    {
        try
        {
            if (!_fileSystem.File.Exists(SettingsPath)) return new AppSettings();

            string json = _fileSystem.File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        _fileSystem.Directory.CreateDirectory(DataDirectory);
        _fileSystem.File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, Format));
    }

    /// <summary>
    /// Encrypts the secret for this Windows account, so the file is useless if it is copied to
    /// another machine or read by another user. It costs a few lines and the thing being protected
    /// is somebody's account rather than a preference.
    /// </summary>
    public static string Protect(string secret)
    {
        if (secret.Length == 0) return string.Empty;

        byte[] sealed_ = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(secret), null, DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(sealed_);
    }

    /// <summary>Empty when there is nothing stored, or when the file came from another account.</summary>
    public static string Unprotect(string protectedSecret)
    {
        if (protectedSecret.Length == 0) return string.Empty;

        try
        {
            byte[] opened = ProtectedData.Unprotect(
                Convert.FromBase64String(protectedSecret), null, DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(opened);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return string.Empty;
        }
    }
}

using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using LogGrep.Services;

namespace LogGrep.ViewModels;

/// <summary>
/// The settings dialog. The secret never lands in a bound property: the password box hands it over
/// once, on save, and what is kept afterwards is only the protected form.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly IFileSystem _fileSystem;
    private readonly AppSettings _current;
    private string _clientId;
    private string _region;
    private bool _isBusy;
    private string _status = string.Empty;

    public SettingsViewModel() : this(new SettingsService(), new FileSystem())
    {
    }

    public SettingsViewModel(SettingsService settings, IFileSystem? fileSystem = null)
    {
        _settings = settings;
        _fileSystem = fileSystem ?? new FileSystem();
        _current = settings.Load();
        _clientId = _current.ClientId;
        _region = _current.Region;

        OpenFolderCommand = new RelayCommand(OpenFolder);
    }

    public RelayCommand OpenFolderCommand { get; }

    /// <summary>Client id of a Blizzard API client from develop.battle.net. Not a secret.</summary>
    public string ClientId
    {
        get => _clientId;
        set => Set(ref _clientId, value);
    }

    public string DataDirectory => _settings.DataDirectory;

    public string SettingsPath => _settings.SettingsPath;

    public string RulesPath => _settings.RulesPath;

    /// <summary>Whether a secret is already stored, which is all the dialog will ever say about it.</summary>
    public string SecretState => _current.ProtectedClientSecret.Length > 0
        ? "A secret is stored. Leave the box empty to keep it."
        : "No secret stored yet.";

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    /// <summary>
    /// An empty box leaves whatever is already stored alone, so somebody correcting a typo in the
    /// client id does not have to go and find their secret again.
    /// </summary>
    public bool Save(string secret)
    {
        _current.ClientId = ClientId.Trim();
        _current.Region = Region.Trim().ToLowerInvariant();
        if (secret.Length > 0) _current.ProtectedClientSecret = SettingsService.Protect(secret);

        try
        {
            _settings.Save(_current);
        }
        catch (Exception ex)
        {
            Status = "Could not save: " + ex.Message;
            return false;
        }

        OnPropertyChanged(nameof(SecretState));
        return true;
    }


    /// <summary>Which region the journal is read from. Wrong here means an empty answer, not an error.</summary>
    public string Region
    {
        get => _region;
        set => Set(ref _region, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value)) OnPropertyChanged(nameof(CanBuild));
        }
    }

    public bool CanBuild => !IsBusy;

    /// <summary>
    /// Saves what is in the dialog and then builds the rules from the journal. The two are one
    /// action because somebody who has just typed a key expects the button beside it to use it.
    /// </summary>
    public async Task BuildRulesAsync(string secret)
    {
        if (!Save(secret)) return;

        string key = SettingsService.Unprotect(_current.ProtectedClientSecret);
        if (_current.ClientId.Length == 0 || key.Length == 0)
        {
            Status = "Enter a client id and secret first.";
            return;
        }

        IsBusy = true;
        Status = "Asking Blizzard…";

        try
        {
            using var api = new BlizzardApi(_current.ClientId, key, _current.Region);

            // A debug build keeps what arrives so the parse can be changed without asking Blizzard
            // again. A release build has no reason to leave a hundred files of raw JSON behind.
            bool keepRaw = false;
#if DEBUG
            keepRaw = true;
#endif
            var cache = new JournalCache(_fileSystem, _settings.JournalFolder, keepRaw);
            var (expansion, encounters) = await cache.FetchAsync(
                api, new Progress<string>(s => Status = s), CancellationToken.None);

            Status = new RuleBuilder(_fileSystem).Build(expansion, encounters, _settings.RulesPath).Summary;
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
    private void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(_settings.DataDirectory);
            Process.Start(new ProcessStartInfo(_settings.DataDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Status = "Could not open the folder: " + ex.Message;
        }
    }
}

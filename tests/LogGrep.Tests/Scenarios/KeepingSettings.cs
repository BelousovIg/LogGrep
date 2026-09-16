using System.IO.Abstractions.TestingHelpers;
using LogGrep.Services;
using LogGrep.ViewModels;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// What the app remembers between runs. These go straight at the service rather than through the
/// page object, because settings are a dialog of their own and nothing in the main window reaches
/// them - putting them behind the page object would be pretending at a path that does not exist.
/// </summary>
public sealed class KeepingSettings
{
    private const string Folder = @"C:\fake\LogGrep";

    private static SettingsService AFreshMachine(MockFileSystem disk) => new(disk, Folder);

    [Fact]
    public void Nothing_saved_yet_reads_as_nothing_rather_than_failing()
    {
        var settings = AFreshMachine(new MockFileSystem()).Load();

        Assert.Equal(string.Empty, settings.ClientId);
        Assert.False(settings.HasCredentials);
    }

    [Fact]
    public void A_file_somebody_scribbled_in_reads_as_nothing_too()
    {
        var disk = new MockFileSystem();
        disk.AddFile(@"C:\fake\LogGrep\settings.json", new MockFileData("{ this is not json"));

        var settings = AFreshMachine(disk).Load();

        Assert.False(settings.HasCredentials);
    }

    [Fact]
    public void A_key_survives_a_save_and_comes_back_whole()
    {
        var disk = new MockFileSystem();
        var page = new SettingsViewModel(AFreshMachine(disk)) { ClientId = "  abc123  " };

        Assert.True(page.Save("the-secret"));

        var stored = AFreshMachine(disk).Load();
        Assert.Equal("abc123", stored.ClientId);
        Assert.True(stored.HasCredentials);
        Assert.Equal("the-secret", SettingsService.Unprotect(stored.ProtectedClientSecret));
    }

    [Fact]
    public void The_secret_is_not_written_in_the_clear()
    {
        var disk = new MockFileSystem();
        new SettingsViewModel(AFreshMachine(disk)) { ClientId = "abc123" }.Save("the-secret");

        string written = disk.File.ReadAllText(@"C:\fake\LogGrep\settings.json");

        Assert.DoesNotContain("the-secret", written, StringComparison.Ordinal);
    }


    [Fact]
    public void The_file_holds_what_was_told_and_nothing_worked_out_from_it()
    {
        var disk = new MockFileSystem();
        new SettingsViewModel(AFreshMachine(disk)) { ClientId = "abc123" }.Save("the-secret");

        string written = disk.File.ReadAllText(@"C:\fake\LogGrep\settings.json");

        Assert.DoesNotContain("HasCredentials", written, StringComparison.Ordinal);
    }
    [Fact]
    public void Leaving_the_box_empty_keeps_the_secret_that_is_already_there()
    {
        var disk = new MockFileSystem();
        new SettingsViewModel(AFreshMachine(disk)) { ClientId = "abc123" }.Save("the-secret");

        // Somebody comes back to correct a typo in the id and does not go looking for their secret.
        new SettingsViewModel(AFreshMachine(disk)) { ClientId = "abc999" }.Save(string.Empty);

        var stored = AFreshMachine(disk).Load();
        Assert.Equal("abc999", stored.ClientId);
        Assert.Equal("the-secret", SettingsService.Unprotect(stored.ProtectedClientSecret));
    }

    [Fact]
    public void Something_that_is_not_a_secret_at_all_unprotects_to_nothing()
        => Assert.Equal(string.Empty, SettingsService.Unprotect("not base64 and not encrypted"));

    [Fact]
    public void The_settings_and_the_rules_live_in_the_one_folder()
    {
        var service = AFreshMachine(new MockFileSystem());

        Assert.Equal(@"C:\fake\LogGrep\settings.json", service.SettingsPath);
        Assert.Equal(@"C:\fake\LogGrep\rules.txt", service.RulesPath);
    }
}

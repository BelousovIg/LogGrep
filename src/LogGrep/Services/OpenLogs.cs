using System.IO.Abstractions;

namespace LogGrep.Services;

/// <summary>
/// The list of logs the window has open, kept across restarts.
///
/// Paths only. What each file turned out to hold - attempts, encounters, when it starts - is read
/// from the file itself every time, so nothing stale can be shown as if it were current, and a log
/// that grew since yesterday is simply read again.
/// </summary>
public sealed class OpenLogs
{
    private readonly IFileSystem _fileSystem;
    private readonly string _path;

    public OpenLogs(IFileSystem fileSystem, string dataDirectory)
    {
        _fileSystem = fileSystem;
        _path = _fileSystem.Path.Combine(dataDirectory, "logs.txt");
    }

    /// <summary>
    /// What was open last time. A path that no longer exists still comes back: the window shows it
    /// as a row that says so, because a file quietly vanishing from a list is worse than a row
    /// somebody has to dismiss.
    /// </summary>
    public IReadOnlyList<string> Load()
    {
        try
        {
            if (!_fileSystem.File.Exists(_path)) return Array.Empty<string>();

            return _fileSystem.File.ReadAllLines(_path)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            // A list nobody can read is a list to start again from, not a reason to refuse to open.
            return Array.Empty<string>();
        }
    }

    public void Save(IEnumerable<string> paths)
    {
        try
        {
            _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(_path)!);
            _fileSystem.File.WriteAllLines(_path, paths);
        }
        catch (Exception)
        {
            // Losing the list costs somebody one drag-and-drop; failing the open costs them the app.
        }
    }
}

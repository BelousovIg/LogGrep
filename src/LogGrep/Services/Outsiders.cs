using System.IO.Abstractions;

namespace LogGrep.Services;

/// <summary>
/// The characters that have been set aside as not ours, kept across restarts.
///
/// Stored the negative way round on purpose. A raid is mostly the same people, with the occasional
/// stranger passing through, so the work is crossing a few names off rather than ticking twenty on -
/// and a list of exceptions means somebody who joins next month is one of ours the moment they turn
/// up, instead of silently missing from every analysis until anybody notices the box.
///
/// Identifiers only, one per line, the same way the log list keeps paths. Who somebody is and what
/// they played is read back out of the logs every time, so nothing here can go stale.
///
/// The identifier is the log's own, because a name is exactly what a transfer or a rename changes,
/// and a list keyed on names would quietly forget somebody the week they moved realm.
/// </summary>
public sealed class Outsiders
{
    private readonly IFileSystem _fileSystem;
    private readonly string _path;

    public Outsiders(IFileSystem fileSystem, string dataDirectory)
    {
        _fileSystem = fileSystem;
        _path = _fileSystem.Path.Combine(dataDirectory, "outsiders.txt");
    }

    public IReadOnlyCollection<string> Load()
    {
        try
        {
            if (!_fileSystem.File.Exists(_path)) return Array.Empty<string>();

            return _fileSystem.File.ReadAllLines(_path)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception)
        {
            // A list nobody can read is a list to answer again, not a reason to refuse to open.
            return Array.Empty<string>();
        }
    }

    public void Save(IEnumerable<string> identifiers)
    {
        try
        {
            _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(_path)!);
            _fileSystem.File.WriteAllLines(_path,
                identifiers.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal));
        }
        catch (Exception)
        {
            // Losing the marks costs a few clicks; failing the save costs them the session.
        }
    }
}

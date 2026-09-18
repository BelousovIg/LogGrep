using System.IO.Abstractions;

namespace LogGrep.Services;

/// <summary>
/// Which characters are ours, kept across restarts.
///
/// Identifiers only, one per line, the same way the log list keeps paths. Everything a person reads
/// about somebody - their name, their realm, what they played, how many attempts they were in - is
/// read back out of the logs every time, so nothing here can go stale and nothing here can be shown
/// as current when it is not.
///
/// The identifier is the log's own, because a name is exactly the thing a transfer or a rename
/// changes, and a list keyed on names would quietly forget somebody the week they moved realm.
///
/// An empty file means the question has not been answered yet, which is not the same as answering
/// "nobody". The window reads it as everybody, and says so.
/// </summary>
public sealed class OurPeople
{
    private readonly IFileSystem _fileSystem;
    private readonly string _path;

    public OurPeople(IFileSystem fileSystem, string dataDirectory)
    {
        _fileSystem = fileSystem;
        _path = _fileSystem.Path.Combine(dataDirectory, "ours.txt");
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
            _fileSystem.File.WriteAllLines(_path, identifiers.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal));
        }
        catch (Exception)
        {
            // Losing the marks costs a few clicks; failing the save costs them the session.
        }
    }
}

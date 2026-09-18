using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using LogGrep.Models;

namespace LogGrep.Services;

/// <summary>
/// What a log turned out to hold, kept so it is only ever read once.
///
/// Reading a gigabyte and a half takes twelve seconds; running every detector over the result takes
/// a fifth of one. So the expensive thing is the file, and a log whose bytes have not changed has
/// nothing left to say that it did not say the first time.
///
/// The identity is the file's name and the first timestamp inside it. Both are fixed for a file the
/// game only ever appends to, which is the point: the size is deliberately not part of it, because
/// a log that grew while somebody was raiding is the same log with more in it, and an identity that
/// included the size would miss its own cache every time it mattered. The size is kept alongside
/// instead, as the mark of how far this was read.
///
/// <see cref="Entry.Schema"/> is what makes it safe to change the parser. Three fields were added to
/// what a scan produces in one afternoon; a cache written before them is not wrong, it is answering
/// an older question, and the only correct response is to read the file again without saying a word
/// about it.
///
/// Measured on the real evening: a 1415 MB log reads cold in thirteen seconds and warm in two
/// tenths of one, and the cache it leaves behind is 4.7 MB.
/// </summary>
public sealed class ScanCache
{
    /// <summary>
    /// Bumped whenever the parser starts producing something it did not before. There is no
    /// migration and there should not be: the log is still on disk, and reading it again is twelve
    /// seconds against the risk of showing somebody half-old numbers.
    /// </summary>
    public const int Schema = 5;

    private static readonly JsonSerializerOptions Format = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        IncludeFields = false,
    };

    private readonly IFileSystem _fileSystem;
    private readonly string _folder;

    public ScanCache(IFileSystem fileSystem, string dataDirectory)
    {
        _fileSystem = fileSystem;
        _folder = _fileSystem.Path.Combine(dataDirectory, "cache");
    }

    /// <summary>What was cached for this log, or null when nothing usable was.</summary>
    public Entry? Load(string name, DateTime recorded)
    {
        try
        {
            string path = PathFor(name, recorded);
            if (!_fileSystem.File.Exists(path)) return null;

            var entry = JsonSerializer.Deserialize<Entry>(_fileSystem.File.ReadAllText(path), Format);

            // An older parser's answers, or a file somebody edited. Either way the log is still
            // there and reading it again is the cheap, correct thing to do.
            return entry is { Schema: Schema } ? entry : null;
        }
        catch (Exception)
        {
            // A cache nobody can read is a cache to write again, not a reason to refuse to open.
            return null;
        }
    }

    public void Save(string name, DateTime recorded, Entry entry)
    {
        try
        {
            _fileSystem.Directory.CreateDirectory(_folder);
            _fileSystem.File.WriteAllText(PathFor(name, recorded), JsonSerializer.Serialize(entry, Format));
        }
        catch (Exception)
        {
            // Losing a cache costs twelve seconds next time; failing the scan costs the session.
        }
    }

    /// <summary>
    /// One file per log, named after the identity. The timestamp goes in the name so two files that
    /// happen to share a name - an export and the log it came from - do not share a cache.
    /// </summary>
    private string PathFor(string name, DateTime recorded)
    {
        var safe = new string(name.Select(c => _fileSystem.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)
            .ToArray());

        return _fileSystem.Path.Combine(_folder,
            safe + "-" + recorded.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture) +
            ".json");
    }

    /// <summary>
    /// Everything a scan produced, plus what it would need to pick up where it left off.
    ///
    /// The resume point is the end of the last <em>finished</em> fight rather than the last line
    /// read. A fight in progress when the file was last looked at is not a result and must not be
    /// kept as one; discarding it costs re-reading its own seconds and gets a real attempt out of
    /// it the moment the game writes the end.
    /// </summary>
    public sealed class Entry
    {
        public int Schema { get; set; }

        /// <summary>How large the file was when this was written, which says whether it has grown.</summary>
        public long Size { get; set; }

        /// <summary>The offset to carry on from: the end of the last fight the log finished.</summary>
        public long ReadTo { get; set; }

        /// <summary>The last zone and map lines seen before that point, which a later fight re-emits.</summary>
        public ByteRange Zone { get; set; }

        public ByteRange Map { get; set; }

        public List<PullRecord> Pulls { get; set; } = new();
    }
}

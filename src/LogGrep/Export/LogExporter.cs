using System.IO;
using System.IO.Abstractions;
using System.Text;
using LogGrep.Models;

namespace LogGrep.Export;

/// <summary>
/// Copies the selected pulls out of the source log byte for byte, so the result looks
/// exactly like a log the game itself wrote: COMBAT_LOG_VERSION header, the zone/map
/// context that was in effect, then the fight.
///
/// The attempts handed in may come from several files at once, and every offset one carries is an
/// offset into its own file - so the exporter follows the attempt to its source rather than being
/// told which file is open. Attempts are written in reading order, which keeps an export of a whole
/// evening in the order the evening ran.
/// </summary>
public sealed class LogExporter
{
    private const int CopyBufferSize = 1 << 20;

    private readonly IFileSystem _fileSystem;

    /// <summary>The real disk. Tests hand in a fake one instead.</summary>
    public LogExporter() : this(new FileSystem())
    {
    }

    public LogExporter(IFileSystem fileSystem) => _fileSystem = fileSystem;

    /// <summary>Writes every selected pull into one file. Returns the number of pulls written.</summary>
    public int ExportSingle(IReadOnlyList<PullRecord> pulls, string destinationFile, CancellationToken ct = default)
    {
        var ordered = InOrder(pulls);
        if (ordered.Count == 0) return 0;

        using var destination = Create(destinationFile);
        using var sources = new Sources(_fileSystem);
        byte[] buffer = new byte[CopyBufferSize];

        // One header, from the first file the export draws on. Every log of a given build carries
        // the same one, and a second COMBAT_LOG_VERSION halfway down a file is not something the
        // game ever writes.
        WriteRange(sources.For(ordered[0].Source), destination, ordered[0].Source.Header, buffer);

        var lastZone = ByteRange.Empty;
        var lastMap = ByteRange.Empty;
        LogSource? lastSource = null;

        foreach (var pull in ordered)
        {
            ct.ThrowIfCancellationRequested();
            var source = sources.For(pull.Source);

            // Offsets only mean anything within their own file, so crossing into another one makes
            // the context stale whatever the numbers say.
            if (!ReferenceEquals(lastSource, pull.Source))
            {
                lastZone = ByteRange.Empty;
                lastMap = ByteRange.Empty;
                lastSource = pull.Source;
            }

            // Only repeat the zone/map context when it actually changed between pulls.
            if (!pull.ZoneChange.IsEmpty && pull.ZoneChange != lastZone)
            {
                WriteRange(source, destination, pull.ZoneChange, buffer);
                lastZone = pull.ZoneChange;
            }

            if (!pull.MapChange.IsEmpty && pull.MapChange != lastMap)
            {
                WriteRange(source, destination, pull.MapChange, buffer);
                lastMap = pull.MapChange;
            }

            WriteFight(source, destination, pull, buffer);
        }

        return ordered.Count;
    }

    /// <summary>Writes each selected pull into its own file inside <paramref name="destinationFolder"/>.</summary>
    public IReadOnlyList<string> ExportSeparate(IReadOnlyList<PullRecord> pulls, string destinationFolder,
        CancellationToken ct = default)
    {
        _fileSystem.Directory.CreateDirectory(destinationFolder);

        var written = new List<string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var sources = new Sources(_fileSystem);
        byte[] buffer = new byte[CopyBufferSize];

        foreach (var pull in InOrder(pulls))
        {
            ct.ThrowIfCancellationRequested();

            var source = sources.For(pull.Source);
            string stem = _fileSystem.Path.GetFileNameWithoutExtension(pull.Source.Path);
            string path = UniquePath(destinationFolder, BuildFileName(stem, pull), used);

            using (var destination = Create(path))
            {
                WriteRange(source, destination, pull.Source.Header, buffer);
                WriteRange(source, destination, pull.ZoneChange, buffer);
                WriteRange(source, destination, pull.MapChange, buffer);
                WriteFight(source, destination, pull, buffer);
            }

            written.Add(path);
        }

        return written;
    }

    /// <summary>Reading order: the files as the evening ran, the attempts as they sit in each.</summary>
    private static List<PullRecord> InOrder(IReadOnlyList<PullRecord> pulls)
        => pulls.OrderBy(p => p.Source.Order).ThenBy(p => p.StartOffset).ToList();

    /// <summary>"WoWCombatLog-090126_Gnarlroot_2026-09-15_20-15-31.txt"</summary>
    public static string BuildFileName(string sourceStem, PullRecord pull)
    {
        var name = new StringBuilder();
        if (sourceStem.Length > 0) name.Append(Sanitize(sourceStem)).Append('_');
        name.Append(Sanitize(pull.EncounterName));
        if (pull.Kind == ContentKind.MythicPlus && pull.KeystoneLevel > 0) name.Append('+').Append(pull.KeystoneLevel);
        name.Append('_').Append(pull.StartTime.ToString("yyyy-MM-dd"));
        name.Append('_').Append(pull.StartTime.ToString("HH-mm-ss"));
        name.Append(".txt");
        return name.ToString();
    }

    /// <summary>
    /// The files an export is drawing on, opened once each and held until it is done. An evening of
    /// several logs alternates between them only when it crosses from one to the next, but opening
    /// a 1.4 GB file per attempt would be paid for on every attempt.
    /// </summary>
    private sealed class Sources : IDisposable
    {
        private readonly IFileSystem _fileSystem;
        private readonly Dictionary<string, Stream> _open = new(StringComparer.OrdinalIgnoreCase);

        public Sources(IFileSystem fileSystem) => _fileSystem = fileSystem;

        public Stream For(LogSource source)
        {
            if (_open.TryGetValue(source.Path, out var stream)) return stream;

            stream = _fileSystem.FileStream.New(source.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                CopyBufferSize, FileOptions.RandomAccess);
            _open[source.Path] = stream;
            return stream;
        }

        public void Dispose()
        {
            foreach (var stream in _open.Values) stream.Dispose();
            _open.Clear();
        }
    }

    private Stream Create(string path)
        => _fileSystem.FileStream.New(path, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize);

    private static void WriteFight(Stream source, Stream destination, PullRecord pull, byte[] buffer)
    {
        long length = pull.EndOffset - pull.StartOffset;
        if (length <= 0) return;
        Copy(source, destination, pull.StartOffset, length, buffer);
    }

    private static void WriteRange(Stream source, Stream destination, ByteRange range, byte[] buffer)
    {
        if (range.IsEmpty) return;
        Copy(source, destination, range.Offset, range.Length, buffer);
    }

    private static void Copy(Stream source, Stream destination, long offset, long length, byte[] buffer)
    {
        source.Seek(offset, SeekOrigin.Begin);

        long remaining = length;
        byte last = 0;
        while (remaining > 0)
        {
            int want = (int)Math.Min(buffer.Length, remaining);
            int read = source.Read(buffer, 0, want);
            if (read <= 0) break;
            destination.Write(buffer, 0, read);
            last = buffer[read - 1];
            remaining -= read;
        }

        // The very last line of a log often has no line break of its own.
        if (last != (byte)'\n') destination.WriteByte((byte)'\n');
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            builder.Append(Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '-' : c);
        }

        string result = builder.ToString().Trim('-', '.');
        return result.Length == 0 ? "encounter" : result;
    }

    private string UniquePath(string folder, string fileName, HashSet<string> used)
    {
        string candidate = _fileSystem.Path.Combine(folder, fileName);
        if (used.Add(candidate) && !_fileSystem.File.Exists(candidate)) return candidate;

        string stem = _fileSystem.Path.GetFileNameWithoutExtension(fileName);
        string extension = _fileSystem.Path.GetExtension(fileName);
        for (int i = 2; ; i++)
        {
            candidate = _fileSystem.Path.Combine(folder, stem + "_" + i + extension);
            if (used.Add(candidate) && !_fileSystem.File.Exists(candidate)) return candidate;
        }
    }
}

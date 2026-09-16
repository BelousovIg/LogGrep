using System.IO;
using System.Text;
using LogGrep.Models;

namespace LogGrep.Export;

/// <summary>
/// Copies the selected pulls out of the source log byte for byte, so the result looks
/// exactly like a log the game itself wrote: COMBAT_LOG_VERSION header, the zone/map
/// context that was in effect, then the fight.
/// </summary>
public static class LogExporter
{
    private const int CopyBufferSize = 1 << 20;

    /// <summary>Writes every selected pull into one file. Returns the number of pulls written.</summary>
    public static int ExportSingle(ScanResult scan, IReadOnlyList<PullRecord> pulls, string destinationFile,
        CancellationToken ct = default)
    {
        var ordered = pulls.OrderBy(p => p.StartOffset).ToList();
        if (ordered.Count == 0) return 0;

        using var source = OpenSource(scan.FilePath);
        using var destination = Create(destinationFile);
        byte[] buffer = new byte[CopyBufferSize];

        WriteRange(source, destination, scan.Header, buffer);

        long lastZone = -1;
        long lastMap = -1;
        foreach (var pull in ordered)
        {
            ct.ThrowIfCancellationRequested();

            // Only repeat the zone/map context when it actually changed between pulls.
            if (!pull.ZoneChange.IsEmpty && pull.ZoneChange.Offset != lastZone)
            {
                WriteRange(source, destination, pull.ZoneChange, buffer);
                lastZone = pull.ZoneChange.Offset;
            }

            if (!pull.MapChange.IsEmpty && pull.MapChange.Offset != lastMap)
            {
                WriteRange(source, destination, pull.MapChange, buffer);
                lastMap = pull.MapChange.Offset;
            }

            WriteFight(source, destination, pull, buffer);
        }

        return ordered.Count;
    }

    /// <summary>Writes each selected pull into its own file inside <paramref name="destinationFolder"/>.</summary>
    public static IReadOnlyList<string> ExportSeparate(ScanResult scan, IReadOnlyList<PullRecord> pulls,
        string destinationFolder, CancellationToken ct = default)
    {
        Directory.CreateDirectory(destinationFolder);

        string stem = Path.GetFileNameWithoutExtension(scan.FilePath);
        var written = new List<string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var source = OpenSource(scan.FilePath);
        byte[] buffer = new byte[CopyBufferSize];

        foreach (var pull in pulls.OrderBy(p => p.StartOffset))
        {
            ct.ThrowIfCancellationRequested();

            string path = UniquePath(destinationFolder, BuildFileName(stem, pull), used);
            using (var destination = Create(path))
            {
                WriteRange(source, destination, scan.Header, buffer);
                WriteRange(source, destination, pull.ZoneChange, buffer);
                WriteRange(source, destination, pull.MapChange, buffer);
                WriteFight(source, destination, pull, buffer);
            }

            written.Add(path);
        }

        return written;
    }

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

    private static FileStream OpenSource(string path)
        => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, CopyBufferSize, FileOptions.RandomAccess);

    private static FileStream Create(string path)
        => new(path, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize);

    private static void WriteFight(FileStream source, FileStream destination, PullRecord pull, byte[] buffer)
    {
        long length = pull.EndOffset - pull.StartOffset;
        if (length <= 0) return;
        Copy(source, destination, pull.StartOffset, length, buffer);
    }

    private static void WriteRange(FileStream source, FileStream destination, ByteRange range, byte[] buffer)
    {
        if (range.IsEmpty) return;
        Copy(source, destination, range.Offset, range.Length, buffer);
    }

    private static void Copy(FileStream source, FileStream destination, long offset, long length, byte[] buffer)
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

    private static string UniquePath(string folder, string fileName, HashSet<string> used)
    {
        string candidate = Path.Combine(folder, fileName);
        if (used.Add(candidate) && !File.Exists(candidate)) return candidate;

        string stem = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        for (int i = 2; ; i++)
        {
            candidate = Path.Combine(folder, stem + "_" + i + extension);
            if (used.Add(candidate) && !File.Exists(candidate)) return candidate;
        }
    }
}

using LogGrep.Models;

namespace LogGrep.ViewModels;

/// <summary>
/// One file in the list of open logs.
///
/// A row exists from the moment somebody adds the file, which is before anything is known about it.
/// The three columns that describe its contents stay empty until the reading reaches it, because a
/// file that has been added but not yet read is a real state and pretending otherwise - with a zero,
/// or with nothing at all - tells somebody the file is empty.
/// </summary>
public sealed class LogRowViewModel : ObservableObject
{
    private LogSource? _source;
    private bool _missing;

    public LogRowViewModel(string path, string name)
    {
        Path = path;
        Name = name;
    }

    public string Path { get; }

    /// <summary>Just the file name. The full path lives in the tooltip, which is all the old box was for.</summary>
    public string Name { get; }

    /// <summary>What the reading found in this file, or null while it has not been read.</summary>
    public LogSource? Source
    {
        get => _source;
        set
        {
            _source = value;
            Raise();
        }
    }

    /// <summary>Whether the file has gone since it was added, which a restart makes common.</summary>
    public bool Missing
    {
        get => _missing;
        set
        {
            if (Set(ref _missing, value)) Raise();
        }
    }

    public string PullsText => Missing ? "—" : Source is { } s ? Display.Count(s.Pulls) : string.Empty;

    public string EncountersText => Missing ? "—" : Source is { } s ? Display.Count(s.Encounters) : string.Empty;

    /// <summary>When the first attempt in this file started - not when the file was opened.</summary>
    public string StartedText => Missing
        ? "file is gone"
        : Source is { Started: { } started } ? Display.Moment(started) : string.Empty;

    private void Raise()
    {
        OnPropertyChanged(nameof(PullsText));
        OnPropertyChanged(nameof(EncountersText));
        OnPropertyChanged(nameof(StartedText));
    }
}

using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using LogGrep.Models;

namespace LogGrep.ViewModels;

/// <summary>One attempt inside an encounter row.</summary>
public sealed class PullViewModel : ObservableObject
{
    private bool _isSelected;
    private bool _isExpanded;
    private string? _roster;
    private ListCollectionView? _playersView;

    public PullViewModel(PullRecord record, EncounterViewModel owner)
    {
        Record = record;
        Owner = owner;
    }

    public PullRecord Record { get; }

    public EncounterViewModel Owner { get; }

    /// <summary>Shared sort state, reached through the owner so the player headers can bind to it.</summary>
    public Sorting Sorting => Owner.Sorting;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Set(ref _isSelected, value)) Owner.OnPullSelectionChanged();
        }
    }

    /// <summary>Whether the per-player table under this attempt is open.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public bool HasPlayers => Record.Roster.Count > 0;

    /// <summary>
    /// The per-player table, built the first time it is shown. A log can hold hundreds of pulls
    /// and most are never opened, so there is no point building all of them up front.
    /// </summary>
    public ICollectionView PlayersView => _playersView ??= CreatePlayersView();

    /// <summary>Re-orders the player rows, but only for a table that has actually been opened.</summary>
    public void ApplyPlayerSorting()
    {
        if (_playersView != null) _playersView.CustomSort = Sorting.Players.Comparer;
    }

    /// <summary>Sets the flag without bubbling back up, used when the parent drives the change.</summary>
    internal void SetSelectedSilently(bool value)
    {
        if (_isSelected == value) return;
        _isSelected = value;
        OnPropertyChanged(nameof(IsSelected));
    }

    public bool IsSuccess => Record.Success;

    public string ResultText => Record.Success ? "win" : "wiped";

    public string StartText => Record.StartTime.ToString("MMM dd HH:mm:ss", CultureInfo.InvariantCulture);

    public string DurationText => Display.Duration(Record.Duration);

    public string ParticipantsText => Record.Participants > 0 ? Record.Participants.ToString() : "—";

    public string DpsText => Display.Rate(Record.Dps);

    public string HpsText => Display.Rate(Record.Hps);

    /// <summary>Every group member of this pull as "Name - Realm", comma separated.</summary>
    public string Roster => _roster ??= string.Join(", ", Record.Players.Select(PlayerName.Format));

    public string RosterTooltip => Record.Players.Count == 0
        ? "No player names were found for this pull"
        : string.Join(Environment.NewLine, Record.Players.Select(PlayerName.Format));

    private ListCollectionView CreatePlayersView()
    {
        var rows = Record.Roster
            .Select(stats => new PlayerRowViewModel(stats, Record.Duration))
            .ToList();

        return new ListCollectionView(rows) { CustomSort = Sorting.Players.Comparer };
    }
}

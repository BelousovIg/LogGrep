using System.Globalization;
using System.Windows;
using LogGrep.Models;

namespace LogGrep.ViewModels;

/// <summary>One attempt inside an encounter row.</summary>
public sealed class PullViewModel : ObservableObject
{
    private bool _isSelected;
    private string? _roster;

    public PullViewModel(PullRecord record, EncounterViewModel owner)
    {
        Record = record;
        Owner = owner;
        CopyRosterCommand = new RelayCommand(CopyRoster, () => Roster.Length > 0);
    }

    public PullRecord Record { get; }

    public EncounterViewModel Owner { get; }

    public RelayCommand CopyRosterCommand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Set(ref _isSelected, value)) Owner.OnPullSelectionChanged();
        }
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

    private void CopyRoster()
    {
        try
        {
            Clipboard.SetDataObject(Roster, copy: true);
        }
        catch (Exception ex)
        {
            Owner.Report("Could not copy to the clipboard: " + ex.Message);
            return;
        }

        Owner.Report("Copied " + Record.Players.Count + " names from " + Record.EncounterName +
                     " (" + StartText + ") to the clipboard.");
    }
}

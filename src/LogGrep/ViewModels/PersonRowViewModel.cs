using System.Windows.Media;
using LogGrep.Models;

namespace LogGrep.ViewModels;

/// <summary>
/// One character in the registry: everything the loaded logs say about them, and the one thing the
/// person using the app says about them - whether they are ours.
///
/// Nothing here is stored except that mark. The counts, the roles and the dates are read back out
/// of the logs every time, so a row cannot show last month's answer as though it were tonight's.
/// </summary>
public sealed class PersonRowViewModel : ObservableObject
{
    private readonly Character _character;
    private readonly Action<PersonRowViewModel> _changed;
    private bool _isOurs;

    public PersonRowViewModel(Character character, bool isOurs, Action<PersonRowViewModel> changed)
    {
        _character = character;
        _isOurs = isOurs;
        _changed = changed;
    }

    /// <summary>The log's own identifier, which is what the mark is filed under.</summary>
    public string Id => _character.Guid;

    public string Name => _character.Who;

    public string Realm => _character.Realm;

    /// <summary>"Name - Realm", what the cell shows on hover and what the clipboard would want.</summary>
    public string FullName => PlayerName.Format(_character.Name);

    public string ClassName => _character.ClassName;

    public Brush ClassBrush => ClassBrushes.For(_character.ClassColor);

    /// <summary>
    /// Every role they were seen in, most-played first. Two of them is not a mistake in the data -
    /// it is somebody who respecced, and the list says so rather than picking one.
    /// </summary>
    public string RolesText => _character.Roles.Count == 0
        ? "—"
        : string.Join(", ", _character.Roles.Select(Specs.NameOf));

    /// <summary>Every specialization behind those roles, for the hover.</summary>
    public string SpecsText => _character.Specs.Count == 0
        ? "No specialization was reported for them"
        : string.Join(", ", _character.Specs.Select(Specs.SpecOf));

    public string PullsText => Display.Count(_character.Pulls);

    public string EncountersText => Display.Count(_character.Encounters);

    public string LastSeenText => _character.LastSeen == default ? "—" : Display.Moment(_character.LastSeen);

    public string SeenTooltip => _character.FirstSeen == default
        ? "Never seen in a fight"
        : "First seen " + Display.Moment(_character.FirstSeen) + ", last seen " + Display.Moment(_character.LastSeen);

    /// <summary>Raw values behind the cells, so the columns sort on numbers and dates.</summary>
    public int PullsValue => _character.Pulls;

    public int EncountersValue => _character.Encounters;

    public DateTime LastSeenValue => _character.LastSeen;

    public bool IsOurs
    {
        get => _isOurs;
        set
        {
            if (!Set(ref _isOurs, value)) return;
            _changed(this);
        }
    }
}

namespace LogGrep.ViewModels;

/// <summary>
/// The sort state of all three tables. One instance lives on the window and is handed down the
/// tree, so a column clicked in any encounter orders that level everywhere at once.
/// Numbers, times and dates sort on their own values - only the genuinely textual columns
/// compare as text.
/// </summary>
public sealed class Sorting
{
    public SortState Encounters { get; } = new(new Dictionary<string, SortColumn>(StringComparer.Ordinal)
    {
        ["Name"] = new(row => ((EncounterViewModel)row).Name),
        ["Difficulty"] = new(row => ((EncounterViewModel)row).DifficultyText),
        ["Pulls"] = new(row => ((EncounterViewModel)row).Pulls.Count, DescendingFirst: true),
        ["Result"] = new(row => ((EncounterViewModel)row).HasKill, DescendingFirst: true),
        ["Party"] = new(row => ((EncounterViewModel)row).PartySizeKey, DescendingFirst: true),
    });

    public SortState Pulls { get; } = new(new Dictionary<string, SortColumn>(StringComparer.Ordinal)
    {
        ["Result"] = new(row => ((PullViewModel)row).IsSuccess, DescendingFirst: true),
        ["Started"] = new(row => ((PullViewModel)row).Record.StartTime),
        ["Roster"] = new(row => ((PullViewModel)row).Roster),
        ["Duration"] = new(row => ((PullViewModel)row).Record.Duration, DescendingFirst: true),
        ["Players"] = new(row => ((PullViewModel)row).Record.Participants, DescendingFirst: true),
        ["Dps"] = new(row => ((PullViewModel)row).Record.Dps, DescendingFirst: true),
        ["Hps"] = new(row => ((PullViewModel)row).Record.Hps, DescendingFirst: true),
    });

    public SortState Players { get; } = new(new Dictionary<string, SortColumn>(StringComparer.Ordinal)
    {
        ["Name"] = new(row => ((PlayerRowViewModel)row).Name),
        ["Class"] = new(row => ((PlayerRowViewModel)row).ClassName),
        ["Spec"] = new(row => ((PlayerRowViewModel)row).SpecName),
        ["Dps"] = new(row => ((PlayerRowViewModel)row).DpsValue, DescendingFirst: true),
        ["Hps"] = new(row => ((PlayerRowViewModel)row).HpsValue, DescendingFirst: true),
        ["Dtps"] = new(row => ((PlayerRowViewModel)row).DtpsValue, DescendingFirst: true),

        // A survivor has no time and no killing blow, so both of these leave them at the bottom.
        ["Died"] = new(row => ((PlayerRowViewModel)row).FirstDeath),
        ["Causes"] = new(row => ((PlayerRowViewModel)row).TopCause),
    });
}

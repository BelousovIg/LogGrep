namespace LogGrep.Analysis;

/// <summary>
/// Spells there is no point counting the uses of.
///
/// The cooldown rule works by watching how often somebody presses a thing and saying when an
/// attempt had fewer of them than their own others did. That question is worth asking of a
/// cooldown and meaningless for a filler: a filler is what gets pressed when nothing better is
/// available, so pressing it less is usually the sign of a *better* attempt, not a worse one.
///
/// The app already refuses to judge a spell that did nothing measurable, which catches movement and
/// utility. It does not catch a filler that deals real damage - a channel a shadow priest spends
/// half the fight in is both a filler and a third of their damage - and no rule drawn from a log
/// tells the two apart, because from outside they look identical: pressed often, hits hard enough
/// to matter.
///
/// So this is a list, and it is filled by people who play the spec. An entry says "do not read
/// anything into how often this was pressed"; it does not hide the spell anywhere else.
/// </summary>
public static class Fillers
{
    /// <summary>
    /// Spell id to what it is. A channel writes two ids - one for the cast, one for the ticks - and
    /// both are listed, because which of them a log carries depends on the event.
    /// </summary>
    private static readonly Dictionary<int, string> Pointless = new()
    {
        // Shadow priest. Mind Flay is the filler the spec presses whenever nothing else is up, and
        // Mind Flay: Insanity replaces it while the proc is held - fewer of either is what a good
        // attempt looks like, not a worse one. Ids read out of a real log rather than a database.
        [15407] = "Mind Flay",
        [193473] = "Mind Flay (ticks)",
        [391403] = "Mind Flay: Insanity",
        [391401] = "Mind Flay: Insanity (ticks)",
    };

    /// <summary>Whether using this one less often says nothing worth saying.</summary>
    public static bool NotWorthCounting(int spellId) => Pointless.ContainsKey(spellId);

    /// <summary>What the list holds, for anybody asking why their spell is not being read.</summary>
    public static IReadOnlyDictionary<int, string> All => Pointless;
}

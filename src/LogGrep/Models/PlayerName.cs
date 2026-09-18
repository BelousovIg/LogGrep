using System.Text;

namespace LogGrep.Models;

/// <summary>
/// Turns the raw combat log spelling of a character ("Ivarpriest-BurningLegion-EU")
/// into something readable ("Ivarpriest - Burning Legion").
/// </summary>
public static class PlayerName
{
    private static readonly string[] Regions = { "EU", "US", "NA", "KR", "TW", "CN" };

    public static string Format(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        int dash = raw.IndexOf('-');
        if (dash <= 0 || dash == raw.Length - 1) return raw;

        string name = raw[..dash];
        string realm = raw[(dash + 1)..];

        // The game appends the region on some realms; it adds nothing here.
        int lastDash = realm.LastIndexOf('-');
        if (lastDash > 0 && IsRegion(realm.AsSpan(lastDash + 1))) realm = realm[..lastDash];

        return name + " - " + SplitWords(realm);
    }

    /// <summary>
    /// Just the character, with the realm dropped: "Ivarpriest-BurningLegion-EU" becomes
    /// "Ivarpriest". Names are unique inside a group, so the realm only matters on hover.
    /// </summary>
    public static string Character(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        int dash = raw.IndexOf('-');
        return dash <= 0 ? raw : raw[..dash];
    }

    /// <summary>
    /// Just the realm, readable: "Ivarpriest-BurningLegion-EU" becomes "Burning Legion". Empty when
    /// the log wrote a bare name, which happens on lines it cut short.
    /// </summary>
    public static string Realm(string raw)
    {
        string whole = Format(raw);
        int dash = whole.IndexOf(" - ", StringComparison.Ordinal);

        return dash < 0 ? string.Empty : whole[(dash + 3)..];
    }

    private static bool IsRegion(ReadOnlySpan<char> value)
    {
        foreach (string region in Regions)
        {
            if (value.Equals(region, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>Realms are written without spaces, so "TarrenMill" becomes "Tarren Mill".</summary>
    private static string SplitWords(string realm)
    {
        var builder = new StringBuilder(realm.Length + 4);
        for (int i = 0; i < realm.Length; i++)
        {
            char c = realm[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(realm[i - 1])) builder.Append(' ');
            builder.Append(c);
        }

        return builder.ToString();
    }
}

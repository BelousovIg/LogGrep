using LogGrep.Models;

namespace LogGrep.ViewModels;

/// <summary>
/// One place the window has been: which screen, and what the report was pointed at while it was
/// there.
///
/// Everything that changes what is on screen belongs in here, including the narrowing - somebody
/// who ticked "wipes only" and then wants it back is doing the same thing as somebody who opened an
/// attempt and wants out of it, and a back button that handles one but not the other is a back
/// button people stop trusting.
/// </summary>
public sealed record Place(
    int Screen, Selection Selection, PullViewModel? Pull, string Player, Role? Role)
{
    /// <summary>Whether this is the same place, so that standing still is not recorded as a move.</summary>
    public bool SameAs(Place other)
        => Screen == other.Screen
           && ReferenceEquals(Selection, other.Selection)
           && ReferenceEquals(Pull, other.Pull)
           && string.Equals(Player, other.Player, StringComparison.Ordinal)
           && Role == other.Role;
}

/// <summary>
/// Where the window has been, so it can go back.
///
/// A plain forward-and-back trail, the way a browser keeps one: moving somewhere new from the middle
/// of it throws the rest away, because a future nobody can get to any more is only there to confuse
/// whoever presses forward.
/// </summary>
public sealed class History : ObservableObject
{
    /// <summary>
    /// How many places are kept. Deep enough that nobody reaches the end of it in a session, and
    /// bounded because every place holds a selection and a session can last an evening.
    /// </summary>
    private const int Deep = 200;

    private readonly List<Place> _places = new();
    private int _at = -1;

    public bool CanGoBack => _at > 0;

    public bool CanGoForward => _at >= 0 && _at < _places.Count - 1;

    /// <summary>Notes that the window is somewhere, unless it is where it already was.</summary>
    public void Went(Place place)
    {
        if (_at >= 0 && _places[_at].SameAs(place)) return;

        // Anything ahead of here is a future that has just stopped being reachable.
        if (_at < _places.Count - 1) _places.RemoveRange(_at + 1, _places.Count - _at - 1);

        _places.Add(place);
        if (_places.Count > Deep) _places.RemoveAt(0);

        _at = _places.Count - 1;
        Moved();
    }

    public Place? Back()
    {
        if (!CanGoBack) return null;

        _at--;
        Moved();
        return _places[_at];
    }

    public Place? Forward()
    {
        if (!CanGoForward) return null;

        _at++;
        Moved();
        return _places[_at];
    }

    /// <summary>Starts again, for when the logs behind every place in it are gone.</summary>
    public void Forget()
    {
        _places.Clear();
        _at = -1;
        Moved();
    }

    private void Moved()
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }
}

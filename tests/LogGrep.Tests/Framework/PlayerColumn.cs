namespace LogGrep.Tests.Framework;

/// <summary>
/// The columns of the roster table, by the name the window sorts them under. A misspelt column
/// would not throw - it would sort by nothing and leave the rows in the order they were already
/// in, which is the kind of green test that proves nothing.
/// </summary>
public enum PlayerColumn
{
    Name,
    Class,
    Spec,
    Dps,
    Hps,
    Dtps,
    Died,
    Causes,
    Mistakes,
}

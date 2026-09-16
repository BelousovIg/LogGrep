using System.Collections;

namespace LogGrep.ViewModels;

/// <summary>How one column is read for sorting, and which direction its first click picks.</summary>
public sealed record SortColumn(Func<object, IComparable?> Key, bool DescendingFirst = false);

/// <summary>
/// Sorting for one level of the tree. It is shared by every table of that level, so ordering the
/// players of one pull orders them the same way in all of them. Until a header is clicked nothing
/// is sorted at all and rows keep the order the scanner produced.
/// </summary>
public sealed class SortState : ObservableObject
{
    private readonly IReadOnlyDictionary<string, SortColumn> _columns;
    private string _column = string.Empty;
    private bool _descending;

    public SortState(IReadOnlyDictionary<string, SortColumn> columns) => _columns = columns;

    /// <summary>Raised after the comparer changed, so open views can re-sort themselves.</summary>
    public event EventHandler? Changed;

    /// <summary>Key of the column being sorted on, empty while the rows are in their natural order.</summary>
    public string Column
    {
        get => _column;
        private set => Set(ref _column, value);
    }

    public bool Descending
    {
        get => _descending;
        private set => Set(ref _descending, value);
    }

    /// <summary>Null while unsorted, which leaves a collection view in the order it was filled.</summary>
    public IComparer? Comparer { get; private set; }

    /// <summary>Clicking the active column flips it, any other column starts on its natural side.</summary>
    public void Toggle(string column)
    {
        if (!_columns.TryGetValue(column, out var definition)) return;

        if (string.Equals(_column, column, StringComparison.Ordinal))
        {
            Descending = !Descending;
        }
        else
        {
            Column = column;
            Descending = definition.DescendingFirst;
        }

        Comparer = new RowComparer(definition.Key, Descending ? -1 : 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Compares two rows on one column. Rows with nothing to compare - a player who never died,
    /// say - sink to the bottom whichever way the column is pointing, because a blank is not a
    /// small value.
    /// </summary>
    private sealed class RowComparer : IComparer
    {
        private readonly Func<object, IComparable?> _key;
        private readonly int _sign;

        public RowComparer(Func<object, IComparable?> key, int sign)
        {
            _key = key;
            _sign = sign;
        }

        public int Compare(object? x, object? y)
        {
            if (x == null || y == null) return 0;

            var left = _key(x);
            var right = _key(y);

            if (left == null) return right == null ? 0 : 1;
            if (right == null) return -1;

            return _sign * left.CompareTo(right);
        }
    }
}

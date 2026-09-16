using System.Collections;

namespace LogGrep.ViewModels;

/// <summary>
/// How one column is read for sorting.
/// </summary>
/// <param name="Key">The value rows are compared on.</param>
/// <param name="DescendingFirst">Which way the column points on its first click.</param>
/// <param name="BlanksLast">
/// Whether a row with no value sinks to the bottom whichever way the column points. Left off, a
/// blank instead counts as the highest value there is, which is what the death column wants: a
/// player who never died outlasted everyone, so reversing the column brings the survivors up.
/// </param>
/// <param name="Tiebreak">Read when the key ties. Always ascending, so names stay A to Z.</param>
public sealed record SortColumn(
    Func<object, IComparable?> Key,
    bool DescendingFirst = false,
    bool BlanksLast = true,
    Func<object, IComparable?>? Tiebreak = null);

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

        Comparer = new RowComparer(definition, Descending ? -1 : 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Compares two rows on one column, falling back to the tiebreak when they match.</summary>
    private sealed class RowComparer : IComparer
    {
        private readonly SortColumn _column;
        private readonly int _sign;

        public RowComparer(SortColumn column, int sign)
        {
            _column = column;
            _sign = sign;
        }

        public int Compare(object? x, object? y)
        {
            if (x == null || y == null) return 0;

            var left = _column.Key(x);
            var right = _column.Key(y);

            int result;
            if (left == null || right == null)
            {
                result = left == null ? right == null ? 0 : 1 : -1;

                // A pinned blank ignores the direction and always sinks; a ranked one rides
                // along with it, sitting above everything once the column is reversed.
                if (result != 0 && _column.BlanksLast) return result;
            }
            else
            {
                result = left.CompareTo(right);
            }

            if (result != 0) return _sign * result;

            return _column.Tiebreak == null ? 0 : Break(x, y);
        }

        private int Break(object x, object y)
        {
            var left = _column.Tiebreak!(x);
            var right = _column.Tiebreak(y);

            if (left == null) return right == null ? 0 : 1;
            if (right == null) return -1;

            return left.CompareTo(right);
        }
    }
}

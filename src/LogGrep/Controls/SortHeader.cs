using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using LogGrep.ViewModels;

namespace LogGrep.Controls;

/// <summary>
/// A clickable column header. Clicking it hands the column key to the <see cref="SortState"/> of
/// its table; the arrow is driven by a binding rather than an event handler, so a header torn
/// down with its row does not keep the long-lived sort state pinned to it.
/// </summary>
public sealed class SortHeader : ButtonBase
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SortHeader),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ColumnProperty =
        DependencyProperty.Register(nameof(Column), typeof(string), typeof(SortHeader),
            new PropertyMetadata(string.Empty, OnBindingInputChanged));

    public static readonly DependencyProperty SortProperty =
        DependencyProperty.Register(nameof(Sort), typeof(SortState), typeof(SortHeader),
            new PropertyMetadata(null, OnBindingInputChanged));

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(SortHeader),
            new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Key this header sorts on, as registered with the table's <see cref="SortState"/>.</summary>
    public string Column
    {
        get => (string)GetValue(ColumnProperty);
        set => SetValue(ColumnProperty, value);
    }

    public SortState? Sort
    {
        get => (SortState?)GetValue(SortProperty);
        set => SetValue(SortProperty, value);
    }

    /// <summary>The arrow, empty unless this column is the one being sorted on.</summary>
    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        private set => SetValue(GlyphProperty, value);
    }

    protected override void OnClick()
    {
        base.OnClick();
        Sort?.Toggle(Column);
    }

    private static void OnBindingInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SortHeader)d).RebindGlyph();

    private void RebindGlyph()
    {
        BindingOperations.ClearBinding(this, GlyphProperty);

        if (Sort == null) return;

        var binding = new MultiBinding
        {
            Converter = GlyphConverter.Instance,
            ConverterParameter = Column,
            Mode = BindingMode.OneWay,
        };
        binding.Bindings.Add(new Binding(nameof(SortState.Column)) { Source = Sort });
        binding.Bindings.Add(new Binding(nameof(SortState.Descending)) { Source = Sort });

        SetBinding(GlyphProperty, binding);
    }

    private sealed class GlyphConverter : IMultiValueConverter
    {
        public static readonly GlyphConverter Instance = new();

        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not string active) return string.Empty;
            if (!string.Equals(active, parameter as string, StringComparison.Ordinal)) return string.Empty;

            return values[1] is true ? "▼" : "▲";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

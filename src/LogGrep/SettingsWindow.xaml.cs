using System.Windows;
using System.Windows.Media;
using LogGrep.Interop;
using LogGrep.ViewModels;

namespace LogGrep;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = new SettingsViewModel();
    }

    private SettingsViewModel Model => (SettingsViewModel)DataContext;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this,
            caption: Brush("PanelBrush"),
            border: Brush("LineBrush"),
            text: Brush("FgBrush"));
    }

    private Color Brush(string key) => ((SolidColorBrush)FindResource(key)).Color;

    /// <summary>
    /// The secret is read straight off the box rather than bound, which is both the only way WPF
    /// offers and the right one: it never becomes a property anything else can reach.
    /// </summary>
    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (Model.Save(Secret.Password)) DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}

using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LogGrep;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Some display drivers hand WPF a hardware surface that never paints, which leaves a
        // blank white window with no error anywhere. This UI is a handful of rows and gains
        // nothing measurable from the GPU, so it draws on the CPU unless asked otherwise.
        if (!e.Args.Any(a => string.Equals(a, "--hardware", StringComparison.OrdinalIgnoreCase)))
        {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        }

        base.OnStartup(e);
    }
}

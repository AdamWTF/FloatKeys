using System.Windows;
using System.Windows.Controls;
using FloatKeys.Controls;
using FloatKeys.Input;
using FloatKeys.Platform;
using FloatKeys.Services;

namespace FloatKeys.Windows;

public partial class KeyboardWindow : Window
{
    private readonly KeyboardController controller;
    private readonly PrototypeLog log;
    private readonly List<(KeyboardKey Control, KeyDefinition Definition)> controls = [];
    private bool shownSymbols;
    private bool closing;

    internal KeyboardWindow(IInputBackend input, PrototypeLog log)
    {
        this.log = log;
        controller = new KeyboardController(input, new DispatcherRepeatScheduler());
        InitializeComponent();
        controller.Changed += Refresh;
        controller.Result += ShowResult;
        controller.InputFailed += CancelControls;
        controller.ExitRequested += Close;
        BuildKeys();
        Closing += (_, _) =>
        {
            closing = true;
            controller.Dispose();
            CancelControls();
        };
        Unloaded += (_, _) => { controller.CancelAll(); CancelControls(); };
    }

    private void BuildKeys()
    {
        CancelControls();
        controls.Clear();
        HeaderKeys.Children.Clear();
        KeyRows.Children.Clear();
        foreach (var definition in new[] { KeyboardLayout.Caps, KeyboardLayout.Exit })
        {
            var key = CreateKey(definition);
            key.Width = definition.Action == KeyAction.Caps ? 112 : 64;
            key.VerticalAlignment = VerticalAlignment.Stretch;
            HeaderKeys.Children.Add(key);
        }
        var layout = controller.Symbols ? KeyboardLayout.Symbols : KeyboardLayout.Main;
        foreach (var row in layout)
        {
            var grid = new Grid { Height = 60 };
            for (int i = 0; i < row.Count; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(row[i].Width, GridUnitType.Star) });
                var key = CreateKey(row[i]);
                Grid.SetColumn(key, i);
                grid.Children.Add(key);
            }
            KeyRows.Children.Add(grid);
        }
        shownSymbols = controller.Symbols;
        Refresh();
    }

    private KeyboardKey CreateKey(KeyDefinition definition)
    {
        var key = new KeyboardKey { Margin = new Thickness(3), Label = controller.Label(definition) };
        key.SetAppearance(definition.Style, controller.Active(definition));
        key.Captured += pointer =>
        {
            log.Write($"interaction begin foreground=0x{NativeMethods.GetForegroundWindow():X}");
            if (!controller.Begin(pointer, definition)) key.Cancel();
        };
        key.Released += (pointer, inside) => controller.End(pointer, inside);
        key.Cancelled += pointer => controller.Cancel(pointer);
        controls.Add((key, definition));
        return key;
    }

    private void Refresh()
    {
        if (closing) return;
        if (shownSymbols != controller.Symbols) { BuildKeys(); return; }
        foreach (var (key, definition) in controls)
        {
            key.Label = controller.Label(definition);
            key.SetAppearance(definition.Style, controller.Active(definition));
        }
    }
    private void CancelControls()
    {
        foreach (var (key, _) in controls.ToArray()) key.Cancel();
    }
    internal void CancelInteractions()
    {
        controller.CancelAll();
        CancelControls();
    }
    private void ShowResult(InputResult result)
    {
        log.Write($"input result foreground=0x{NativeMethods.GetForegroundWindow():X}");
        if (result.Success) Status.Text = "Submitted to Windows; verify the target received it.";
        else
        {
            Status.Text = result.CleanupFailed ? "Release failed. Cancel a tap or exit to retry; see prototype.log." :
                $"{result.Error} ({result.Submitted}/{result.Requested}, error {result.NativeError})";
            log.Write($"error={result.Error} cleanupFailed={result.CleanupFailed}");
        }
    }
}

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
    private readonly AppLog log;
    private readonly List<(KeyboardKey Control, KeyDefinition Definition)> controls = [];
    private bool shownSymbols;
    private bool closing;
    private readonly List<KeyboardKey> actions = [];
    private KeyboardKey? sizeLabel, opacityLabel;
    internal PointerHandle DragHandle { get; }
    internal PointerHandle ResizeHandle { get; }
    internal bool Symbols => controller.Symbols;
    internal event Action? CollapseRequested;
    internal event Action? DismissRequested;
    internal bool AllowClose { get; set; }
    internal event Action? LayoutChanged;
    internal event Action<int>? SizeStep;
    internal event Action<int>? OpacityStep;

    internal KeyboardWindow(IInputBackend input, AppLog log, bool symbols = false)
    {
        this.log = log;
        controller = new KeyboardController(input, new DispatcherRepeatScheduler(), symbols: symbols);
        InitializeComponent();
        DragHandle = new(DragSurface);
        ResizeHandle = new(ResizeSurface);
        BuildSettings();
        controller.Changed += Refresh;
        controller.Result += ShowResult;
        controller.InputFailed += CancelControls;
        BuildKeys();
        Closing += (_, e) =>
        {
            if (!AllowClose && DismissRequested is not null)
            {
                e.Cancel = true;
                DismissRequested.Invoke();
                return;
            }
            closing = true;
            controller.Dispose();
            CancelControls();
        };
        Unloaded += (_, _) => { controller.CancelAll(); CancelControls(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) CloseSettings(); };
    }

    private void BuildKeys()
    {
        CancelControls();
        controls.Clear();
        actions.RemoveAll(key => HeaderKeys.Children.Contains(key));
        HeaderKeys.Children.Clear();
        KeyRows.Children.Clear();
        var caps = CreateKey(KeyboardLayout.Caps);
        caps.Width = 112;
        HeaderKeys.Children.Add(caps);
        HeaderKeys.Children.Add(ActionKey("⚙", () =>
        {
            CancelInteractions();
            bool open = SettingsPanel.Visibility != Visibility.Visible;
            SettingsPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            KeyRows.IsEnabled = !open;
        }));
        var minimise = ActionKey("−", () => CollapseRequested?.Invoke());
        minimise.ToolTip = "Minimise keyboard";
        HeaderKeys.Children.Add(minimise);
        var layout = controller.Symbols ? KeyboardLayout.Symbols : KeyboardLayout.Main;
        KeyRows.RowDefinitions.Clear();
        int rowIndex = 0;
        foreach (var row in layout)
        {
            KeyRows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var grid = new Grid();
            Grid.SetRow(grid, rowIndex++);
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
        if (shownSymbols != controller.Symbols) { BuildKeys(); LayoutChanged?.Invoke(); return; }
        foreach (var (key, definition) in controls)
        {
            key.Label = controller.Label(definition);
            key.SetAppearance(definition.Style, controller.Active(definition));
        }
    }
    private void CancelControls()
    {
        foreach (var (key, _) in controls.ToArray()) key.Cancel();
        foreach (var key in actions.ToArray()) key.Cancel();
    }
    internal void CancelInteractions()
    {
        controller.CancelAll();
        CancelControls();
    }
    internal void CancelGeometry() { DragHandle.Cancel(); ResizeHandle.Cancel(); }
    private KeyboardKey ActionKey(string label, Action callback, double width = 50)
    {
        var key = new KeyboardKey { Label = label, Width = width, Height = 46, Margin = new Thickness(3) };
        key.SetAppearance(KeyStyle.Special, false);
        key.Released += (_, inside) => { if (inside) callback(); };
        actions.Add(key);
        return key;
    }
    private void BuildSettings()
    {
        SizeSettings.Children.Add(ActionKey("−", () => SizeStep?.Invoke(-1)));
        sizeLabel = ActionKey("56 DIP", () => { }, 180);
        sizeLabel.IsHitTestVisible = false;
        SizeSettings.Children.Add(sizeLabel);
        SizeSettings.Children.Add(ActionKey("+", () => SizeStep?.Invoke(1)));
        OpacitySettings.Children.Add(ActionKey("−", () => OpacityStep?.Invoke(-1)));
        opacityLabel = ActionKey("85% opacity", () => { }, 180);
        opacityLabel.IsHitTestVisible = false;
        OpacitySettings.Children.Add(opacityLabel);
        OpacitySettings.Children.Add(ActionKey("+", () => OpacityStep?.Invoke(1)));
    }
    internal void RefreshSettings(double size, double opacity)
    {
        sizeLabel!.Label = $"{size:0} DIP size";
        opacityLabel!.Label = $"{opacity:P0} opacity";
    }
    private void CloseSettings()
    {
        SettingsPanel.Visibility = Visibility.Collapsed;
        KeyRows.IsEnabled = true;
    }
    internal void ShowSettings()
    {
        CancelInteractions();
        SettingsPanel.Visibility = Visibility.Visible;
        KeyRows.IsEnabled = false;
    }
    private void ShowResult(InputResult result)
    {
        if (result.Success) Status.Text = "Submitted to Windows; verify the target received it.";
        else
        {
            Status.Text = result.CleanupFailed ? "Release failed. Cancel a tap or exit to retry; see FloatKeys logs." :
                $"{result.Error} ({result.Submitted}/{result.Requested}, error {result.NativeError})";
            log.Write($"input failed requested={result.Requested} submitted={result.Submitted} nativeError={result.NativeError} cleanupFailed={result.CleanupFailed}");
        }
    }
}

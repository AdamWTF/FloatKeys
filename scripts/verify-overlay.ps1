param(
    [Parameter(Mandatory = $true)]
    [string] $Executable
)

$ErrorActionPreference = 'Stop'
# Smoke check only: no keyboard injection and no target application interaction.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class OverlayProbe {
    public delegate bool EnumCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr window, StringBuilder text, int size);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)] public static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    public static IntPtr Find(uint process) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, _) => {
            uint owner;
            GetWindowThreadProcessId(window, out owner);
            var title = new StringBuilder(256);
            GetWindowText(window, title, title.Capacity);
            if (owner == process && IsWindowVisible(window) && title.ToString().StartsWith("FloatKeys")) {
                found = window;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
'@

$resolvedExecutable = (Resolve-Path -LiteralPath $Executable).Path
$foregroundBefore = [OverlayProbe]::GetForegroundWindow()
$prototypeProcess = Start-Process -FilePath $resolvedExecutable -WindowStyle Hidden -PassThru
$overlayHandle = [IntPtr]::Zero
try {
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        if ($prototypeProcess.HasExited) { throw "Prototype exited before showing its window (exit $($prototypeProcess.ExitCode))." }
        $overlayHandle = [OverlayProbe]::Find([uint32]$prototypeProcess.Id)
        if ($overlayHandle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 100
    }
    if ($overlayHandle -eq [IntPtr]::Zero) { throw 'Visible prototype window was not found.' }
    $styles = [OverlayProbe]::GetWindowLongPtr($overlayHandle, -20).ToInt64()
    $expectedStyles = 0x08000088 # WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST
    if (($styles -band $expectedStyles) -ne $expectedStyles) { throw ('Missing overlay styles: 0x{0:X}' -f $styles) }
    if (($styles -band 0x00040000) -ne 0) { throw 'WS_EX_APPWINDOW unexpectedly present.' }
    $foregroundAfter = [OverlayProbe]::GetForegroundWindow()
    if ($foregroundAfter -eq $overlayHandle) { throw 'Prototype became foreground during startup.' }
    if ($foregroundAfter -ne $foregroundBefore) { throw 'Foreground changed during smoke check; repeat without interacting with other windows.' }
    'PASS executable={0} styles=0x{1:X} foregroundBefore=0x{2:X} foregroundAfter=0x{3:X}' -f $resolvedExecutable, $styles, $foregroundBefore.ToInt64(), $foregroundAfter.ToInt64()
}
finally {
    if ($overlayHandle -ne [IntPtr]::Zero) {
        [void][OverlayProbe]::PostMessageW($overlayHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) # WM_CLOSE, graceful cleanup
    }
    if (!$prototypeProcess.WaitForExit(5000)) {
        throw "Prototype process $($prototypeProcess.Id) did not exit; close it manually."
    }
    if ($prototypeProcess.ExitCode -ne 0) { throw "Prototype exited with code $($prototypeProcess.ExitCode)." }
}

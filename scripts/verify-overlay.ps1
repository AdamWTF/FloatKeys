param(
    [Parameter(Mandatory = $true)]
    [string] $Executable,
    [string] $LifecycleProbe
)

$ErrorActionPreference = 'Stop'
# Smoke check only: no keyboard injection and no target application interaction.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class OverlayProbe {
    public static System.Collections.Generic.List<IntPtr> Windows(uint pid) {
        var result = new System.Collections.Generic.List<IntPtr>();
        EnumWindows((h, p) => { GetWindowThreadProcessId(h, out uint owner); if (owner == pid) result.Add(h); return true; }, IntPtr.Zero);
        return result;
    }
    public delegate bool EnumCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr window, StringBuilder text, int size);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)] public static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    public static IntPtr Find(uint process, string suffix) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, _) => {
            uint owner;
            GetWindowThreadProcessId(window, out owner);
            var title = new StringBuilder(256);
            GetWindowText(window, title, title.Capacity);
            if (owner == process && title.ToString() == "FloatKeys — " + suffix) {
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
$keyboardHandle = [IntPtr]::Zero
try {
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        if ($prototypeProcess.HasExited) { throw "Prototype exited before showing its window (exit $($prototypeProcess.ExitCode))." }
        $overlayHandle = [OverlayProbe]::Find([uint32]$prototypeProcess.Id, 'Launcher')
        $keyboardHandle = [OverlayProbe]::Find([uint32]$prototypeProcess.Id, 'Keyboard')
        if ($overlayHandle -ne [IntPtr]::Zero -and $keyboardHandle -ne [IntPtr]::Zero -and [OverlayProbe]::IsWindowVisible($overlayHandle)) { break }
        Start-Sleep -Milliseconds 100
    }
    if ($overlayHandle -eq [IntPtr]::Zero -or $keyboardHandle -eq [IntPtr]::Zero) { throw 'Both overlay windows were not found.' }
    $styles = [OverlayProbe]::GetWindowLongPtr($overlayHandle, -20).ToInt64()
    $expectedStyles = 0x08000088 # WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST
    if (($styles -band $expectedStyles) -ne $expectedStyles) { throw ('Missing overlay styles: 0x{0:X}' -f $styles) }
    if (($styles -band 0x00040000) -ne 0) { throw 'WS_EX_APPWINDOW unexpectedly present.' }
    $keyboardStyles = [OverlayProbe]::GetWindowLongPtr($keyboardHandle, -20).ToInt64()
    if (($keyboardStyles -band $expectedStyles) -ne $expectedStyles -or ($keyboardStyles -band 0x00040000) -ne 0) { throw 'Invalid keyboard styles.' }
    if ([OverlayProbe]::IsWindowVisible($keyboardHandle)) { throw 'Keyboard must start hidden.' }
    $secondProcess = Start-Process -FilePath $resolvedExecutable -WindowStyle Hidden -PassThru
    if (!$secondProcess.WaitForExit(5000) -or $secondProcess.ExitCode -ne 0) { throw 'Second instance did not exit quietly.' }
    if ($prototypeProcess.HasExited) { throw 'Existing instance was terminated by secondary launch.' }
    $foregroundAfter = [OverlayProbe]::GetForegroundWindow()
    if ($foregroundAfter -eq $overlayHandle) { throw 'Prototype became foreground during startup.' }
    if ($foregroundAfter -ne $foregroundBefore) { throw 'Foreground changed during smoke check; repeat without interacting with other windows.' }
    'PASS executable={0} styles=0x{1:X} foregroundBefore=0x{2:X} foregroundAfter=0x{3:X}' -f $resolvedExecutable, $styles, $foregroundBefore.ToInt64(), $foregroundAfter.ToInt64()
}
finally {
    if ($overlayHandle -ne [IntPtr]::Zero) {
        [void][OverlayProbe]::PostMessageW($overlayHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) # WM_CLOSE must hide only.
        Start-Sleep -Milliseconds 300
        if ($prototypeProcess.HasExited) { throw 'Native window closure unexpectedly terminated FloatKeys.' }
        if ([OverlayProbe]::IsWindowVisible($overlayHandle)) { throw 'Native window closure did not hide the launcher.' }
        # Simulate session-ending messages only on this test process, never broadcast/log off Windows.
        # WPF receives session notifications through an application-owned hidden HWND,
        # not the overlay HWND. Reproduce the broadcast only within this process.
        $sessionWindows = [OverlayProbe]::Windows([uint32]$prototypeProcess.Id)
        foreach ($window in $sessionWindows) { [void][OverlayProbe]::PostMessageW($window, 0x0011, [IntPtr]::Zero, [IntPtr]::Zero) }
        Start-Sleep -Milliseconds 100
        foreach ($window in $sessionWindows) { [void][OverlayProbe]::PostMessageW($window, 0x0016, [IntPtr]1, [IntPtr]::Zero) }
    }
    if (!$prototypeProcess.WaitForExit(5000)) {
        throw "Prototype process $($prototypeProcess.Id) did not exit; close it manually."
    }
    if ($prototypeProcess.ExitCode -ne 0) { throw "Prototype exited with code $($prototypeProcess.ExitCode)." }
    if ([OverlayProbe]::GetForegroundWindow() -ne $foregroundBefore) { throw 'Foreground changed during shutdown.' }
    'PASS secondaryInstanceRejected=true closeHides=true sessionShutdown=true processExited=true'
}
if ($LifecycleProbe) {
    & (Resolve-Path -LiteralPath $LifecycleProbe).Path
    if ($LASTEXITCODE -ne 0) { throw 'Lifecycle/render probe failed.' }
}

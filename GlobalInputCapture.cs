using System.Runtime.InteropServices;
using System.Windows;

namespace InputMapperDesktop;

internal sealed class NativeInputEvent(string kind, string phase, string code = "", string key = "", double x = 0, double y = 0, string button = "")
{
    public string Kind { get; } = kind;
    public string Phase { get; } = phase;
    public string Code { get; } = code;
    public string Key { get; } = key;
    public double X { get; } = x;
    public double Y { get; } = y;
    public string Button { get; } = button;
}

internal sealed class GlobalInputCapture
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WmMouseMove = 0x0200;
    private const int WmLeftDown = 0x0201;
    private const int WmLeftUp = 0x0202;
    private const int WmRightDown = 0x0204;
    private const int WmRightUp = 0x0205;
    private const int WmMiddleDown = 0x0207;
    private const int WmMiddleUp = 0x0208;
    private const int WmWheel = 0x020A;

    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private HookProc? _keyboardProc;
    private HookProc? _mouseProc;
    private bool _active;
    private readonly HashSet<string> _heldKeys = new(StringComparer.Ordinal);

    public event Action<NativeInputEvent>? Input;

    public void Start()
    {
        if (_active) return;
        _keyboardProc = KeyboardCallback;
        _mouseProc = MouseCallback;
        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, GetModuleHandle(null), 0);
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, GetModuleHandle(null), 0);
        if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
        {
            Stop();
            throw new InvalidOperationException("Windows 无法安装全局键鼠监听钩子。");
        }
        _active = true;
    }

    public void Stop()
    {
        if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        _keyboardHook = _mouseHook = IntPtr.Zero;
        _heldKeys.Clear();
        _active = false;
    }

    private IntPtr KeyboardCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var message = wParam.ToInt32();
            if (message is WmKeyDown or WmSysKeyDown or WmKeyUp or WmSysKeyUp)
            {
                var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                var down = message is WmKeyDown or WmSysKeyDown;
                var (key, keyCode) = DescribeKey(data.VirtualKey, data.ScanCode, data.Flags);
                var changed = down ? _heldKeys.Add(keyCode) : _heldKeys.Remove(keyCode);
                if (!changed) return CallNextHookEx(_keyboardHook, code, wParam, lParam);
                Input?.Invoke(new NativeInputEvent("key", down ? "down" : "up", keyCode, key));
            }
        }
        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private IntPtr MouseCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var message = wParam.ToInt32();
            var data = Marshal.PtrToStructure<MsLlHookStruct>(lParam);
            var x = Math.Clamp(data.Point.X / SystemParameters.PrimaryScreenWidth, 0, 1);
            var y = Math.Clamp(data.Point.Y / SystemParameters.PrimaryScreenHeight, 0, 1);
            if (message == WmMouseMove)
                Input?.Invoke(new NativeInputEvent("mouse", "move", x: x, y: y));
            else if (message is WmLeftDown or WmLeftUp or WmRightDown or WmRightUp or WmMiddleDown or WmMiddleUp)
            {
                var button = message is WmLeftDown or WmLeftUp ? "left" :
                    message is WmRightDown or WmRightUp ? "right" : "middle";
                var down = message is WmLeftDown or WmRightDown or WmMiddleDown;
                Input?.Invoke(new NativeInputEvent("mouse", down ? "down" : "up", x: x, y: y, button: button));
            }
            else if (message == WmWheel)
            {
                var delta = unchecked((short)(data.MouseData >> 16));
                Input?.Invoke(new NativeInputEvent("mouse", delta >= 0 ? "wheel-up" : "wheel-down", x: x, y: y));
            }
        }
        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private static (string Key, string Code) DescribeKey(uint vk, uint scan, uint flags)
    {
        if (vk == 0x10) return scan == 0x36 ? ("Shift", "ShiftRight") : ("Shift", "ShiftLeft");
        if (vk == 0x11) return ("Control", (flags & 1) != 0 ? "ControlRight" : "ControlLeft");
        if (vk == 0x12) return ("Alt", (flags & 1) != 0 ? "AltRight" : "AltLeft");
        switch (vk)
        {
            case 0xA0: return ("Shift", "ShiftLeft");
            case 0xA1: return ("Shift", "ShiftRight");
            case 0xA2: return ("Control", "ControlLeft");
            case 0xA3: return ("Control", "ControlRight");
            case 0xA4: return ("Alt", "AltLeft");
            case 0xA5: return ("Alt", "AltRight");
            case 0x5B: return ("Meta", "MetaLeft");
            case 0x5C: return ("Meta", "MetaRight");
            case 0x20: return (" ", "Space");
            case 0x08: return ("Backspace", "Backspace");
            case 0x09: return ("Tab", "Tab");
            case 0x0D: return ("Enter", "Enter");
            case 0x14: return ("CapsLock", "CapsLock");
            case 0x1B: return ("Escape", "Escape");
            case 0x25: return ("ArrowLeft", "ArrowLeft");
            case 0x26: return ("ArrowUp", "ArrowUp");
            case 0x27: return ("ArrowRight", "ArrowRight");
            case 0x28: return ("ArrowDown", "ArrowDown");
        }
        if (vk is >= 0x41 and <= 0x5A)
        {
            var letter = (char)vk;
            return (char.ToLowerInvariant(letter).ToString(), "Key" + letter);
        }
        if (vk is >= 0x30 and <= 0x39)
        {
            var digit = (char)vk;
            return (digit.ToString(), "Digit" + digit);
        }
        if (vk is >= 0x70 and <= 0x87)
        {
            var number = vk - 0x6F;
            return ("F" + number, "F" + number);
        }
        return vk switch
        {
            0xBD => ("-", "Minus"), 0xBB => ("=", "Equal"), 0xDB => ("[", "BracketLeft"),
            0xDD => ("]", "BracketRight"), 0xDC => ("\\", "Backslash"), 0xBA => (";", "Semicolon"),
            0xDE => ("'", "Quote"), 0xBC => (",", "Comma"), 0xBE => (".", "Period"),
            0xBF => ("/", "Slash"), 0xC0 => ("`", "Backquote"),
            _ => ("", "Scan" + scan + ((flags & 1) != 0 ? "E0" : ""))
        };
    }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct { public uint VirtualKey; public uint ScanCode; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MsLlHookStruct { public Point Point; public uint MouseData; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern IntPtr GetModuleHandle(string? moduleName);
}

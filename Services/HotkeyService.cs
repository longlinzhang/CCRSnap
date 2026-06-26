using System.Windows;
using System.Windows.Interop;
using CCRSnap.Native;

namespace CCRSnap.Services;

public interface IHotkeyService
{
    event Action<int>? HotkeyPressed;
    bool Register(int id, uint modifiers, uint key);
    bool Unregister(int id);
    void Attach(Window window);
}

public class HotkeyService : IHotkeyService
{
    private Window? _window;
    private readonly Dictionary<int, (uint modifiers, uint key)> _registrations = new();
    private nint _hwnd;

    public event Action<int>? HotkeyPressed;

    public void Attach(Window window)
    {
        _window = window;
        _hwnd = new WindowInteropHelper(window).Handle;
        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);

        // Re-register all hotkeys
        foreach (var kv in _registrations)
            NativeMethods.RegisterHotKey(_hwnd, kv.Key, kv.Value.modifiers, kv.Value.key);
    }

    public bool Register(int id, uint modifiers, uint key)
    {
        _registrations[id] = (modifiers, key);
        if (_hwnd != nint.Zero)
            return NativeMethods.RegisterHotKey(_hwnd, id, modifiers, key);
        return true;
    }

    public bool Unregister(int id)
    {
        _registrations.Remove(id);
        if (_hwnd != nint.Zero)
            return NativeMethods.UnregisterHotKey(_hwnd, id);
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _registrations.Keys.ToList())
            Unregister(id);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            HotkeyPressed?.Invoke(id);
            handled = true;
        }
        return nint.Zero;
    }
}

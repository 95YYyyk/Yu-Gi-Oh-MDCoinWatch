using System.Runtime.InteropServices;

namespace MdCoinWatch;

/// <summary>界面用的 Win32/GDI 声明。没有 WinForms，全部自己画。</summary>
internal static class Ui32
{
    internal const int WS_POPUP = unchecked((int)0x80000000);
    internal const int WS_EX_TOPMOST = 0x00000008;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;
    internal const int WS_EX_LAYERED = 0x00080000;
    internal const int WS_EX_NOACTIVATE = 0x08000000;
    internal const int WS_EX_APPWINDOW = 0x00040000;

    internal const int GWL_EXSTYLE = -20;
    internal const int SWP_FRAMECHANGED = 0x0020;
    internal const int WM_SETICON = 0x0080;
    internal const int ICON_SMALL = 0;
    internal const int ICON_BIG = 1;

    internal const int WM_PAINT = 0x000F;
    internal const int WM_ERASEBKGND = 0x0014;
    internal const int WM_TIMER = 0x0113;
    internal const int WM_CLOSE = 0x0010;
    internal const int WM_DESTROY = 0x0002;
    internal const int WM_MOUSEMOVE = 0x0200;
    internal const int WM_LBUTTONDOWN = 0x0201;
    internal const int WM_LBUTTONUP = 0x0202;
    internal const int WM_RBUTTONUP = 0x0205;
    internal const int WM_NULL = 0x0000;
    internal const int WM_SETCURSOR = 0x0020;
    internal const int WM_DPICHANGED = 0x02E0;

    internal const int SW_SHOWNOACTIVATE = 4;
    internal const int SW_HIDE = 0;
    internal const int LWA_ALPHA = 0x00000002;
    internal const int HWND_TOPMOST = -1;
    internal const int SWP_NOACTIVATE = 0x0010;
    internal const int SWP_NOSIZE = 0x0001;
    internal const int SWP_NOMOVE = 0x0002;
    internal const int SRCCOPY = 0x00CC0020;

    internal const int DT_LEFT = 0x00000000;
    internal const int DT_SINGLELINE = 0x00000020;
    internal const int DT_VCENTER = 0x00000004;
    internal const int DT_NOPREFIX = 0x00000800;
    internal const int DT_CALCRECT = 0x00000400;
    internal const int DT_END_ELLIPSIS = 0x00008000;
    internal const int TRANSPARENT = 1;

    internal const int MF_STRING = 0x00000000;
    internal const int MF_GRAYED = 0x00000001;
    internal const int MF_DISABLED = 0x00000002;
    internal const int MF_SEPARATOR = 0x00000800;
    internal const int MF_CHECKED = 0x00000008;
    internal const int TPM_RIGHTBUTTON = 0x0002;
    internal const int TPM_RETURNCMD = 0x0100;

    internal const int IDC_ARROW = 32512;
    internal const int IDC_SIZEWE = 32644;
    internal const int IDC_SIZENS = 32645;
    internal const int IDC_SIZENWSE = 32642;
    internal const int IDC_SIZENESW = 32643;
    internal const int IDC_SIZEALL = 32646;

    internal static readonly nint HWND_TOP = nint.Zero;

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSG
    {
        public nint hwnd; public int message; public nint wParam, lParam; public int time; public Win32.POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PAINTSTRUCT
    {
        public nint hdc; public int fErase; public Win32.RECT rcPaint;
        public int fRestore, fIncUpdate;
        public long r1, r2, r3, r4;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WNDCLASSEXW
    {
        public int cbSize, style;
        public nint lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public nint hInstance, hIcon, hCursor, hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CHOOSECOLORW
    {
        public int lStructSize;
        public nint hwndOwner, hInstance;
        public int rgbResult;
        public nint lpCustColors;
        public int Flags, lCustData;
        public nint lpfnHook;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpTemplateName;
    }

    internal delegate nint WndProc(nint hwnd, int msg, nint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ushort RegisterClassExW(ref WNDCLASSEXW wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateWindowExW(int exStyle, string cls, string title, int style,
        int x, int y, int w, int h, nint parent, nint menu, nint inst, nint param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint DefWindowProcW(nint hwnd, int msg, nint wParam, nint lParam);
    [DllImport("user32.dll")] internal static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint hwnd, int cmd);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(nint hwnd, int index, nint value);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(nint hwnd, int index, int value);
    /// <summary>32 位进程里 user32 没有 SetWindowLongPtrW，得退回 SetWindowLongW。</summary>
    internal static nint SetWindowLongPtr(nint hwnd, int index, nint value)
        => IntPtr.Size == 8 ? SetWindowLongPtr64(hwnd, index, value) : new nint(SetWindowLong32(hwnd, index, (int)value));
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, int flags);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd, out Win32.RECT r);
    [DllImport("user32.dll")] internal static extern bool SetLayeredWindowAttributes(nint hwnd, uint key, byte alpha, int flags);
    [DllImport("user32.dll")] internal static extern int SetWindowRgn(nint hwnd, nint rgn, bool redraw);
    [DllImport("user32.dll")] internal static extern nint BeginPaint(nint hwnd, out PAINTSTRUCT ps);
    [DllImport("user32.dll")] internal static extern bool EndPaint(nint hwnd, ref PAINTSTRUCT ps);
    [DllImport("user32.dll")] internal static extern bool InvalidateRect(nint hwnd, nint rect, bool erase);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int DrawTextW(nint hdc, string text, int len, ref Win32.RECT r, int flags);
    [DllImport("user32.dll")] internal static extern int FillRect(nint hdc, ref Win32.RECT r, nint brush);
    [DllImport("user32.dll")] internal static extern int FrameRect(nint hdc, ref Win32.RECT r, nint brush);
    [DllImport("user32.dll")] internal static extern nint SetCapture(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
    [DllImport("user32.dll")] internal static extern nint SetTimer(nint hwnd, nint id, int ms, nint proc);
    [DllImport("user32.dll")] internal static extern bool KillTimer(nint hwnd, nint id);
    [DllImport("user32.dll")] internal static extern int GetMessageW(out MSG msg, nint hwnd, int min, int max);
    [DllImport("user32.dll")] internal static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] internal static extern nint DispatchMessageW(ref MSG msg);
    [DllImport("user32.dll")] internal static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] internal static extern bool PostMessageW(nint hwnd, int msg, nint w, nint l);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] internal static extern int ReleaseDC(nint hwnd, nint hdc);
    [DllImport("user32.dll")] internal static extern nint LoadCursorW(nint inst, nint name);
    [DllImport("user32.dll")] internal static extern nint SetCursor(nint cursor);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int MessageBoxW(nint hwnd, string text, string caption, uint type);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool SystemParametersInfoW(uint action, uint param, ref Win32.RECT rect, uint winIni);
    [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Win32.POINT p);
    [DllImport("user32.dll")] internal static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint CreatePopupMenu();
    [DllImport("user32.dll")] internal static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool AppendMenuW(nint menu, int flags, nint id, string text);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int TrackPopupMenuEx(nint menu, int flags, int x, int y, nint hwnd, nint param);

    [DllImport("gdi32.dll")] internal static extern nint CreateCompatibleDC(nint hdc);
    [DllImport("gdi32.dll")] internal static extern bool DeleteDC(nint hdc);
    [DllImport("gdi32.dll")] internal static extern nint CreateCompatibleBitmap(nint hdc, int w, int h);
    [DllImport("gdi32.dll")] internal static extern nint SelectObject(nint hdc, nint obj);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] internal static extern bool BitBlt(nint d, int x, int y, int w, int h, nint s, int sx, int sy, int rop);
    [DllImport("gdi32.dll")] internal static extern nint CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] internal static extern nint CreatePen(int style, int width, uint color);
    [DllImport("gdi32.dll")] internal static extern bool MoveToEx(nint hdc, int x, int y, nint old);
    [DllImport("gdi32.dll")] internal static extern bool LineTo(nint hdc, int x, int y);
    [DllImport("gdi32.dll")] internal static extern bool Ellipse(nint hdc, int l, int t, int r, int b);
    [DllImport("gdi32.dll")] internal static extern nint CreateFontW(int h, int w, int esc, int orient, int weight,
        uint italic, uint underline, uint strike, uint charset, uint prec, uint clip, uint quality, uint pitch, string face);
    [DllImport("gdi32.dll")] internal static extern int SetBkMode(nint hdc, int mode);
    [DllImport("gdi32.dll")] internal static extern uint SetTextColor(nint hdc, uint color);

    [DllImport("gdi32.dll")] internal static extern int GetDeviceCaps(nint hdc, int index);
    [DllImport("gdi32.dll")] internal static extern nint CreateRoundRectRgn(int l, int t, int r, int b, int ew, int eh);
    [DllImport("gdi32.dll")]
    internal static extern nint CreateDIBSection(nint hdc, ref Win32.BITMAPINFO bmi, uint usage, out nint bits, nint section, uint offset);

    [DllImport("msimg32.dll")]
    internal static extern bool AlphaBlend(nint dst, int dx, int dy, int dw, int dh,
        nint src, int sx, int sy, int sw, int sh, BLENDFUNCTION bf);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool ChooseColorW(ref CHOOSECOLORW cc);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint ExtractIconW(nint hInst, string exeFileName, uint iconIndex);

    [DllImport("user32.dll")] internal static extern bool DestroyIcon(nint icon);

    internal static uint Rgb(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));
}

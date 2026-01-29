using System.Runtime.InteropServices;

namespace TrRebootTools.Shared.Util
{
    internal static class Win32
    {
        [DllImport("user32")]
        public static extern int MessageBoxW(
            nint hWnd,
            [MarshalAs(UnmanagedType.LPWStr)] string text,
            [MarshalAs(UnmanagedType.LPWStr)] string caption,
            int type
        );

        public const int MB_OK = 0;
        public const int MB_OKCANCEL = 1;
        public const int MB_YESNOCANCEL = 3;
        public const int MB_YESNO = 4;

        public const int MB_ICONQUESTION = 0x20;
        public const int MB_ICONEXCLAMATION = 0x30;
        public const int MB_ICONINFORMATION = 0x40;

        public const int IDOK = 1;
        public const int IDCANCEL = 2;
        public const int IDYES = 6;
        public const int IDNO = 7;
    }
}

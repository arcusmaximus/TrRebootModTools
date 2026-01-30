using Avalonia.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Threading.Tasks;

namespace TrRebootTools.Shared.Util
{
    public static class MessageBox
    {
        public static async Task<ButtonResult> ShowAsync(
            string title,
            string message,
            ButtonEnum buttons = ButtonEnum.Ok,
            Icon icon = Icon.None)
        {
            if (OperatingSystem.IsWindows())
            {
                int type = 0;
                type |= buttons switch
                {
                    ButtonEnum.Ok           => Win32.MB_OK,
                    ButtonEnum.OkCancel     => Win32.MB_OKCANCEL,
                    ButtonEnum.YesNo        => Win32.MB_YESNO,
                    ButtonEnum.YesNoCancel  => Win32.MB_YESNOCANCEL,
                    _ => throw new NotSupportedException()
                };
                type |= icon switch
                {
                    Icon.None       => 0,
                    Icon.Info       => Win32.MB_ICONINFORMATION,
                    Icon.Question   => Win32.MB_ICONQUESTION,
                    Icon.Error      => Win32.MB_ICONEXCLAMATION,
                    _ => throw new NotSupportedException()
                };
                nint ownerHandle = App.WindowStack.Peek().TryGetPlatformHandle()?.Handle ?? 0;
                int result = Win32.MessageBoxW(ownerHandle, message, title, type);
                return result switch
                {
                    Win32.IDOK      => ButtonResult.Ok,
                    Win32.IDCANCEL  => ButtonResult.Cancel,
                    Win32.IDYES     => ButtonResult.Yes,
                    Win32.IDNO      => ButtonResult.No,
                    _ => throw new NotSupportedException()
                };
            }
            else
            {
                var msgBox = MessageBoxManager.GetMessageBoxStandard(title, message, buttons, icon);
                Window owner = App.WindowStack.Peek();
                if (owner.IsVisible)
                    return await msgBox.ShowWindowDialogAsync(owner);
                else
                    return await msgBox.ShowAsync();
            }
        }

        public static async Task ShowErrorAsync(Exception exception)
        {
            await ShowErrorAsync(exception.Message);
        }

        public static async Task ShowErrorAsync(string message)
        {
            await ShowAsync("Error", message, icon: Icon.Error);
        }
    }
}

using GeneralUpdate.Infrastructure.DataServices.Pick;
using WindowsFolderPicker = Windows.Storage.Pickers.FolderPicker;

namespace GeneralUpdate.PacketTool.Platforms.Windows
{
    public class FolderPicker : IFolderPickerService
    {
        public async Task<string> PickFolderTaskAsync()
        {
            var folderPicker = new WindowsFolderPicker();
            // Might be needed to make it work on Windows 10
            folderPicker.FileTypeFilter.Add("*");

            // Get the current window's HWND by passing in the Window object
            var hwnd = ((MauiWinUIWindow)App.Current.Windows[0].Handler.PlatformView).WindowHandle;

            // Associate the HWND with the file picker
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var result = await folderPicker.PickSingleFolderAsync();

            return result?.Path;
        }
    }
}

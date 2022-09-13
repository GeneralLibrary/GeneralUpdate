using CommunityToolkit.Mvvm.Input;
using GeneralUpdate.Infrastructure.DataServices.Pick;
using GeneralUpdate.Infrastructure.MVVM;
using GeneralUpdate.PacketTool.Services;

namespace GeneralUpdate.PacketTool.ViewModels
{
    public class MainViewModel : ViewModeBase
    {
        #region Private Members

        private string sourcePath, targetPath, patchPath, infoMessage, url;
        private bool isPublish;
        private AsyncRelayCommand editCommand;
        private AsyncRelayCommand buildCommand;
        private AsyncRelayCommand<string> selectFolderCommand;
        private readonly IFolderPickerService _folderPickerService;
        private MainService _mainService;
        
        #endregion

        #region Constructors

        public MainViewModel(IFolderPickerService folderPickerService)
        {
            _folderPickerService = folderPickerService;
            _mainService = new MainService();
            IsPublish = false;
        }

        #endregion

        #region Public Properties

        public string SourcePath { get => sourcePath; set => SetProperty(ref sourcePath, value); }
        public string TargetPath { get => targetPath; set => SetProperty(ref targetPath, value); }
        public string PatchPath { get => patchPath; set => SetProperty(ref patchPath, value); }
        public string InfoMessage { get => infoMessage; set => SetProperty(ref infoMessage, value); }
        public bool IsPublish { get => isPublish; set => SetProperty(ref isPublish, value); }
        public string Url { get => url; set => SetProperty(ref url, value); }

        public AsyncRelayCommand<string> SelectFolderCommand
        {
            get => selectFolderCommand ?? (selectFolderCommand = new AsyncRelayCommand<string>(SelectFolderAction));
        }

        public AsyncRelayCommand BuildCommand
        {
            get => buildCommand ?? (buildCommand = new AsyncRelayCommand(BuildPacketCallback));
        }
        public AsyncRelayCommand EditCommand
        {
            get => editCommand ?? (editCommand = new AsyncRelayCommand(EditCallback));
        }

        private async Task EditCallback()
        {
            await Shell.Current.GoToAsync("DifferentPage");
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Choose a path
        /// </summary>
        /// <param name="value"></param>
        private async Task SelectFolderAction(string value)
        {
            var pickerResult = await _folderPickerService.PickFolderTaskAsync();
            if (pickerResult == null)
            {
                await Shell.Current.DisplayAlert("Pick options", "No results were selected !", "ok");
                return;
            }
            switch (value)
            {
                case "Source":
                    SourcePath = pickerResult;
                    break;
                case "Target":
                    TargetPath = pickerResult;
                    break;
                case "Patch":
                    PatchPath = pickerResult;
                    break;
            }
        }

        /// <summary>
        ///  Build patch package
        /// </summary>
        private async Task BuildPacketCallback()
        {
            if (ValidationParameters())
            {
                await Shell.Current.DisplayAlert("Build options", "The path is not set or the folder does not exist !", "ok");
                return;
            }
            try
            {
                //await DifferentialCore.Instance.Clean(SourcePath, TargetPath, PatchPath, (sender, args) =>
                //{
                //    InfoMessage += $"{args.Name} - {args.Path}" + "\r\n";
                //});
                //If upload is checked, the differential package will be uploaded to the file server,
                //and the file server will insert the information of the update package after receiving it.
                if (IsPublish)
                {
                    var directoryInfo = new DirectoryInfo(TargetPath);
                    var fileArray = directoryInfo.GetFiles();
                    var findPacket = fileArray.FirstOrDefault(f => f.Extension.Equals(".zip"));
                    if (findPacket == null) return;
                    await _mainService.PostUpgradPakcet<string>("",null);
                    InfoMessage += $" Update package found under {TargetPath}  path , file full name {findPacket.Name} ." + "\r\n";
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Build options", $"Operation failed : {TargetPath} , Error : {ex.Message}  !", "ok");
            }
        }

        private bool ValidationParameters() => (string.IsNullOrEmpty(SourcePath) || string.IsNullOrEmpty(TargetPath) || string.IsNullOrEmpty(PatchPath) ||
                !Directory.Exists(SourcePath) || !Directory.Exists(TargetPath) || !Directory.Exists(PatchPath));

        #endregion
    }
}

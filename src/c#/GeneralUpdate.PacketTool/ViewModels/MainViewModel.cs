using CommunityToolkit.Mvvm.Input;
using GeneralUpdate.Differential;
using GeneralUpdate.Infrastructure.Config;
using GeneralUpdate.Infrastructure.DataServices.Pick;
using GeneralUpdate.Infrastructure.MVVM;
using GeneralUpdate.PacketTool.Services;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace GeneralUpdate.PacketTool.ViewModels
{
    public class MainViewModel : ViewModeBase
    {
        #region Private Members

        private string sourcePath, targetPath, patchPath, infoMessage, url, packetName;
        private List<string> _formats, _encodings;
        private string _currentFormat, _currentEncoding;
        private bool isPublish;
        private AsyncRelayCommand editCommand;
        private AsyncRelayCommand buildCommand;
        private AsyncRelayCommand<string> selectFolderCommand;
        private readonly IFolderPickerService _folderPickerService;
        private MainService _mainService;
        private IConfiguration _configuration;

        #endregion

        #region Constructors

        public MainViewModel(IFolderPickerService folderPickerService, IConfiguration config)
        {
            _folderPickerService = folderPickerService;
            _configuration = config;
            _mainService = new MainService();
            IsPublish = false;
            CurrentEncoding = Encodings.First();
            CurrentFormat = Formats.First();
        }

        #endregion

        #region Public Properties

        public string SourcePath { get => sourcePath; set => SetProperty(ref sourcePath, value); }
        public string TargetPath { get => targetPath; set => SetProperty(ref targetPath, value); }
        public string PatchPath { get => patchPath; set => SetProperty(ref patchPath, value); }
        public string InfoMessage { get => infoMessage; set => SetProperty(ref infoMessage, value); }
        public bool IsPublish { get => isPublish; set => SetProperty(ref isPublish, value); }
        public string Url { get => url; set => SetProperty(ref url, value); }
        public string PacketName { get => url; set => SetProperty(ref packetName, value); }

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
        public List<string> Formats 
        {
            get 
            {
                if (_formats == null)
                {
                    _formats = new List<string>();
                    _formats.Add("ZIP");
                    _formats.Add("7Z");
                }
                return _formats;
            }
        }

        public List<string> Encodings 
        {
            get 
            {
                if (_currentEncoding == null) 
                {
                    _encodings = new List<string>();
                    _encodings.Add("Default");
                    _encodings.Add("UTF8");
                    _encodings.Add("UTF7");
                    _encodings.Add("Unicode");
                    _encodings.Add("UTF32");
                    _encodings.Add("BigEndianUnicode");
                    _encodings.Add("Latin1");
                    _encodings.Add("ASCII");
                }
                return _encodings;
            }
        }

        public string CurrentFormat { 
            get => _currentFormat;
            set => SetProperty(ref _currentFormat, value); 
        }

        public string CurrentEncoding
        {
            get => _currentEncoding; 
            set => SetProperty(ref _currentEncoding, value);
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
                await DifferentialCore.Instance.Clean(SourcePath, TargetPath, PatchPath, (sender, args) =>{},
                    String2OperationType(CurrentFormat),String2Encoding(CurrentEncoding), PacketName);
                //If upload is checked, the differential package will be uploaded to the file server,
                //and the file server will insert the information of the update package after receiving it.
                if (IsPublish)
                {
                    var directoryInfo = new DirectoryInfo(TargetPath);
                    var fileArray = directoryInfo.GetFiles();
                    var findPacket = fileArray.FirstOrDefault(f => f.Extension.Equals(".zip"));
                    if (findPacket == null) return;
                    //TODO:TEST
                    await _mainService.PostUpgradPakcet<string>(Path.Combine(TargetPath, PacketName),async (resp) => 
                    {
                        await Shell.Current.DisplayAlert("Build options", $"Release success!", "ok");
                    });
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Build options", $"Operation failed : {TargetPath} , Error : {ex.Message}  !", "ok");
            }
        }

        private bool ValidationParameters() => (string.IsNullOrEmpty(SourcePath) || string.IsNullOrEmpty(TargetPath) || string.IsNullOrEmpty(PatchPath) ||
                !Directory.Exists(SourcePath) || !Directory.Exists(TargetPath) || !Directory.Exists(PatchPath));

        private Encoding String2Encoding(string encoding)
        {
            Encoding result = null;
            switch (encoding)
            {
                case "Default":
                    result = Encoding.Default;
                    break;
                case "UTF8":
                    result = Encoding.UTF8;
                    break;
                case "UTF7":
                    result = Encoding.UTF7;
                    break;
                case "Unicode":
                    result = Encoding.Unicode;
                    break;
                case "UTF32":
                    result = Encoding.UTF32;
                    break;
                case "BigEndianUnicode":
                    result = Encoding.BigEndianUnicode;
                    break;
                case "Latin1":
                    result = Encoding.Latin1;
                    break;
                case "ASCII":
                    result = Encoding.ASCII;
                    break;
            }
            return result;
        }

        private Zip.Factory.OperationType String2OperationType(string type)
        {
            var result = Zip.Factory.OperationType.GZip;
            switch (type)
            {
                case "ZIP":
                    result = Zip.Factory.OperationType.GZip;
                    break;
                case "7Z":
                    result = Zip.Factory.OperationType.G7z;
                    break;
            }
            return result;
        }
        #endregion
    }
}

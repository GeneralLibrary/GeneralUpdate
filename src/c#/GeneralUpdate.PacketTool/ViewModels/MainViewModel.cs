﻿using CommunityToolkit.Mvvm.Input;
using GeneralUpdate.Differential;
using GeneralUpdate.Infrastructure.DataServices.Pick;
using GeneralUpdate.Infrastructure.MVVM;
using GeneralUpdate.PacketTool.Services;
using GeneralUpdate.Zip.Factory;
using System.Text;

namespace GeneralUpdate.PacketTool.ViewModels
{
    public class MainViewModel : ViewModeBase
    {
        #region Private Members

        private string sourcePath, targetPath, patchPath, infoMessage, url, packetName;
        private List<string> _formats, _encodings,_appTypes;
        private string _currentFormat, _currentEncoding, _currnetAppType, _currentVersion, _currentClientAppKey;
        private bool isPublish;
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
            CurrentEncoding = Encodings.First();
            CurrentFormat = Formats.First();
            CurrnetAppType = AppTypes.First();
        }

        #endregion

        #region Public Properties

        public string SourcePath { get => sourcePath; set => SetProperty(ref sourcePath, value); }
        public string TargetPath { get => targetPath; set => SetProperty(ref targetPath, value); }
        public string PatchPath { get => patchPath; set => SetProperty(ref patchPath, value); }
        public string InfoMessage { get => infoMessage; set => SetProperty(ref infoMessage, value); }
        public bool IsPublish { get => isPublish; set => SetProperty(ref isPublish, value); }
        public string Url { get => url; set => SetProperty(ref url, value); }
        public string PacketName { get => packetName; set => SetProperty(ref packetName, value); }

        public AsyncRelayCommand<string> SelectFolderCommand
        {
            get => selectFolderCommand ?? (selectFolderCommand = new AsyncRelayCommand<string>(SelectFolderAction));
        }

        public AsyncRelayCommand BuildCommand
        {
            get => buildCommand ?? (buildCommand = new AsyncRelayCommand(BuildPacketCallback));
        }

        public List<string> AppTypes
        {
            get 
            {
                if (_appTypes == null)
                {
                    _appTypes = new List<string>();
                    _appTypes.Add("Client");
                    _appTypes.Add("Upgrade");
                }
                return _appTypes;
            }
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

        public string CurrnetAppType 
        { 
            get => _currnetAppType; 
            set => SetProperty(ref _currnetAppType, value); 
        }

        public string CurrentVersion { get => _currentVersion; set => SetProperty(ref _currentVersion, value); }
        public string CurrentClientAppKey { get => _currentClientAppKey; set => SetProperty(ref _currentClientAppKey, value); }

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
            //var packetPath1 = Path.Combine(TargetPath, PacketName);
            //await _mainService.PostUpgradPakcet<string>(packetPath1, String2AppType(CurrnetAppType), CurrentVersion, CurrentClientAppKey,"", async (resp) =>
            //{
            //    await Shell.Current.DisplayAlert("Build options", $"Release success!", "ok");
            //});

            if (ValidationParameters())
            {
                await Shell.Current.DisplayAlert("Build options", "Required field not filled !", "ok");
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
                    var packetPath = Path.Combine(TargetPath,PacketName);
                    if (!File.Exists(packetPath)) await Shell.Current.DisplayAlert("Build options", $"The package was not found in the following path {packetPath} !", "cancel");
                    //TODO:TEST
                    await _mainService.PostUpgradPakcet<string>(packetPath, String2AppType(CurrnetAppType), CurrentVersion,CurrentClientAppKey,"", async (resp) =>
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
                !Directory.Exists(SourcePath) || !Directory.Exists(TargetPath) || !Directory.Exists(PatchPath) || string.IsNullOrEmpty(CurrentVersion) || string.IsNullOrEmpty(Url)
            || string.IsNullOrEmpty(CurrentClientAppKey));

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

        private OperationType String2OperationType(string type)
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

        private int String2AppType(string appType)
        {
            int result = 0;
            switch (appType)
            {
                case "Client":
                    result = 1;
                    break;
                case "UTF8":
                    result = 2;
                    break;
            }
            return result;
        }

        #endregion
    }
}

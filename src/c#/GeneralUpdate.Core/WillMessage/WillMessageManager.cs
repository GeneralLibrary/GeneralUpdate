using GeneralUpdate.Core.Domain.PO;
using GeneralUpdate.Core.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeneralUpdate.Core.WillMessage
{
    internal class WillMessageManager
    {
        #region Private Members

        internal const string DEFULT_WILL_MESSAGE_DIR = @"C:\generalupdate_willmessages";
        internal const string DEFULT_WILL_MESSAGE_FILE = "will_message.json";

        internal const string BACKUP_ROOT_PATH = @"C:\generalupdate_backup";
        private string _packetPath;
        private string _appPath;
        private string _backupPath;
        private Stack<BackupPO> _backupStack = new Stack<BackupPO>();

        private string _willMessageFile;
        private WillMessagePO _willMessage;
        private bool _isFirstTime = true;

        private static WillMessageManager _instance;
        private readonly static object _instanceLock = new object();

        #endregion

        #region Constructors

        private WillMessageManager() { }

        #endregion

        #region Public Properties

        internal static WillMessageManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new WillMessageManager();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Public Methods

        internal WillMessagePO GetWillMessage(string path = null)
        {
            _willMessageFile = string.IsNullOrWhiteSpace(path) ? GetWillMessagePath() : path;
            return _willMessage = FileUtil.GetJson<WillMessagePO>(_willMessageFile);
        }

        internal void Clear()
        {
            _packetPath = null;
            _appPath = null;
            _backupPath = null;
            _willMessage = null;
            _willMessageFile = null;
            _backupStack.Clear();
            FileUtil.DeleteDir(DEFULT_WILL_MESSAGE_DIR);
            FileUtil.DeleteDir(BACKUP_ROOT_PATH);
        }

        internal void Backup(string appPath, string packetPath, string version,int appType)
        {
            if (!Directory.Exists(BACKUP_ROOT_PATH))
                Directory.CreateDirectory(BACKUP_ROOT_PATH);

            var versionDir = Path.Combine(BACKUP_ROOT_PATH, version, appType == 1 ? "ClientApp" : "UpgradeApp");
            if (!Directory.Exists(versionDir))
                Directory.CreateDirectory(versionDir);

            _appPath = appPath;
            _packetPath = packetPath;
            _backupPath = versionDir;
            ProcessDirectory(_packetPath, _appPath, _backupPath);
            _backupStack.Push(new BackupPO { Version = version,  AppType = appType, AppPath = _appPath, BackupPath = _backupPath });
        }

        internal void Restore()
        {
            if (_willMessage == null || _willMessage.Message == null) return;
            while (_willMessage.Message.Any())
            {
                var message = _willMessage.Message.Pop();
                _appPath = message.AppPath;
                _backupPath = message.BackupPath;
                ProcessDirectory(_backupPath, _backupPath, _appPath);
            }
        }

        internal void Builder() 
        {
            if (!_backupStack.Any()) return;

            _willMessage = new WillMessagePO.Builder()
                                               .SetMessage(_backupStack)
                                               .SetStatus(WillMessageStatus.NotStarted)
                                               .SetCreateTime(DateTime.Now)
                                               .SetChangeTime(DateTime.Now)
                                               .Build();
            FileUtil.CreateJson(Path.Combine(DEFULT_WILL_MESSAGE_DIR, DateTime.Now.ToString("yyyyMMdd")), DEFULT_WILL_MESSAGE_FILE, _willMessage);
        }

        internal void Check()
        {
            var message = GetWillMessage();
            if (message == null) return;
            if (_isFirstTime && message?.Status == WillMessageStatus.NotStarted)
            {
                Restore();
                _isFirstTime = false;
                return;
            }

            switch (message?.Status)
            {
                case WillMessageStatus.NotStarted:
                    return;
                case WillMessageStatus.Failed:
                    Restore();
                    break;
                case WillMessageStatus.Completed:
                    Clear();
                    break;
            }
        }

        #endregion

        #region Private Methods

        private string GetWillMessagePath() => Path.Combine(DEFULT_WILL_MESSAGE_DIR, DateTime.Now.ToString("yyyyMMdd"), DEFULT_WILL_MESSAGE_FILE);

        private void ProcessDirectory(string targetDirectory, string basePath, string destPath)
        {
            var fileNames = Directory.GetFiles(targetDirectory);
            foreach (string fileName in fileNames)
            {
                ProcessFile(fileName, basePath, destPath);
            }

            var subdirectoryEntries = Directory.GetDirectories(targetDirectory);
            foreach (string subdirectory in subdirectoryEntries)
            {
                ProcessDirectory(subdirectory, basePath, destPath);
            }
        }

        private void ProcessFile(string path, string basePath, string destPath)
        {
            var relativePath = GetRelativePath(basePath, path);
            var sourceFilePath = Path.Combine(basePath, relativePath);
            var destFilePath = Path.Combine(destPath, relativePath);

            if (File.Exists(sourceFilePath))
            {
                var destDirPath = Path.GetDirectoryName(destFilePath);
                if (!Directory.Exists(destDirPath))
                {
                    Directory.CreateDirectory(destDirPath);
                }

                File.Copy(sourceFilePath, destFilePath, true);
            }
        }

        private string GetRelativePath(string fromPath, string toPath)
        {
            var fromUri = new Uri(fromPath);
            var toUri = new Uri(toPath);

            var relativeUri = fromUri.MakeRelativeUri(toUri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            return relativePath.Replace('/', Path.DirectorySeparatorChar);
        }

        #endregion
    }
}

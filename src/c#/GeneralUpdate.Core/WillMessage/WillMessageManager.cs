using GeneralUpdate.Core.ContentProvider;
using GeneralUpdate.Core.Domain.PO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeneralUpdate.Core.WillMessage
{
    public class WillMessageManager
    {
        #region Private Members

        private string TempPath = Path.GetTempPath();
        public const string DEFULT_WILL_MESSAGE_DIR = "generalupdate_willmessages";
        public const string BACKUP_DIR = "generalupdate_backup";
        public const string DEFULT_WILL_MESSAGE_FILE = "will_message.json";

        private string _packetPath;
        private string _appPath;
        private string _backupPath;
        private readonly Stack<BackupPO> _backupStack = new Stack<BackupPO>();

        private string _willMessageFile;
        private WillMessagePO _willMessage;
        private bool _isFirstTime = true;

        private static WillMessageManager _instance;
        private static readonly object _instanceLock = new object();

        #endregion Private Members

        #region Constructors

        private WillMessageManager()
        { }

        #endregion Constructors

        #region Public Properties

        public static WillMessageManager Instance
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

        #endregion Public Properties

        #region Public Methods

        public WillMessagePO GetWillMessage(string path = null)
        {
            _willMessageFile = string.IsNullOrWhiteSpace(path) ? GetWillMessagePath() : path;
            return _willMessage = FileProvider.GetJson<WillMessagePO>(_willMessageFile);
        }

        public void Clear()
        {
            _packetPath = null;
            _appPath = null;
            _backupPath = null;
            _willMessage = null;
            _willMessageFile = null;
            _backupStack.Clear();
            FileProvider.DeleteDir(DEFULT_WILL_MESSAGE_DIR);
            FileProvider.DeleteDir(GetBackupPath());
        }

        public void Backup(string appPath, string packetPath, string version, string hash, int appType)
        {
            if (!Directory.Exists(GetBackupPath()))
                Directory.CreateDirectory(GetBackupPath());

            var versionDir = Path.Combine(GetBackupPath(), version, appType == 1 ? "ClientApp" : "UpgradeApp");
            if (!Directory.Exists(versionDir))
                Directory.CreateDirectory(versionDir);

            _appPath = appPath;
            _packetPath = packetPath;
            _backupPath = versionDir;
            ProcessDirectory(_packetPath, _appPath, _backupPath);
            _backupStack.Push(new BackupPO { Version = version, AppType = appType, AppPath = _appPath, BackupPath = _backupPath, Hash = hash });
        }

        public void Restore()
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

        public void Builder()
        {
            if (!_backupStack.Any()) return;

            _willMessage = new WillMessagePO.Builder()
                                               .SetMessage(_backupStack)
                                               .SetStatus(WillMessageStatus.NotStarted)
                                               .SetCreateTime(DateTime.Now)
                                               .SetChangeTime(DateTime.Now)
                                               .Build();
            FileProvider.CreateJson(GetWillMessagePath(), _willMessage);
        }

        public void Check()
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

        #endregion Public Methods

        #region Private Methods

        private string GetWillMessagePath() =>
            Path.Combine(TempPath, DEFULT_WILL_MESSAGE_DIR, DateTime.Now.ToString("yyyyMMdd"), DEFULT_WILL_MESSAGE_FILE);

        private string GetBackupPath() =>
            Path.Combine(TempPath, BACKUP_DIR);

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

        #endregion Private Methods
    }
}
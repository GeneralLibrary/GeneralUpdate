using GeneralUpdate.Core.Domain.PO;
using GeneralUpdate.Core.Utils;
using GeneralUpdate.Differential.ContentProvider;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.WillMessage
{
    internal class WillMessageManager
    {
        #region Private Members

        internal const string DEFULT_WILL_MESSAGE_DIR = @"C:\generalupdate_willmessages";
        internal const string DEFULT_WILL_MESSAGE_FILE = "will_message.json";
        internal const string BACKUP_ROOT_PATH = @"C:\generalupdate_backup";

        private string _willMessageFile;
        private WillMessagePO _willMessage;
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
            _willMessageFile = string.IsNullOrWhiteSpace(path) ? GetFilePath() : path;
            return _willMessage = FileUtil.ReadJsonFile<WillMessagePO>(_willMessageFile);
        }

        internal void Clear()
        {
            FileUtil.DeleteFile(_willMessageFile);
            _willMessage = null;
            DeleteRootDir();
        }

        internal async Task<List<BackupPO>> Backup(string appPath, string packetPath, string version,int appType)
        {
            if (!Directory.Exists(BACKUP_ROOT_PATH))
                Directory.CreateDirectory(BACKUP_ROOT_PATH);

            var versionDir = Path.Combine(BACKUP_ROOT_PATH, version);
            if (!Directory.Exists(versionDir))
                Directory.CreateDirectory(versionDir);

            //Take the left tree as the center to match the files that are not in the right tree .
            var fileProvider = new FileProvider();
            var nodes = await fileProvider.Compare(appPath, packetPath);
            var backups = new List<BackupPO>();
            foreach (var node in nodes.Item3) 
            {
                backups.Add(new BackupPO { Name = node.Name , Version = version , AppType = appType, InstallPath = node.Path , BackupPath = versionDir  });
            }
            return backups;
        }

        private void BuilderWillMessage() 
        {
            _willMessage = new WillMessagePO();
            _willMessage.ChangeTime = DateTime.Now;
            _willMessage.CreateTime = DateTime.Now;
            _willMessage.Status = WillMessageStatus.NotStarted;
        }

        #endregion

        #region Private Methods

        private string GetFilePath() => Path.Combine(DEFULT_WILL_MESSAGE_DIR, $"{DateTime.Now.ToString("yyyyMMdd")}_{DEFULT_WILL_MESSAGE_FILE}");

        private void Create(WillMessagePO willMessage)
        {
            if (willMessage == null) return;
            _willMessage = willMessage;
            FileUtil.CreateJsonFile(DEFULT_WILL_MESSAGE_DIR, $"{DateTime.Now.ToString("yyyyMMdd")}_{DEFULT_WILL_MESSAGE_FILE}", willMessage);
        }

        /// <summary>
        /// Delete the backup file directory and recursively delete all backup content.
        /// </summary>
        private void DeleteRootDir()
        {
            if (string.IsNullOrWhiteSpace(BACKUP_ROOT_PATH)) return;
            if (Directory.Exists(BACKUP_ROOT_PATH))
                Directory.Delete(BACKUP_ROOT_PATH, true);
        }

        #endregion
    }
}

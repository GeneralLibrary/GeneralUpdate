namespace GeneralUpdate.Core.Domain.Enum
{
    public enum ProgressType
    {
        /// <summary>
        /// Check for updates
        /// </summary>
        Check,

        /// <summary>
        /// Download the update package
        /// </summary>
        Download,

        /// <summary>
        /// 更新文件
        /// </summary>
        Updatefile,

        /// <summary>
        /// update completed
        /// </summary>
        Done,

        /// <summary>
        /// Update failed
        /// </summary>
        Fail,

        /// <summary>
        /// Update config
        /// </summary>
        Config,

        /// <summary>
        /// Update patch
        /// </summary>
        Patch,

        /// <summary>
        /// MD5 code
        /// </summary>
        MD5
    }
}
using System;

namespace GeneralUpdate.Core.Domain.DO
{
    public class VersionConfigDO
    {
        /// <summary>
        /// Product branch ID (Used to distinguish multiple branches under the same product).
        /// </summary>
        public string Guid { get; set; }

        /// <summary>
        /// Update package download location.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// MD5 verification code
        /// </summary>
        public string MD5 { get; set; }

        /// <summary>
        /// Update the package name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Update the package file format.
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// The version number that will be updated.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Update package release time.
        /// </summary>
        public long PubTime { get; set; }

        /// <summary>
        /// Init version config infomation.
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="url"></param>
        /// <param name="mD5"></param>
        /// <param name="name"></param>
        /// <param name="format"></param>
        /// <param name="version"></param>
        /// <param name="pubTime"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public VersionConfigDO(string guid, string url, string mD5, string name, string format, string version, long pubTime)
        {
            Guid = guid ?? throw new ArgumentNullException(nameof(guid));
            Url = url ?? throw new ArgumentNullException(nameof(url));
            MD5 = mD5 ?? throw new ArgumentNullException(nameof(mD5));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Format = format ?? throw new ArgumentNullException(nameof(format));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            PubTime = pubTime;
        }
    }
}
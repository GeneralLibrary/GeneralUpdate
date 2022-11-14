using System;

namespace GeneralUpdate.Core.Domain.Entity
{
    public class VersionInfo : Entity
    {
        public VersionInfo() { }

        public VersionInfo(long pubTime, string name, string mD5, string version, string url)
        {
            PubTime = pubTime;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            MD5 = mD5 ?? throw new ArgumentNullException(nameof(mD5));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Url = url ?? throw new ArgumentNullException(nameof(Url));
            if (!IsURL(Url)) throw new Exception($"Illegal url {nameof(Url)}");
        }

        /// <summary>
        /// Update package release time.
        /// </summary>
        public long PubTime { get; set; }

        /// <summary>
        /// Update package name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Compare and verify with the downloaded update package.
        /// </summary>
        public string MD5 { get; set; }

        /// <summary>
        /// The version number.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Remote service url address.
        /// </summary>
        public string Url { get; set; }
    }
}

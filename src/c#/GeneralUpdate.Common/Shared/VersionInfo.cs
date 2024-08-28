using System;

namespace GeneralUpdate.Common.Shared
{
    public class VersionInfo
    {
        public VersionInfo()
        { }

        public VersionInfo(long pubTime, string name, string hash, string version, string url)
        {
            PubTime = pubTime;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Url = url ?? throw new ArgumentNullException(nameof(Url));
            if (!IsUri(Url)) throw new Exception($"Illegal url {nameof(Url)}");
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
        public string Hash { get; set; }

        /// <summary>
        /// The version number.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Remote service url address.
        /// </summary>
        public string Url { get; set; }

        public override string ToString()
        {
            return Version;
        }
        
        bool IsUri(string uriString)
        {
            return Uri.TryCreate(uriString, UriKind.Absolute, out Uri uriResult) 
                   && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme == Uri.UriSchemeFtp);
        }
    }
}
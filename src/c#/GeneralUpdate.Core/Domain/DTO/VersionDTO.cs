namespace GeneralUpdate.Core.Domain.DTO
{
    public class VersionDTO
    {
        public VersionDTO(string hash, long pubTime, string version, string url, string name)
        {
            Hash = hash;
            PubTime = pubTime;
            Version = version;
            Url = url;
            Name = name;
        }

        public string Hash { get; set; }

        public long PubTime { get; set; }

        public string Version { get; set; }

        public string Url { get; set; }

        public string Name { get; set; }
    }
}
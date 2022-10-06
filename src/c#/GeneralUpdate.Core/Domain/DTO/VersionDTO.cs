namespace GeneralUpdate.Core.Domain.DTO
{
    public class VersionDTO
    {
        public VersionDTO(string md5, long pubTime, string version, string url, string name)
        {
            MD5 = md5;
            PubTime = pubTime;
            Version = version;
            Url = url;
            Name = name;
        }

        public string MD5 { get; set; }

        public long PubTime { get; set; }

        public string Version { get; set; }

        public string Url { get; set; }

        public string Name { get; set; }
    }
}
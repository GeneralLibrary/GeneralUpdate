using System;

namespace GeneralUpdate.Zip.Events
{
    public class BaseUnZipProgressEventArgs : EventArgs
    {
        public long Size { get; set; }

        public int Index { get; set; }

        public int Count { get; set; }

        public string Path { get; set; }

        public string Name { get; set; }
    }
}
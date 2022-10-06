using System.Collections.Generic;

namespace GeneralUpdate.Differential.Common
{
    public class Filefilter
    {
        public const string JSON_DLL = "Newtonsoft.Json.dll";

        public static readonly List<string> Temp = new List<string>() { ".json" };

        /// <summary>
        /// File formats to avoid when doing differential updates.
        /// </summary>
        public static readonly List<string> Diff = new List<string>() { ".patch", ".7z", ".zip", ".rar", ".tar", ".db", ".xml", ".ini", ".json", ".config" };
    }
}
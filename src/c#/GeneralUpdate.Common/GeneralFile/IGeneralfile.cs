using System.Collections.Generic;
using System.IO;

namespace GeneralUpdate.Common.GeneralFile
{
    public interface IGeneralfile
    {
        bool Equals();
        
        List<string> GetBlackFileFormats();
        
        List<string> GetBlackFiles();
        
        List<FileInfo> Comparer(string directoryA, string directoryB);

        void Create<T>(string targetPath, T obj);

        T Read<T>(string path);
        
        string Serialize(object obj);

        T Deserialize<T>(string str);
    }
}
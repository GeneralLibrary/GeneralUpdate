using System.Collections.Generic;
using System.IO;

namespace GeneralUpdate.Common;

public class BlackListManager
{
    private readonly static object _lockObject = new object();
    
    private static BlackListManager _instance;
    
    private static readonly List<string> _blackFileFormats =
    [
        ".patch",
        ".7z",
        ".zip",
        ".rar",
        ".tar",
        ".json"
    ];

    private static readonly List<string> _blackFiles = ["Newtonsoft.Json.dll"];

    private BlackListManager()
    {
    }

    public static BlackListManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lockObject)
                {
                    if (_instance == null)
                    {
                        _instance = new BlackListManager();
                    }
                }
            }

            return _instance;
        }
    }

    public IReadOnlyList<string> BlackFileFormats => _blackFileFormats.AsReadOnly();
    public IReadOnlyList<string> BlackFiles => _blackFiles.AsReadOnly();
    
    public void AddBlackFileFormats(List<string> formats)
    {
        foreach (var format in formats)
        {
            AddBlackFileFormat(format);
        }
    }
        
    public void AddBlackFileFormat(string format)
    {
        if (!_blackFileFormats.Contains(format))
        {
            _blackFileFormats.Add(format);
        }
    }
     
    public void RemoveBlackFileFormat(string format)
    {
        _blackFileFormats.Remove(format);
    }

    public void AddBlackFiles(List<string> fileNames)
    {
        foreach (var fileName in fileNames)
        {
            AddBlackFile(fileName);
        }
    }
        
    public void AddBlackFile(string fileName)
    {
        if (!_blackFiles.Contains(fileName))
        {
            _blackFiles.Add(fileName);
        }
    }

    public void RemoveBlackFile(string fileName)
    {
        _blackFiles.Remove(fileName);
    }
    
    public bool IsBlacklisted(string relativeFilePath)
    {
        var fileName = Path.GetFileName(relativeFilePath);
        var fileExtension = Path.GetExtension(relativeFilePath);

        return _blackFiles.Contains(fileName) || _blackFileFormats.Contains(fileExtension);
    }
}
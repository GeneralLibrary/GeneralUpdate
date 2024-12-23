using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.FileBasic;

public class BlackListManager
{
    private static readonly object LockObject = new object();
    private static BlackListManager? _instance;

    private static readonly List<string> _blackFileFormats =
    [
        ".patch",
        Format.ZIP,
        ".rar",
        ".tar",
        ".json",
        ".pdb"
    ];

    private static readonly List<string> _blackFiles = ["Newtonsoft.Json.dll"];

    private static readonly List<string> _skipDirectorys = [];

    private BlackListManager() { }

    public static BlackListManager? Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (LockObject)
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

    public IReadOnlyList<string> SkipDirectorys = _skipDirectorys.AsReadOnly();

    public void AddBlackFileFormats(List<string>? formats)
    {
        if (formats == null)
            return;

        foreach (var format in formats)
        {
            AddBlackFileFormat(format);
        }
    }

    public void AddBlackFileFormat(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return;

        if (!_blackFileFormats.Contains(format))
        {
            _blackFileFormats.Add(format);
        }
    }

    public void AddBlackFiles(List<string>? fileNames)
    {
        if (fileNames == null)
            return;

        foreach (var fileName in fileNames)
        {
            AddBlackFile(fileName);
        }
    }

    public void AddBlackFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        if (!_blackFiles.Contains(fileName))
        {
            _blackFiles.Add(fileName);
        }
    }

    public void AddSkipDirectorys(List<string>? directorys)
    {
        if (directorys == null)
            return;
        foreach (var directory in directorys)
        {
            AddSkipDirectory(directory);
        }
    }

    public void AddSkipDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return;
        if (!_skipDirectorys.Contains(directory))
        {
            _skipDirectorys.Add(directory);
        }
    }

    public bool IsBlacklisted(string relativeFilePath)
    {
        var fileName = Path.GetFileName(relativeFilePath);
        var fileExtension = Path.GetExtension(relativeFilePath);

        return _blackFiles.Contains(fileName) || _blackFileFormats.Contains(fileExtension);
    }

    public bool IsSkipDirectory(string directory)
    {
        return _skipDirectorys.Any(directory.Contains);
    }
}
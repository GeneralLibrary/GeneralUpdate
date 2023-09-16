using GeneralUpdate.Core.CustomAwaiter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.Differential.Config.Handles
{
    public class IniHandle<TEntity> : IHandle<TEntity>, IAwaiter<TEntity> where TEntity : class
    {
        public bool IsCompleted => throw new NotImplementedException();

        public TEntity GetResult()
        {
            throw new NotImplementedException();
        }

        public void OnCompleted(Action continuation)
        {
            throw new NotImplementedException();
        }

        public Task<TEntity> Read(string path)
        {
            throw new NotImplementedException();
        }

        public Task<bool> Write(TEntity oldEntity, TEntity newEntity)
        {
            throw new NotImplementedException();
        }

        Dictionary<string, Dictionary<string, string>> ParseIniFile(string filePath)
        {
            Dictionary<string, Dictionary<string, string>> iniData = new Dictionary<string, Dictionary<string, string>>();
            string currentSection = "";

            foreach (string line in File.ReadLines(filePath))
            {
                string trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    iniData[currentSection] = new Dictionary<string, string>();
                }
                else if (!string.IsNullOrEmpty(trimmedLine) && trimmedLine.Contains("="))
                {
                    string[] parts = trimmedLine.Split('=');
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();
                    iniData[currentSection][key] = value;
                }
            }

            return iniData;
        }

        static Dictionary<string, Dictionary<string, string>> GetIniFileDiff(
            Dictionary<string, Dictionary<string, string>> originalIni,
            Dictionary<string, Dictionary<string, string>> targetIni)
        {
            Dictionary<string, Dictionary<string, string>> diffIni = new Dictionary<string, Dictionary<string, string>>();

            foreach (var sectionKey in originalIni.Keys)
            {
                if (!targetIni.ContainsKey(sectionKey))
                {
                    diffIni[sectionKey] = originalIni[sectionKey];
                }
                else
                {
                    Dictionary<string, string> originalSection = originalIni[sectionKey];
                    Dictionary<string, string> targetSection = targetIni[sectionKey];
                    Dictionary<string, string> diffSection = originalSection
                        .Where(kv => !targetSection.ContainsKey(kv.Key) || targetSection[kv.Key] != kv.Value)
                        .ToDictionary(kv => kv.Key, kv => kv.Value);

                    if (diffSection.Count > 0)
                    {
                        diffIni[sectionKey] = diffSection;
                    }
                }
            }

            return diffIni;
        }

        void MergeIniFiles(
            Dictionary<string, Dictionary<string, string>> targetIni,
            Dictionary<string, Dictionary<string, string>> diffIni)
        {
            foreach (var sectionKey in diffIni.Keys)
            {
                if (!targetIni.ContainsKey(sectionKey))
                {
                    targetIni[sectionKey] = new Dictionary<string, string>();
                }

                Dictionary<string, string> targetSection = targetIni[sectionKey];
                Dictionary<string, string> diffSection = diffIni[sectionKey];

                foreach (var keyValue in diffSection)
                {
                    targetSection[keyValue.Key] = keyValue.Value;
                }
            }
        }

        void SaveIniFile(string filePath, Dictionary<string, Dictionary<string, string>> iniData)
        {
            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                foreach (var sectionKey in iniData.Keys)
                {
                    writer.WriteLine($"[{sectionKey}]");

                    foreach (var keyValue in iniData[sectionKey])
                    {
                        writer.WriteLine($"{keyValue.Key}={keyValue.Value}");
                    }

                    writer.WriteLine(); // 空行分隔不同的部分
                }
            }
        }
    }
}
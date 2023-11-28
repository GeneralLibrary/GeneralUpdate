using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GeneralUpdate.Core.HashAlgorithms
{
    public abstract class HashAlgorithmBase
    {
        public string ComputeHash(string fileName)
        {
            using (var hashAlgorithm = GetHashAlgorithm())
            {
                using (var file = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var dataArray = GetHashAlgorithm().ComputeHash(file);
                    var stringBuilder = new StringBuilder();
                    for (int i = 0; i < dataArray.Length; i++)
                    {
                        stringBuilder.Append(dataArray[i].ToString("x2"));
                    }
                    return stringBuilder.ToString();
                }
            }
        }

        protected abstract HashAlgorithm GetHashAlgorithm();
    }
}
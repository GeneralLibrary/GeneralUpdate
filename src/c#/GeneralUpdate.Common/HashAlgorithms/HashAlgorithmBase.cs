using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GeneralUpdate.Common.HashAlgorithms
{
    public abstract class HashAlgorithmBase
    {
        public string ComputeHash(string fileName)
        {
            if (!System.IO.File.Exists(fileName))
                throw new FileNotFoundException(nameof(fileName));

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
        
        public byte[] ComputeHashBytes(string fileName)
        {
            if (!File.Exists(fileName))
                throw new FileNotFoundException(nameof(fileName));

            using (var hashAlgorithm = GetHashAlgorithm())
            {
                using (var file = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    return hashAlgorithm.ComputeHash(file);
                }
            }
        }
        
        protected abstract HashAlgorithm GetHashAlgorithm();
    }
}
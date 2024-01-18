using System.Security.Cryptography;

namespace GeneralUpdate.Core.HashAlgorithms
{
    public class Md5HashAlgorithm : HashAlgorithmBase
    {
        protected override HashAlgorithm GetHashAlgorithm() => MD5.Create();
    }
}
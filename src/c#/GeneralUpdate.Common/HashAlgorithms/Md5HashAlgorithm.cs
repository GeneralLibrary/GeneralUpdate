using System.Security.Cryptography;

namespace GeneralUpdate.Common.HashAlgorithms
{
    public class Md5HashAlgorithm : HashAlgorithmBase
    {
        protected override HashAlgorithm GetHashAlgorithm() => MD5.Create();
    }
}
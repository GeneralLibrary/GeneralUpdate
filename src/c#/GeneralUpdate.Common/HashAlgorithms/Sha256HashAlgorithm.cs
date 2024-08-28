using System.Security.Cryptography;

namespace GeneralUpdate.Common.HashAlgorithms
{
    public class Sha256HashAlgorithm : HashAlgorithmBase
    {
        protected override HashAlgorithm GetHashAlgorithm() => SHA256.Create();
    }
}
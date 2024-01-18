using System.Security.Cryptography;

namespace GeneralUpdate.Core.HashAlgorithms
{
    public class Sha256HashAlgorithm : HashAlgorithmBase
    {
        protected override HashAlgorithm GetHashAlgorithm() => SHA256.Create();
    }
}
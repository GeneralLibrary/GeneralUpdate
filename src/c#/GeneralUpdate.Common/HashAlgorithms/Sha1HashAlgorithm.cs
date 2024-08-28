using System.Security.Cryptography;

namespace GeneralUpdate.Common.HashAlgorithms
{
    public class Sha1HashAlgorithm : HashAlgorithmBase
    {
        protected override HashAlgorithm GetHashAlgorithm() => new SHA1Managed();
    }
}
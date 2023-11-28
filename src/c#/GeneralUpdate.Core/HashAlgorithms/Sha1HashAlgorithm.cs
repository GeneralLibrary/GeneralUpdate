using System.Security.Cryptography;

namespace GeneralUpdate.Core.HashAlgorithms
{
    public class Sha1HashAlgorithm : HashAlgorithmBase
    {
        protected override HashAlgorithm GetHashAlgorithm()=> new SHA1Managed();
    }
}

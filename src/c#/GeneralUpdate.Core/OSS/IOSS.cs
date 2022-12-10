using System.Threading.Tasks;

namespace GeneralUpdate.Core.OSS
{
    public interface IOSS
    {
        Task<IOSS> Download(string remoteUrl);
    }
}

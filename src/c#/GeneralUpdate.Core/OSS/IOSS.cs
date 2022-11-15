using System.Threading.Tasks;

namespace GeneralUpdate.Core.OSS
{
    public interface IOSS
    {
        void SetParmeter(string url);

        Task<string> Download();
    }
}

using GeneralUpdate.Core.Domain.Enum;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.OSS
{
    public interface IOSS
    {
        void SetParameter(string url, string fileName, string format, int timeOut);

        void Update();
    }
}

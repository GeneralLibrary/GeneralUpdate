using GeneralUpdate.Core.Domain.DTO;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Utils;
using System;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Domain.Service
{
    public class VersionService
    {
        public async Task<VersionRespDTO> ValidationVersion(string url,Action<object, ProgressType, string> statusCallback) 
        {
            statusCallback(this, ProgressType.Check, "Update checking...");
            VersionRespDTO resp = await ValidationVersion(url);
            if(resp == null) statusCallback(this, ProgressType.Check, $"Request failed , Code :{resp.Code}, Message:{resp.Message} !");
            return await ValidationVersion(url);
        }

        public async Task<VersionRespDTO> ValidationVersion(string url)
        {
            var updateResp = await HttpUtil.GetTaskAsync<VersionRespDTO>(url);
            if (updateResp == null || updateResp.Body == null) throw new ArgumentNullException($"The  verification request is abnormal, please check the network or parameter configuration!");
            if (updateResp.Code != HttpStatus.OK) throw new Exception($"Request failed , Code :{updateResp.Code}, Message:{updateResp.Message} !");
            if (updateResp.Code == HttpStatus.OK) return updateResp;
            return null;
        }
    }
}

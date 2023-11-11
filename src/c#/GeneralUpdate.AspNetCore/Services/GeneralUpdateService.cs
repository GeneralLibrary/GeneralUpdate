using GeneralUpdate.AspNetCore.DTO;
using GeneralUpdate.Core.Domain.DTO;
using GeneralUpdate.Core.Domain.Enum;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace GeneralUpdate.AspNetCore.Services
{
    public class GeneralUpdateService : IUpdateService
    {
        /// <summary>
        /// Update validate.
        /// </summary>
        /// <param name="clientType">1:ClientApp 2:UpdateApp</param>
        /// <param name="clientVersion">The current version number of the client.</param>
        /// <param name="serverLastVersion">The latest version number of the server.</param>
        /// <param name="isForce">Do you need to force an update.</param>
        /// <param name="getUrlsAction">Each version update (Query the latest version information in the database according to the client version number).</param>
        /// <returns></returns>
        public string Update(int clientType, string clientVersion, string serverLastVersion, string clientAppKey, string appSecretKey,
            bool isForce, List<VersionDTO> versions)
        {
            ParameterVerification(clientType, clientVersion, serverLastVersion, clientAppKey, appSecretKey, versions);
            if (!clientAppKey.Equals(appSecretKey)) throw new Exception("App key does not exist or is incorrect !");
            Version clientLastVersion;
            var respDTO = new VersionRespDTO();
            try
            {
                if (!Version.TryParse(clientVersion, out clientLastVersion))
                {
                    respDTO.Message = $"{RespMessage.RequestFailed} Wrong version number.";
                    respDTO.Code = HttpStatus.BAD_REQUEST;
                    return JsonConvert.SerializeObject(respDTO);
                }
                var lastVersion = new Version(serverLastVersion);
                if (clientLastVersion < lastVersion)
                {
                    respDTO.Body = new VersionBodyDTO() { ClientType = clientType, Versions = versions, IsUpdate = true, IsForcibly = isForce };
                    respDTO.Code = HttpStatus.OK;
                    respDTO.Message = RespMessage.RequestSucceeded;
                }
                else
                {
                    respDTO.Body = new VersionBodyDTO() { ClientType = clientType, Versions = versions, IsUpdate = false, IsForcibly = false };
                    respDTO.Code = HttpStatus.OK;
                    respDTO.Message = RespMessage.RequestNone;
                }
            }
            catch
            {
                respDTO.Message = RespMessage.ServerException;
                respDTO.Code = HttpStatus.SERVICE_UNAVAILABLE;
            }
            return JsonConvert.SerializeObject(respDTO);
        }

        private void ParameterVerification(int clientType, string clientVersion, string serverLastVersion, string clientAppkey, string appSecretKey, List<VersionDTO> versions)
        {
            if (clientType <= 0) throw new Exception(@"'clientType' cannot be less than or equal to 0 !");
            if (string.IsNullOrWhiteSpace(clientVersion)) throw new ArgumentNullException(@"'clientVersion' cannot be null !");
            if (string.IsNullOrWhiteSpace(serverLastVersion)) throw new ArgumentNullException(@"'serverLastVersion' cannot be null !");
            if (versions == null) throw new ArgumentNullException(@"versions cannot be null !");
            if (string.IsNullOrEmpty(clientAppkey) || string.IsNullOrEmpty(appSecretKey)) throw new NullReferenceException("The APP key does not exist !");
        }
    }
}
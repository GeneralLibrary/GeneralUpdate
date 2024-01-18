using GeneralUpdate.Core.Domain.DTO;
using System;
using System.Collections.Generic;

namespace GeneralUpdate.AspNetCore.Services
{
    public interface IUpdateService
    {
        /// <summary>
        /// Verify whether the current version of the client needs to be updated.
        /// </summary>
        /// <param name="clientType">1:ClientApp 2:UpdateApp</param>
        /// <param name="clientVersion">Current version of the client</param>
        /// <param name="serverLastVersion">The latest version of the server.</param>
        /// <param name="clientAppKey">The appkey agreed by the client and server.</param>
        /// <param name="appSecretKey">Appkey is stored in the database.</param>
        /// <param name="isForce">Whether to force all versions to be updated.</param>
        /// <param name="versions"></param>
        /// <returns>Json object.</returns>
        string Update(int clientType, string clientVersion, string serverLastVersion, string clientAppKey, string appSecretKey, bool isForce, List<VersionDTO> versions);

        /// <summary>
        /// When this web api is called at the end of the automatic update, it does not mean that every call is successful.
        /// Failure, rollback, and success scenarios will inform the server of the result of the update through this web api.
        /// If there is an exception let the decision maker decide whether to fix the problem by pushing the latest version of the update again.
        /// </summary>
        /// <param name="clientType">1:ClientApp 2:UpdateApp</param>
        /// <param name="clientVersion">Current version of the client.</param>
        /// <param name="clientAppkey">The appkey agreed by the client and server.</param>
        /// <param name="appSecretKey">Appkey is stored in the database.</param>
        /// <param name="meesage">The message from the client is used to describe the current situation to the decision maker.</param>
        /// <param name="dumpBase64">If an exception occurs, the dump file is returned.</param>
        /// <param name="logBase64">If an exception occurs, the log log is returned</param>
        /// <param name="exception">If an exception occurs, the object is returned.</param>
        /// <returns></returns>
        string Report(int clientType, string clientVersion, string clientAppkey, string appSecretKey, string meesage, string dumpBase64, string logBase64, Exception exception);
    }
}
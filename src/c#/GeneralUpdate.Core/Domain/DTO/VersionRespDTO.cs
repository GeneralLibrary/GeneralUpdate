using System.Collections.Generic;

namespace GeneralUpdate.Core.Domain.DTO
{
    public class VersionRespDTO : BaseResponseDTO<VersionBodyDTO> {}

    public class VersionBodyDTO
    {
        public bool IsUpdate { get; set; }

        /// <summary>
        /// Is forcibly update.
        /// </summary>
        public bool IsForcibly { get; set; }

        /// <summary>
        /// 1:ClientApp 2:UpdateApp
        /// </summary>
        public int ClientType { get; set; }

        /// <summary>
        /// Returns information about all versions that are different from the latest version based on the current version of the client.
        /// </summary>
        public List<VersionDTO> Versions { get; set; }
    }
}

using GeneralUpdate.Core.Domain.Entity;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Domain.DTO.Assembler
{
    public class VersionAssembler
    {
        public static List<VersionInfo> ToEntitys(List<VersionDTO> versionDTO)
        {
            List<VersionInfo> entitys = new List<VersionInfo>();
            versionDTO.ForEach((v) =>
            {
                entitys.Add(ToEntity(v));
            });
            return entitys;
        }

        public static VersionInfo ToEntity(VersionDTO versionDTO)
        {
            return new VersionInfo(versionDTO.PubTime, versionDTO.Name, versionDTO.MD5, versionDTO.Version, versionDTO.Url);
        }
    }
}
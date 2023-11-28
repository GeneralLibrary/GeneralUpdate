using GeneralUpdate.Core.Domain.Entity;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Domain.DO.Assembler
{
    public class VersionAssembler
    {
        public static List<VersionInfo> ToDataObjects(List<VersionConfigDO> versionDTO)
        {
            List<VersionInfo> entitys = new List<VersionInfo>();
            versionDTO.ForEach((v) =>
            {
                entitys.Add(ToDataObject(v));
            });
            return entitys;
        }

        public static VersionInfo ToDataObject(VersionConfigDO versionDO)
        {
            return new VersionInfo(versionDO.PubTime, versionDO.Name, versionDO.Hash, versionDO.Version, versionDO.Url);
        }
    }
}
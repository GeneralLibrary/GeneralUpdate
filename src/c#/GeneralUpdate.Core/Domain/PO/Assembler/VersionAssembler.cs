using GeneralUpdate.Core.Domain.Entity;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Domain.PO.Assembler
{
    public class VersionAssembler
    {
        public static List<VersionInfo> ToDataObjects(List<VersionPO> versionDTO)
        {
            List<VersionInfo> entitys = new List<VersionInfo>();
            versionDTO.ForEach((v) =>
            {
                entitys.Add(ToDataObject(v));
            });
            return entitys;
        }

        public static VersionInfo ToDataObject(VersionPO versionDO)
        {
            return new VersionInfo(versionDO.PubTime, versionDO.Name, versionDO.MD5, versionDO.Version, versionDO.Url);
        }
    }
}

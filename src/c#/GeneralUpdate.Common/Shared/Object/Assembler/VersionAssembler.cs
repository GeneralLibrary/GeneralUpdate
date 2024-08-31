using System.Collections.Generic;

namespace GeneralUpdate.Common.Shared.Object
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
            return new VersionInfo(versionDTO.PubTime, versionDTO.Name, versionDTO.Hash, versionDTO.Version, versionDTO.Url);
        }
    }
}
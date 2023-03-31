using GeneralUpdate.Core.Utils;
using System;

namespace GeneralUpdate.Core.Domain.Entity.Assembler
{
    public class ProcessAssembler
    {
        public static string ToBase64(ProcessInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            return SerializeUtil.Serialize(info);
        }

        public static string ToBase64(ParamsOSS info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            return SerializeUtil.Serialize(info);
        }

        public static Packet ToPacket(ProcessInfo info)
        {
            var packet = new Packet();
            packet.AppName = info.AppName;
            packet.AppSecretKey = info.AppSecretKey;
            packet.AppType = info.AppType;
            packet.InstallPath = info.InstallPath;
            packet.ClientVersion = info.CurrentVersion;
            packet.LastVersion = info.LastVersion;
            packet.UpdateLogUrl = info.LogUrl;
            packet.Encoding = ConvertUtil.ToEncoding(info.CompressEncoding);
            packet.Format = info.CompressFormat;
            packet.DownloadTimeOut = info.DownloadTimeOut;
            packet.UpdateVersions = info.UpdateVersions;
            return packet;
        }
    }
}
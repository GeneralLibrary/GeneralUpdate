using GeneralUpdate.Core.Utils;
using System;
using System.Text;

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
            packet.Encoding = ToEncoding(info.CompressEncoding);
            packet.Format = info.CompressFormat;
            packet.DownloadTimeOut = info.DownloadTimeOut;
            packet.UpdateVersions = info.UpdateVersions;
            return packet;
        }

        private static Encoding ToEncoding(int type)
        {
            Encoding encoding = Encoding.Default;
            switch (type)
            {
                case 1:
                    encoding = Encoding.UTF8;
                    break;

                case 2:
                    encoding = Encoding.UTF7;
                    break;

                case 3:
                    encoding = Encoding.UTF32;
                    break;

                case 4:
                    encoding = Encoding.Unicode;
                    break;

                case 5:
                    encoding = Encoding.BigEndianUnicode;
                    break;

                case 6:
                    encoding = Encoding.ASCII;
                    break;

                case 7:
                    encoding = Encoding.Default;
                    break;
            }
            return encoding;
        }
    }
}
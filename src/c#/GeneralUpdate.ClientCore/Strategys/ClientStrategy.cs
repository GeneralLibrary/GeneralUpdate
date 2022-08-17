using GeneralUpdate.Core.Strategys;
using GeneralUpdate.Core.Utils;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GeneralUpdate.ClientCore.Strategys
{
    public class ClientStrategy : AbstractStrategy
    {
        public override void Excute()
        {
            //if (Packet.IsUpdate)
            //{
            //    base.Excute();
            //}
            //else
            //{
            //    StartApp(Packet.AppName);
            //}
        }

        protected override bool StartApp(string appName)
        {
            try
            {
                //Task.Run(async () =>
                //{
                //    var respDTO = await HttpUtil.GetTaskAsync<UpdateValidateRespDTO>(Packet.MainValidateUrl);
                //    if (respDTO.Code == HttpStatus.OK)
                //    {
                //        var body = respDTO.Body;
                //        try
                //        {
                //            //Request updated information for the main application.
                //            var clientParameter = new ClientParameter();
                //            clientParameter.ClientVersion = Packet.ClientVersion;
                //            clientParameter.LastVersion = Packet.LastVersion;
                //            clientParameter.InstallPath = Packet.InstallPath;
                //            clientParameter.UpdateLogUrl = Packet.UpdateLogUrl;
                //            clientParameter.MainValidateUrl = Packet.MainValidateUrl;
                //            clientParameter.MainUpdateUrl = Packet.MainUpdateUrl;
                //            clientParameter.AppName = Packet.MainAppName;
                //            clientParameter.AppType = 1;
                //            clientParameter.CompressEncoding = ConvertUtil.ToEncodingType(Packet.Encoding);
                //            clientParameter.CompressFormat = Packet.Format;
                //            clientParameter.DownloadTimeOut = Packet.DownloadTimeOut;
                //            clientParameter.AppSecretKey = Packet.AppSecretKey;
                //            clientParameter.UpdateVersions = ConvertUtil.ToUpdateVersions(body.UpdateVersions);
                //            var clientParameterBase64 = SerializeUtil.Serialize(clientParameter);
                //            if (!string.IsNullOrEmpty(Packet.UpdateLogUrl))
                //            {
                //                Process.Start("explorer.exe", Packet.UpdateLogUrl);
                //            }
                //            Process.Start($"{Packet.InstallPath}\\{appName}.exe", clientParameterBase64);
                //            Process.GetCurrentProcess().Kill();
                //        }
                //        catch (Exception ex)
                //        {
                //            if (ExceptionEventAction != null)
                //                ExceptionEventAction(this, new ExceptionEventArgs(ex));
                //        }
                //    }
                //    else
                //    {
                //        if (ExceptionEventAction != null)
                //            ExceptionEventAction(this,
                //             new ExceptionEventArgs(new System.Exception($"{ respDTO.Code }{ respDTO.Message }")));
                //    }
                //});
                return true;
            }
            catch (Exception ex)
            {
                //if (ExceptionEventAction != null)
                //    ExceptionEventAction(this, new ExceptionEventArgs(ex));
                return false;
            }
        }
    }
}
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Text;
using GeneralUpdate.ClientCore;
using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Strategys.PlatformWindows;

namespace TestWinForms
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //之前Demo中有一些回调,之所以写在winform里面,是用来更新 更新客户端的,但是我的客户端基本不更新,所以不考虑那一块
            Upgrade();
        }

        private string mainAppName = "TestWinForms";
        public void Upgrade()
        {
            var version = GetDllVersion(AppDomain.CurrentDomain.BaseDirectory + $@"{mainAppName}.dll");
            this.label2.Text = version;
          
            //异步操作,不影响主界面操作
            Task.Run(async () =>
            {
                //该对象用于主程序客户端与更新组件进程之间交互用的对象
                var config = new Configinfo();
                //本机的客户端程序应用地址
                config.InstallPath = AppDomain.CurrentDomain.BaseDirectory;

                //更新程序的当前版本号,这个基本不需要更新,直接写死就行了
                config.ClientVersion = "1.0.0.0";
                //客户端类型：1.主程序客户端 2.更新组件
                config.AppType = AppType.ClientApp;
                //指定应用密钥，用于区分客户端应用 要和fileserver中的一致
                config.AppSecretKey = "B8A7FADD-386C-46B0-B283-C9F963420C7C";

                var updateHost = "http://127.0.0.1:5008/api/update";
                //更新组件更新包下载地址  就是检查更新程序的url
                config.UpdateUrl = $"{updateHost}/Versions/{AppType.UpgradeApp}/{config.ClientVersion}/{config.AppSecretKey}";
                //更新程序exe名称
                config.AppName = "GeneralUpdate.Upgrad";
                //主程序客户端exe名称
                config.MainAppName = mainAppName;
                //主程序版本信息 通过当前程序集获取   每次需要发布版本时,改一下winform程序的程序集版本号就可以了,api会自动判断是否需要升级
                var mainVersion = GetDllVersion(AppDomain.CurrentDomain.BaseDirectory + $@"{mainAppName}.dll");
                //更新公告网页
                config.UpdateLogUrl = $"{updateHost}/UpdateLog.html";
                //检查更新主程序的url
                config.MainUpdateUrl = $"{updateHost}/Versions/{AppType.ClientApp}/{mainVersion}/{config.AppSecretKey}";

                //构建启动对象
                var generalClientBootstrap = new GeneralClientBootstrap();

                generalClientBootstrap.Config(config).Option(UpdateOption.DownloadTimeOut, 60).Option(UpdateOption.Encoding, Encoding.Default).Option(UpdateOption.Format, Format.ZIP).
                    //注入一个func让用户决定是否跳过本次更新，如果是强制更新则不生效
                    SetCustomOption(() => false).Strategy<WindowsStrategy>();
                await generalClientBootstrap.LaunchTaskAsync();
            });
        }

        /// <summary>
        /// 获取对应路径的dll的版本号
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public string GetDllVersion(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo != null && fileInfo.Exists)
                {
                    return Assembly.LoadFrom(filePath).GetName().Version.ToString();
                }

                throw new Exception($"{filePath}文件版本获取失败");
            }
            catch (Exception ex)
            {
                throw new Exception($"{filePath}文件版本获取失败,错误信息 : {ex.Message} .", ex.InnerException);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        { 
            Upgrade();
        }


        private void button2_Click_1(object sender, EventArgs e)
        {
            MessageBox.Show("更新成功");
        }
    }
}
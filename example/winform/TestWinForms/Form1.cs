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
            //֮ǰDemo����һЩ�ص�,֮����д��winform����,���������� ���¿ͻ��˵�,�����ҵĿͻ��˻���������,���Բ�������һ��
            Upgrade();
        }

        private string mainAppName = "TestWinForms";
        public void Upgrade()
        {
            var version = GetDllVersion(AppDomain.CurrentDomain.BaseDirectory + $@"{mainAppName}.dll");
            this.label2.Text = version;
          
            //�첽����,��Ӱ�����������
            Task.Run(async () =>
            {
                //�ö�������������ͻ���������������֮�佻���õĶ���
                var config = new Configinfo();
                //�����Ŀͻ��˳���Ӧ�õ�ַ
                config.InstallPath = AppDomain.CurrentDomain.BaseDirectory;

                //���³���ĵ�ǰ�汾��,�����������Ҫ����,ֱ��д��������
                config.ClientVersion = "1.0.0.0";
                //�ͻ������ͣ�1.������ͻ��� 2.�������
                config.AppType = AppType.ClientApp;
                //ָ��Ӧ����Կ���������ֿͻ���Ӧ�� Ҫ��fileserver�е�һ��
                config.AppSecretKey = "B8A7FADD-386C-46B0-B283-C9F963420C7C";

                var updateHost = "http://127.0.0.1:5008/api/update";
                //����������°����ص�ַ  ���Ǽ����³����url
                config.UpdateUrl = $"{updateHost}/Versions/{AppType.UpgradeApp}/{config.ClientVersion}/{config.AppSecretKey}";
                //���³���exe����
                config.AppName = "GeneralUpdate.Upgrad";
                //������ͻ���exe����
                config.MainAppName = mainAppName;
                //������汾��Ϣ ͨ����ǰ���򼯻�ȡ   ÿ����Ҫ�����汾ʱ,��һ��winform����ĳ��򼯰汾�žͿ�����,api���Զ��ж��Ƿ���Ҫ����
                var mainVersion = GetDllVersion(AppDomain.CurrentDomain.BaseDirectory + $@"{mainAppName}.dll");
                //���¹�����ҳ
                config.UpdateLogUrl = $"{updateHost}/UpdateLog.html";
                //�������������url
                config.MainUpdateUrl = $"{updateHost}/Versions/{AppType.ClientApp}/{mainVersion}/{config.AppSecretKey}";

                //������������
                var generalClientBootstrap = new GeneralClientBootstrap();

                generalClientBootstrap.Config(config).Option(UpdateOption.DownloadTimeOut, 60).Option(UpdateOption.Encoding, Encoding.Default).Option(UpdateOption.Format, Format.ZIP).
                    //ע��һ��func���û������Ƿ��������θ��£������ǿ�Ƹ�������Ч
                    SetCustomOption(() => false).Strategy<WindowsStrategy>();
                await generalClientBootstrap.LaunchTaskAsync();
            });
        }

        /// <summary>
        /// ��ȡ��Ӧ·����dll�İ汾��
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

                throw new Exception($"{filePath}�ļ��汾��ȡʧ��");
            }
            catch (Exception ex)
            {
                throw new Exception($"{filePath}�ļ��汾��ȡʧ��,������Ϣ : {ex.Message} .", ex.InnerException);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        { 
            Upgrade();
        }


        private void button2_Click_1(object sender, EventArgs e)
        {
            MessageBox.Show("���³ɹ�");
        }
    }
}
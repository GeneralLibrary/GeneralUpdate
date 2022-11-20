using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using GeneralUpdate.AspNetCore.Hubs;
using static System.Net.Mime.MediaTypeNames;
using GeneralUpdate.AspNetCore.Services;
using GeneralUpdate.Core.Domain.DTO;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Utils;
using GeneralUpdate.AspNetCore.DTO;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace fileserver.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class UpdateController : ControllerBase
    {
        private IWebHostEnvironment _webHostEvironment;
        private IHttpContextAccessor _accessor;
        private IUpdateService _updateService;
        private IHubContext<VersionHub> _hubContext;

        public UpdateController(IWebHostEnvironment webHostEvironment, IHttpContextAccessor accessor, IUpdateService updateService, IHubContext<VersionHub> hubContext)
        {
            _webHostEvironment = webHostEvironment;
            _accessor = accessor;
            _updateService = updateService;
            _hubContext = hubContext;
        }

        //http://localhost:5008/api/Update/test
        [HttpGet]
        public string Test()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", $"1.0.0.9.zip");
            var md5 = FileUtil.GetFileMD5(path);
            return "success";
        }


        /// <summary>
        /// 推送强制更新命令给App
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<string> Push()
        {
            //http://localhost:5008/api/Update/push  测试路径

            await _hubContext.SendMessage("versionhub", "update");
            return "ok";
        }

        [HttpGet]
        [Route("/api/update/Versions/{clientType}/{clientVersion}/{clientAppKey}")]
        public string Versions(int clientType, string clientVersion, string clientAppKey)
        {
            //如果是更新程序,直接返回不需要更新,这个不会更新
            if (clientType == AppType.UpgradeApp)
            {
                return GetUpgradeInfo(clientType, clientVersion);
            }

            var versions = new List<VersionDTO>();
           
          
            var pubTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            //这里只会是更新主程序,因为升级程序已经在上面直接返回了
            string version = "1.0.0.9";//这里设置为9是让程序认为需要更新

            //生成好的更新包文件的MD5码，因为返回给客户端的时候需要同这个来验证是否可用
            //这个md5可以使用单元测试里面的生成md5获取  为了方便测试,直接用升级文件来获取了
            //var md5 = "cc9f7189676613b906bd7680ea518f0e";
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", $"{version}.zip");
            var md5 = FileUtil.GetFileMD5(path);

            var url = $"http://127.0.0.1:5008/{version}.zip";//更新包的下载地址
            var name = version;
            versions.Add(new VersionDTO(md5, pubTime, version, url, name));
            return _updateService.Update(clientType, clientVersion, version, clientAppKey, "B8A7FADD-386C-46B0-B283-C9F963420C7C", false, versions);

        }

        /// <summary>
        /// 当更新程序来请求时 返回一个不需要更新的对象回去
        /// </summary>
        /// <param name="clientType"></param>
        /// <returns></returns>
        private string GetUpgradeInfo(int clientType, string clientVersion)
        {
            var response = new VersionRespDTO() { };
            response.Body = new VersionBodyDTO() { ClientType = clientType, IsUpdate = false, IsForcibly = false, Versions = new List<VersionDTO>() { new VersionDTO("111", 0, clientVersion, "http://127.0.0.1:5008/test/", "111") } };
            response.Code = HttpStatus.OK;
            response.Message = RespMessage.RequestSucceeded;
            return JsonConvert.SerializeObject(response);
        }

        /// <summary>
        /// 上传文件
        /// 重写这个方法里面的插入数据库,就能和旧的联动了
        /// 打包工具上填的地址   http://localhost:5008/api/Update/Upload
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<string> Upload()
        {
            var uploadReapDTO = new UploadReapDTO();
            try
            {
                //只要调用了此方法,都是工具调用的,必然不可能为null
                var request = _accessor.HttpContext.Request;
                if (!request.HasFormContentType) throw new Exception("ContentType was not included in the request !");
                var form = await request.ReadFormAsync();

                var formFile = form.Files["file"];

                if (formFile is null || formFile.Length == 0) throw new ArgumentNullException("Uploaded update package file not found !");
                await using var stream = formFile.OpenReadStream();
                byte[] buffer = new byte[stream.Length];
                stream.Read(buffer, 0, buffer.Length);
                //TODO:save to file server.
                string localPath = $"E:\\{formFile.FileName}";
                await using var fileStream = new FileStream(localPath, FileMode.CreateNew, FileAccess.Write);
                fileStream.Write(buffer, 0, buffer.Length);

                //TODO oujl 将数据存储到mysql
                int.TryParse(request.Form["clientType"], out int clientType);
                var fileName = formFile.FileName;
                var version = request.Form["version"].ToString();
                var clientAppKey = request.Form["clientAppKey"].ToString();
                var md5 = request.Form["md5"].ToString();



                uploadReapDTO.Code = HttpStatus.OK;
                uploadReapDTO.Body = "Published successfully.";
                uploadReapDTO.Message = RespMessage.RequestSucceeded;
                return JsonConvert.SerializeObject(uploadReapDTO);
            }
            catch (Exception ex)
            {
                uploadReapDTO.Code = HttpStatus.BAD_REQUEST;
                uploadReapDTO.Body = $"Failed to publish ! Because : {ex.Message}";
                uploadReapDTO.Message = RespMessage.RequestFailed;
                return JsonConvert.SerializeObject(uploadReapDTO);
            }
        }
    }
}

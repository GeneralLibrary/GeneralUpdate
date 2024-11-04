using System;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Zip;
using GeneralUpdate.Zip.Factory;

namespace GeneralUpdate.ClientCore.Pipeline;

public class ZipMiddleware : IMiddleware
{
    public Task InvokeAsync(PipelineContext context)
    {
        return Task.Run(() =>
        {
            try
            {
                var type = MatchType(context.Get<string>("Format"));
                var name = context.Get<string>("Name");
                var sourcePath = context.Get<string>("ZipFilePath");
                var destinationPath = context.Get<string>("PatchPath");
                var encoding = context.Get<Encoding>("Encoding");

                var generalZipfactory = new GeneralZipFactory();
                generalZipfactory.CompressProgress += (sender, args) => { };
                generalZipfactory.Completed += (sender, args) => { };
                generalZipfactory.CreateOperate(type, name, sourcePath, destinationPath, true, encoding);
                generalZipfactory.UnZip();
            }
            catch (Exception e)
            {
                EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
            }
        });
    }

    private static OperationType MatchType(string extensionName)
    {
        var type = extensionName switch
        {
            ".zip" => OperationType.GZip,
            ".7z" => OperationType.G7z,
            _ => OperationType.None
        };
        return type;
    }
}
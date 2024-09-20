using System.Text;
using System.Threading.Tasks;
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
            var type = MatchType(context.Get<string>("Format"));
            var name = context.Get<string>("Name");
            var sourcePath = context.Get<string>("SourcePath");
            var destinationPath = context.Get<string>("DestinationPath");
            var encoding = context.Get<Encoding>("Encoding");

            var generalZipfactory = new GeneralZipFactory();
            generalZipfactory.CompressProgress += (sender, args) => { };
            generalZipfactory.Completed += (sender, args) => { };
            generalZipfactory.CreateOperate(type, name, sourcePath, destinationPath, true, encoding);
            generalZipfactory.UnZip();
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
namespace GeneralUpdate.ProtectService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateSlimBuilder(args);
            var app = builder.Build();
            var protectApi = app.MapGroup("/protect");
            protectApi.MapGet("/{paths}", Restore);
            app.Run();
        }

        internal static string Restore(string paths)
        {
            return paths;
        }
    }
}

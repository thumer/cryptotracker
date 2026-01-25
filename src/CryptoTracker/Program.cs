using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CryptoTracker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
            builder.Logging.AddApplicationInsights();

            var startup = new Startup(builder.Configuration);
            startup.ConfigureServices(builder.Services);

            var app = builder.Build();
            startup.Configure(app, app.Environment);
            app.Run();
        }
    }
}

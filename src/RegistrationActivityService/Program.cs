namespace RegistrationActivityService
{
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Serilog;

    class Program
    {
        static async Task<int> Main()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();
            
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
            
            var builder = new HostBuilder()
                .ConfigureHostConfiguration(context =>
                {
                    context.AddConfiguration(configuration);
                })
                .ConfigureServices(services =>
                {
                    services.AddHostedService<RegistrationActivityService>();
                })
                .UseSerilog();

            await builder.RunConsoleAsync();
            
            return 0;
        }
    }
}
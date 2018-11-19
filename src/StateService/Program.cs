namespace StateService
{
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Registration.Data;
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
                .ConfigureServices((host, services) =>
                {
                    services
                        .AddEntityFrameworkSqlServer()
                        .AddDbContext<RegistrationStateSagaDbContext>(options =>
                        {
                            options
                                .UseSqlServer(host.Configuration.GetValue<string>("DatabaseConnectionString"));
                        });
                    
                    services
                        .AddHostedService<StateService>();
                })
                .UseSerilog();

            await builder.RunConsoleAsync();
            
            return 0;
        }
    }
}
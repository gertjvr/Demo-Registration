namespace RegistrationService
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MassTransit;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Registration.Consumers;
    using Serilog;


    public class RegistrationService :
        IHostedService
    {
        readonly ILogger _logger = Log.ForContext<RegistrationService>();
        readonly IConfiguration _configuration;
        
        IBusControl _busControl;

        public RegistrationService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.Information("Creating bus...");

            _busControl = Bus.Factory.CreateUsingRabbitMq(cfg =>
            {
                cfg.UseSerilog();
                
                var host = cfg.Host(_configuration.GetValue<Uri>("RabbitMQ.ConnectionString"), h =>
                {
                });

                cfg.ReceiveEndpoint(host, _configuration.GetValue<string>("SubmitRegistrationQueueName"), e =>
                {
                    e.PrefetchCount = 16;
                    
                    e.Consumer<SubmitRegistrationConsumer>();
                });

                cfg.ReceiveEndpoint(host, _configuration.GetValue<string>("ProcessRegistrationQueueName"), e =>
                {
                    e.PrefetchCount = 16;
                    
                    e.Consumer<ProcessRegistrationConsumer>();
                });
            });

            _logger.Information("Starting bus...");

            await _busControl.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.Information("Stopping bus...");

            await _busControl.StartAsync(cancellationToken);
        }
    }
}


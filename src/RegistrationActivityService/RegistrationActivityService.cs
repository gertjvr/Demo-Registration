namespace RegistrationActivityService
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MassTransit;
    using MassTransit.Courier;
    using MassTransit.RabbitMqTransport;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Registration.Activities.EventRegistration;
    using Registration.Activities.LicenseVerification;
    using Registration.Activities.ProcessPayment;
    using Serilog;


    public class RegistrationActivityService :
        IHostedService
    {
        readonly ILogger _logger = Log.ForContext<RegistrationActivityService>();
        readonly IConfiguration _configuration;
        
        IBusControl _busControl;

        public RegistrationActivityService(IConfiguration configuration)
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

                ConfigureExecuteActivity<LicenseVerificationActivity, LicenseVerificiationArguments>(cfg, host);

                ConfigureActivity<EventRegistrationActivity, EventRegistrationArguments, EventRegistrationLog>(cfg, host);

                ConfigureActivity<ProcessPaymentActivity, ProcessPaymentArguments, ProcessPaymentLog>(cfg, host);
            });

            _logger.Information("Starting bus...");

            await _busControl.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.Information("Stopping bus...");

            await _busControl.StopAsync(cancellationToken);
        }

        void ConfigureActivity<TActivity, TArguments, TLog>(IRabbitMqBusFactoryConfigurator cfg, IRabbitMqHost host)
            where TActivity : class, Activity<TArguments, TLog>, new()
            where TArguments : class
            where TLog : class
        {
            Uri compensateAddress = null;

            cfg.ReceiveEndpoint(host, GetCompensateActivityQueueName(typeof(TActivity)), e =>
            {
                e.PrefetchCount = 16;
                e.CompensateActivityHost<TActivity, TLog>();

                compensateAddress = e.InputAddress;
            });

            cfg.ReceiveEndpoint(host, GetExecuteActivityQueueName(typeof(TActivity)), e =>
            {
                e.PrefetchCount = 16;
                e.ExecuteActivityHost<TActivity, TArguments>(compensateAddress);
            });
        }

        void ConfigureExecuteActivity<TActivity, TArguments>(IRabbitMqBusFactoryConfigurator cfg, IRabbitMqHost host)
            where TActivity : class, ExecuteActivity<TArguments>, new()
            where TArguments : class
        {
            cfg.ReceiveEndpoint(host, GetExecuteActivityQueueName(typeof(TActivity)), e =>
            {
                e.PrefetchCount = 16;
                e.ExecuteActivityHost<TActivity, TArguments>();
            });
        }

        string GetExecuteActivityQueueName(Type activityType)
        {
            return $"execute-{activityType.Name.Replace("Activity", "").ToLowerInvariant()}";
        }

        string GetCompensateActivityQueueName(Type activityType)
        {
            return $"compensate-{activityType.Name.Replace("Activity", "").ToLowerInvariant()}";
        }
    }
}
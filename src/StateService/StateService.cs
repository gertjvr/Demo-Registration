namespace StateService
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using GreenPipes;
    using MassTransit;
    using MassTransit.EntityFrameworkCoreIntegration;
    using MassTransit.EntityFrameworkCoreIntegration.Saga;
    using MassTransit.RabbitMqTransport;
    using MassTransit.Saga;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Registration.Common;
    using Registration.Contracts;
    using Registration.Data;
    using RegistrationState;
    using Serilog;


    public class StateService :
        IHostedService
    {
        readonly ILogger _logger = Log.ForContext<StateService>();
        readonly IConfiguration _configuration;

        IBusControl _busControl;
        ISagaRepository<RegistrationStateInstance> _sagaRepository;

        public StateService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.Information("Creating bus...");

            var connectionString = _configuration.GetValue<string>("DatabaseConnectionString");

            var contextFactory = new RegistrationStateSagaDbContextFactory();

            using (var context = contextFactory.CreateDbContext(new[] {connectionString}))
            {
                context.Database.Migrate();
                
                await context.Database.EnsureCreatedAsync(cancellationToken);
            }
            
            Func<DbContext> sagaDbContextFactory = () => contextFactory.CreateDbContext(new [] { connectionString });
            
            _sagaRepository = new EntityFrameworkSagaRepository<RegistrationStateInstance>(sagaDbContextFactory);

            _busControl = Bus.Factory.CreateUsingRabbitMq(cfg =>
            {
                cfg.UseSerilog();
                
                var host = cfg.Host(_configuration.GetValue<Uri>("RabbitMQ.ConnectionString"), h =>
                {
                });

                EndpointConvention.Map<ProcessRegistration>(
                    host.Settings.HostAddress.GetDestinationAddress(_configuration.GetValue<string>("ProcessRegistrationQueueName")));

                cfg.ReceiveEndpoint(host, _configuration.GetValue<string>("RegistrationStateQueueName"), e =>
                {
                    e.PrefetchCount = 16;

                    var paritioner = cfg.CreatePartitioner(8);

                    var machine = new RegistrationStateMachine();

                    e.StateMachineSaga(machine, _sagaRepository, x =>
                    {
                        x.Message<RegistrationReceived>(m => m.UsePartitioner(paritioner, p => p.Message.SubmissionId));
                        x.Message<RegistrationCompleted>(m => m.UsePartitioner(paritioner, p => p.Message.SubmissionId));
                    });
                });
            });

            _logger.Information("Starting bus...");

            await _busControl.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.Information("Stopping bus...");

            await _busControl.StopAsync(cancellationToken);
        }
    }
}
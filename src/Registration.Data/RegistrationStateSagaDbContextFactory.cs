namespace Registration.Data
{
    using MassTransit.EntityFrameworkCoreIntegration;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Design;
    using RegistrationState;


    public class RegistrationStateSagaDbContextFactory : IDesignTimeDbContextFactory<RegistrationStateSagaDbContext>
    {
        public RegistrationStateSagaDbContext CreateDbContext(string[] args)
        {
            var dbContextOptionsBuilder = new DbContextOptionsBuilder<SagaDbContext<RegistrationStateInstance, RegistrationStateMap>>();

            dbContextOptionsBuilder.UseSqlServer(args[0],
                m =>
                {
                    var executingAssembly = typeof(RegistrationStateSagaDbContextFactory).Assembly;
                    m.MigrationsAssembly(executingAssembly.GetName().Name);
                });

            return new RegistrationStateSagaDbContext(dbContextOptionsBuilder.Options);
        }
    }
}
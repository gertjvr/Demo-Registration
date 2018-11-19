namespace Registration.Data
{
    using MassTransit.EntityFrameworkCoreIntegration;
    using Microsoft.EntityFrameworkCore;
    using RegistrationState;


    public class RegistrationStateSagaDbContext : SagaDbContext<RegistrationStateInstance, RegistrationStateMap>
    {
        public RegistrationStateSagaDbContext(DbContextOptions options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new RegistrationStateMap());
        }
    }
}
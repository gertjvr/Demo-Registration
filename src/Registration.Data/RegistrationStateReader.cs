namespace Registration.Data
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Models;
    using RegistrationState;


    public class RegistrationStateReader :
        IRegistrationStateReader
    {
        readonly Func<RegistrationStateSagaDbContext> _sagaDbContextFactory;

        public RegistrationStateReader(string connectionString)
        {
            var contextFactory = new RegistrationStateSagaDbContextFactory(); 
            
            _sagaDbContextFactory = () => contextFactory.CreateDbContext(new [] { connectionString });
        }

        public async Task<RegistrationModel> Get(Guid submissionId)
        {
            using (var dbContext = _sagaDbContextFactory())
            {
                var instance = await dbContext.Set<RegistrationStateInstance>()
                    .Where(x => x.CorrelationId == submissionId)
                    .SingleAsync().ConfigureAwait(false);

                return new RegistrationModel
                {
                    SubmissionId = instance.CorrelationId,
                    ParticipantEmailAddress = instance.ParticipantEmailAddress,
                    ParticipantCategory = instance.ParticipantCategory,
                    ParticipantLicenseNumber = instance.ParticipantLicenseNumber,
                    EventId = instance.EventId,
                    RaceId = instance.RaceId,
                    Status = instance.CurrentState
                };
            }
        }
    }
}
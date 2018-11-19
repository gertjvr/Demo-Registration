namespace RegistrationState
{
    using System;
    using System.Threading.Tasks;
    using Automatonymous;
    using GreenPipes;
    using MassTransit;
    using MassTransit.Util;
    using Registration.Contracts;
    using Serilog;


    public class RegistrationStateMachine :
        MassTransitStateMachine<RegistrationStateInstance>
    {
        static readonly ILogger _logger = Log.ForContext<RegistrationStateMachine>();

        public RegistrationStateMachine()
        {
            InstanceState(x => x.CurrentState);

            Event(() => EventRegistrationReceived, x =>
            {
                x.CorrelateById(m => m.Message.SubmissionId);
                x.SelectId(m => m.Message.SubmissionId);
            });

            Event(() => EventRegistrationCompleted, x =>
            {
                x.CorrelateById(m => m.Message.SubmissionId);
            });

            Event(() => LicenseVerificationFailed, x =>
            {
                x.CorrelateById(m => m.Message.SubmissionId);
            });

            Event(() => PaymentFailed, x =>
            {
                x.CorrelateById(m => m.Message.SubmissionId);
            });

            Initially(
                When(EventRegistrationReceived)
                    .Then(Initialize)
                    .ThenAsync(InitiateProcessing)
                    .TransitionTo(Received));

            During(Received,
                When(EventRegistrationCompleted)
                    .Then(Register)
                    .TransitionTo(Registered),
                When(LicenseVerificationFailed)
                    .Then(InvalidLicense)
                    .TransitionTo(Suspended),
                When(PaymentFailed)
                    .Then(PaymentFailure)
                    .TransitionTo(Suspended));

            During(Suspended,
                When(EventRegistrationReceived)
                    .Then(Initialize)
                    .ThenAsync(InitiateProcessing)
                    .TransitionTo(Received));
        }

        public State Received { get; }
        public State Registered { get; }
        public State Suspended { get; }

        public Event<RegistrationReceived> EventRegistrationReceived { get; }
        public Event<RegistrationCompleted> EventRegistrationCompleted { get; }
        public Event<RegistrationLicenseVerificationFailed> LicenseVerificationFailed { get; }
        public Event<RegistrationPaymentFailed> PaymentFailed { get; }

        void Initialize(BehaviorContext<RegistrationStateInstance, RegistrationReceived> context)
        {
            InitializeInstance(context.Instance, context.Data);
        }

        void Register(BehaviorContext<RegistrationStateInstance, RegistrationCompleted> context)
        {
            _logger.Information("Registered: {SubmissionId} ({ParticipantEmailAddress})",
                context.Data.SubmissionId, context.Instance.ParticipantEmailAddress);
        }

        void InvalidLicense(BehaviorContext<RegistrationStateInstance, RegistrationLicenseVerificationFailed> context)
        {
            _logger.Information("Invalid License: {SubmissionId} ({ParticipantLicenseNumber}) - {Message}", 
                context.Data.SubmissionId, context.Instance.ParticipantLicenseNumber, context.Data.ExceptionInfo.Message);
        }

        void PaymentFailure(BehaviorContext<RegistrationStateInstance, RegistrationPaymentFailed> context)
        {
            _logger.Information("Payment Failed: {SubmissionId} ({ParticipantEmailAddress}) - {Message}",
                context.Data.SubmissionId, context.Instance.ParticipantEmailAddress, context.Data.ExceptionInfo.Message);
        }

        async Task InitiateProcessing(BehaviorContext<RegistrationStateInstance, RegistrationReceived> context)
        {
            if (!EndpointConvention.TryGetDestinationAddress<ProcessRegistration>(out var destinationAddress))
            {
                throw new ConfigurationException($"The endpoint convention was not configured: {TypeMetadataCache<ProcessRegistration>.ShortName}");
            }
            
            var registration = CreateProcessRegistration(context.Data);

            await context.GetPayload<ConsumeContext>().Send(destinationAddress, registration).ConfigureAwait(false);

            _logger.Information("Processing: {SubmissionId} ({ParticipantEmailAddress})",
                context.Data.SubmissionId, context.Data.ParticipantEmailAddress);
        }

        static void InitializeInstance(RegistrationStateInstance instance, RegistrationReceived data)
        {
            _logger.Information("Initializing: {SubmissionId} ({ParticipantEmailAddress})",
                data.SubmissionId, data.ParticipantEmailAddress);

            instance.ParticipantEmailAddress = data.ParticipantEmailAddress;
            instance.ParticipantLicenseNumber = data.ParticipantLicenseNumber;
            instance.ParticipantCategory = data.ParticipantCategory;

            instance.EventId = data.EventId;
            instance.RaceId = data.RaceId;
        }

        static ProcessRegistration CreateProcessRegistration(RegistrationReceived message)
        {
            return new Process(message.SubmissionId, message.ParticipantEmailAddress, message.ParticipantLicenseNumber, message.ParticipantCategory,
                message.EventId, message.RaceId, message.CardNumber);
        }


        class Process :
            ProcessRegistration
        {
            public Process(Guid submissionId, string participantEmailAddress, string participantLicenseNumber, string participantCategory, string eventId,
                string raceId, string cardNumber)
            {
                SubmissionId = submissionId;
                ParticipantEmailAddress = participantEmailAddress;
                ParticipantLicenseNumber = participantLicenseNumber;
                ParticipantCategory = participantCategory;
                EventId = eventId;
                RaceId = raceId;
                CardNumber = cardNumber;

                Timestamp = DateTime.UtcNow;
            }

            public Guid SubmissionId { get; }
            public DateTime Timestamp { get; }
            public string ParticipantEmailAddress { get; }
            public string ParticipantLicenseNumber { get; }
            public string ParticipantCategory { get; }
            public string EventId { get; }
            public string RaceId { get; }
            public string CardNumber { get; }
        }
    }
}
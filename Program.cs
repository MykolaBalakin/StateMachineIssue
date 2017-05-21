using Automatonymous;
using MassTransit;
using MassTransit.Saga;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GreenPipes;

namespace StateMachineIssue
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var stateMachine = new StateMachine();

            var repository = new InMemorySagaRepository<StateMachineInstance>();

            var bus = Bus.Factory.CreateUsingInMemory(config =>
            {
                config.ReceiveEndpoint("queue", endpoint =>
                {
                    endpoint.StateMachineSaga(stateMachine, repository, c=>
                    {
                        c.UseConcurrencyLimit(1);
                    });
                });
            });

            bus.Start();

            var rnd = new Random();

            while (true)
            {
                var id = rnd.Next();
                bus.Publish(new InitEvent { EntityId = id }).Wait();
                bus.Publish(new FinalizeEvent { EntityId = id }).Wait();
                Console.ReadLine();
            }

            Console.ReadLine();

            bus.Stop();
        }
    }

    class StateMachine : MassTransitStateMachine<StateMachineInstance>
    {
        public Event<InitEvent> Init { get; private set; }
        public Event<FinalizeEvent> Finalize { get; private set; }

        public State Calculating { get; private set; }

        public StateMachine()
        {
            InstanceState(x => x.CurrentState);

            State(() => Calculating);

            Event(() => Init, e => e
                .CorrelateBy(i => (int?)i.EntityId, c => c.Message.EntityId)
                .SelectId(c => Guid.NewGuid()));

            Event(() => Finalize, e => e
                .CorrelateBy(i => (int?)i.EntityId, c => c.Message.EntityId)
                //.SelectId(c => Guid.NewGuid())
                .OnMissingInstance(c => c.Discard()));


            Initially(
                When(Init)
                    .Then(InitInstance)
                    .TransitionTo(Calculating)
                    .Then(LogMessage),
                Ignore(Finalize));

            During(Calculating,
                When(Finalize)
                    .Then(LogMessage)
                    .Finalize());

            SetCompletedWhenFinalized();
        }

        private void InitInstance(BehaviorContext<StateMachineInstance, InitEvent> context)
        {
            context.Instance.EntityId = context.Data.EntityId;
        }

        private void LogMessage<TEvent>(BehaviorContext<StateMachineInstance, TEvent> context)
        {
            Console.WriteLine($"{typeof(TEvent).Name} is recieved by SM");
        }
    }

    class StateMachineInstance : SagaStateMachineInstance
    {
        public Guid CorrelationId { get; set; }

        public string CurrentState { get; set; }

        public int EntityId { get; set; }
    }

    class InitEvent
    {
        public int EntityId { get; set; }
    }

    class FinalizeEvent
    {
        public int EntityId { get; set; }
    }
}

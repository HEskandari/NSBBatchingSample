using NServiceBus;
using SharedMessages;

var config = new EndpointConfiguration(EndpointNames.WorkProcessor);

config.UsePersistence<InMemoryPersistence>();
config.UseTransport<LearningTransport>();
config.UseSerialization<NewtonsoftSerializer>();
config.AuditProcessedMessagesTo("audit");
config.SendFailedMessagesTo("error");
config.LimitMessageProcessingConcurrencyTo(64);

var transport = config.UseTransport<LearningTransport>();
var routing = transport.Routing();
var _ = await config.StartWithDefaultRoutes(routing);

Console.WriteLine("Started.");
Console.WriteLine("Press [Enter] to exit.");

while (true)
{
    var pressedKey = Console.ReadKey();
    switch (pressedKey.Key)
    {
        case ConsoleKey.Enter:
        {
            Environment.Exit(0);
            break;
        }
    }
}

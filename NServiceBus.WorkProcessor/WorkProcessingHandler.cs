namespace NServiceBus.WorkProcessor;

public class WorkProcessingHandler : IHandleMessages<ProcessWorkOrder>
{
    public Task Handle(ProcessWorkOrder message, IMessageHandlerContext context)
    {
        Console.WriteLine($"Processing work order '{message.WorkOrder}'");
        Thread.Sleep(Random.Shared.Next(500, 1000));

        return context.Send(new WorkOrderCompleted
        {
            ProcessId = message.ProcessId,
            WorkOrderNo = message.WorkOrder
        });
    }
}
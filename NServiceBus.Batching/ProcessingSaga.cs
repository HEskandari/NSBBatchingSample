using System.Collections.Concurrent;

namespace NServiceBus.Batching;

public class ProcessingSaga : Saga<ProcessingSagaData>, 
    IAmStartedByMessages<StartProcessing>,
    IHandleMessages<WorkOrderCompleted>,
    IHandleMessages<WorkAllDone>
{
    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<ProcessingSagaData> mapper)
    {
        mapper.MapSaga(saga => saga.ProcessId)
            .ToMessage<StartProcessing>(msg => msg.ProcessId)
            .ToMessage<WorkOrderCompleted>(msg => msg.ProcessId)
            .ToMessage<WorkAllDone>(msg => msg.ProcessId);
    }
    
    public async Task Handle(StartProcessing message, IMessageHandlerContext context)
    {
        Console.WriteLine($"Processing saga started: '{message.ProcessId}'");
        Console.WriteLine($"Starting to process {message.WorkCount} work orders.");

        Data.WorkCount = message.WorkCount;
        Data.StartedAt = DateTime.UtcNow;
        Data.Progress = new WorkProgress();

        await ImportNextBatch(context);
    }

    private async Task ImportNextBatch(IMessageHandlerContext context)
    {
        var importedPages = Data.Progress.ImportedPages();
        var remainingPages = Data.WorkCount - importedPages;
        
        if (Data.Progress.IsAllComplete(Data.WorkCount))
        {
            await StartPostWorkProcess(context);
        }
        else if (remainingPages > 0)
        {
            var range = Enumerable.Range(importedPages + 1, remainingPages);
            var nextBatch = range.BatchWithDefaultSize().First().ToList();

            Data.Progress.StartNewBatch(nextBatch);
            await StartWork(nextBatch, context);
        }
    }

    public async Task Handle(WorkOrderCompleted message, IMessageHandlerContext context)
    {
        Data.Progress.MarkPageComplete(message.WorkOrderNo);
        
        if (Data.Progress.IsAllComplete(Data.WorkCount))
        {
            Console.WriteLine("Checking if it was the last batch of work.");
            await StartPostWorkProcess(context);
        }
        else if (Data.Progress.IsCurrentBatchCompleted())
        {
            Console.WriteLine("Importing the next batch of work.");
            await ImportNextBatch(context);
        }
    }

    public Task Handle(WorkAllDone message, IMessageHandlerContext context)
    {
        var took = DateTime.UtcNow - Data.StartedAt;
        Console.WriteLine($"All done. Took {took.TotalSeconds}");
        MarkAsComplete();
        return Task.CompletedTask;
    }

    private async Task StartWork(List<int> orders, IMessageHandlerContext context)
    {
        var orderRange = $"{orders[0]} - {orders[^1]}"; 
        Console.WriteLine($"Queueing next batch of work orders: ({orderRange}).");
        
        foreach (var order in orders)
        {
            await context.Send(new ProcessWorkOrder
            {
                ProcessId = Data.ProcessId,
                WorkOrder = order
            });
        }
    }
    
    private async Task StartPostWorkProcess(IMessageHandlerContext context)
    {
        if (Data.Progress.IsAllComplete(Data.WorkCount))
        {
            await context.SendLocal(new WorkAllDone
            {
                ProcessId = Data.ProcessId
            });
        }
    }
}

[Serializable]
public class WorkProgress
{
    public WorkProgress()
    {
        DonePages = new List<int>();
        BatchPages = new ConcurrentDictionary<int, bool>();
    }

    public List<int> DonePages { get; set; }
    public IDictionary<int, bool> BatchPages { get; set; }
    
    public void MarkPageComplete(int pageNo)
    {
        DonePages.Add(pageNo);
        BatchPages[pageNo] = true;
    }

    public bool IsAllComplete(int totalPageCount)
    {
        return DonePages.Count == totalPageCount;
    }

    public int ImportedPages()
    {
        return DonePages.Count;
    }

    public bool IsCurrentBatchCompleted()
    {
        return BatchPages.All(p => p.Value);
    }
		
    public void StartNewBatch(List<int> pages)
    {
        BatchPages.Clear();
        foreach (var p in pages)
        {
            BatchPages.Add(p, false);
        }
    }
}

public class ProcessingSagaData : ContainSagaData
{
    public Guid ProcessId { get; set; }
    public int WorkCount { get; set; }
    public WorkProgress Progress { get; set; }
    public DateTime StartedAt { get; set; }
}
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
        Console.WriteLine($"Starting the process for '{message.WorkCount}' work orders.");

        Data.WorkCount = message.WorkCount;
        Data.StartedAt = DateTime.UtcNow;
        Data.Progress = new WorkProgress();

        await ImportNextBatch(context);
    }

    private async Task ImportNextBatch(IMessageHandlerContext context)
    {
        if (Data.Progress.AllWorkCompleted(Data.WorkCount))
        {
            await FinishWork(context);
        } 
        else if (Data.Progress.HasRemainingWork(Data.WorkCount))
        {
            var importedPages = Data.Progress.ImportedPages();
            var remainingPages = Data.WorkCount - importedPages;
            var range = Enumerable.Range(importedPages + 1, remainingPages);
            var nextBatch = range.Batch(batchSize: 100).First().ToList();
            
            await SendWorkRequest(nextBatch, context);
        }
    }

    public async Task Handle(WorkOrderCompleted message, IMessageHandlerContext context)
    {
        Data.Progress.MarkWorkComplete(message.WorkOrderNo);
        
        if (Data.Progress.AllWorkCompleted(Data.WorkCount))
        {
            await FinishWork(context);
        }
        else if (Data.Progress.IsCurrentBatchCompleted())
        {
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

    private async Task SendWorkRequest(List<int> orders, IMessageHandlerContext context)
    {
        var orderRange = $"{orders[0]} - {orders[^1]}";
        Console.WriteLine($"Queueing next batch of work orders: ({orderRange}).");
        
        Data.Progress.StartNewBatch(orders);
        
        foreach (var order in orders)
        {
            await context.Send(new ProcessWorkOrder
            {
                ProcessId = Data.ProcessId,
                WorkOrder = order
            });
        }
    }
    
    private async Task FinishWork(IMessageHandlerContext context)
    {
        if (Data.Progress.AllWorkCompleted(Data.WorkCount))
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
        CompletedWork = new List<int>();
        BatchPages = new ConcurrentDictionary<int, bool>();
    }

    public List<int> CompletedWork { get; set; }
    public IDictionary<int, bool> BatchPages { get; set; }
    
    public void MarkWorkComplete(int workNo)
    {
        CompletedWork.Add(workNo);
        BatchPages[workNo] = true;
    }

    public bool AllWorkCompleted(int totalWorkCount)
    {
        return CompletedWork.Count == totalWorkCount;
    }

    public int ImportedPages()
    {
        return CompletedWork.Count;
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

    public bool HasRemainingWork(int totalWorkCount)
    {
        var importedPages = ImportedPages();
        var remainingPages = totalWorkCount - importedPages;
        return remainingPages > 0;
    }
}

public class ProcessingSagaData : ContainSagaData
{
    public ProcessingSagaData()
    {
        Progress = new WorkProgress();
    }
    
    public Guid ProcessId { get; set; }
    public int WorkCount { get; set; }
    public WorkProgress Progress { get; set; }
    public DateTime StartedAt { get; set; }
}
using NServiceBus;

public class StartProcessing : ICommand
{
    public Guid ProcessId { get; set; }
    public int WorkCount { get; set; }
}

public class ProcessWorkOrder : ICommand
{
    public Guid ProcessId { get; set; }
    public int WorkOrder { get; set; }
}

public class WorkOrderCompleted : IMessage
{
    public Guid ProcessId { get; set; }
    public int WorkOrderNo { get; set; }
}

public class WorkAllDone : IMessage
{
    public Guid ProcessId { get; set; }
}
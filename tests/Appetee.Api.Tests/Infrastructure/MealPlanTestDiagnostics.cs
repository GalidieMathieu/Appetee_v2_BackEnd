using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Appetee.Api.Tests.Infrastructure;

internal sealed record MealPlanOperation(string Operation,int SqlCommands,long ReportedRows,long ElapsedMs);

/// <summary>Captures only existing safe transaction metrics, never HTTP bodies or SQL values.</summary>
internal sealed class MealPlanTestDiagnostics : ILoggerProvider
{
    private readonly ConcurrentQueue<MealPlanOperation> _operations=new();
    internal IReadOnlyList<MealPlanOperation> Operations=>_operations.ToArray();
    internal void Clear()=>_operations.Clear();
    public ILogger CreateLogger(string category)=>new MetricLogger(this,category);
    public void Dispose() { }
    private sealed class MetricLogger(MealPlanTestDiagnostics owner,string category):ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;
        public bool IsEnabled(LogLevel level)=>category=="Appetee.Infrastructure.MealPlans.MealPlanTransaction" && level==LogLevel.Information;
        public void Log<TState>(LogLevel level,EventId eventId,TState state,Exception? exception,Func<TState,Exception?,string> formatter)
        {
            if(!IsEnabled(level) || state is not IEnumerable<KeyValuePair<string,object?>> values)return;
            var fields=values.ToDictionary(p=>p.Key,p=>p.Value);
            if(fields.TryGetValue("SqlCommands",out var commands))
                owner._operations.Enqueue(new((string)fields["Operation"]!,Convert.ToInt32(commands),Convert.ToInt64(fields["ReportedRows"]),Convert.ToInt64(fields["ElapsedMs"])));
        }
    }
}

/// <summary>Test-only response fault after the controller has committed; never retries the operation.</summary>
internal sealed class MealPlanReplyFaultFilter : IAsyncResultFilter
{
    private int _drop;
    internal void DropNextCopyReply()=>Interlocked.Exchange(ref _drop,1);
    internal void Clear()=>Interlocked.Exchange(ref _drop,0);
    public async Task OnResultExecutionAsync(ResultExecutingContext context,ResultExecutionDelegate next)
    {
        if(context.ActionDescriptor.RouteValues.TryGetValue("action",out var action) && action is "CopyDay" or "CopyWeek"
            && Interlocked.Exchange(ref _drop,0)==1)throw new IOException("Synthetic post-commit reply failure.");
        await next();
    }
}

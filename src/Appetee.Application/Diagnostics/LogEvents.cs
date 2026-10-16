using Microsoft.Extensions.Logging;

namespace Appetee.Application.Diagnostics;

/// <summary>Stable, value-free event names shared by all logging layers.</summary>
public static class LogEvents
{
    public static readonly EventId RequestCompleted = new(1000, "http.request.completed");
    public static readonly EventId RequestFailed = new(1001, "http.request.failed");
    public static readonly EventId RequestCancelled = new(1002, "http.request.cancelled");
    public static readonly EventId RequestException = new(1003, "http.exception.handled");
    public static readonly EventId Starting = new(1100, "app.starting");
    public static readonly EventId Started = new(1101, "app.started");
    public static readonly EventId Stopping = new(1102, "app.stopping");
    public static readonly EventId StartupFailed = new(1199, "app.startup.failed");
    public static readonly EventId BlobUploadCompleted = new(2000, "blob.upload.completed");
    public static readonly EventId BlobDeleteCompleted = new(2001, "blob.delete.completed");
    public static readonly EventId BlobFailed = new(2002, "blob.operation.failed");
    public static readonly EventId PlannerWriteCompleted = new(2100, "planner.write.completed");
    public static readonly EventId PlannerWriteUnchanged = new(2101, "planner.write.unchanged");
    public static readonly EventId PlannerDeadlockRetry = new(2102, "planner.write.deadlock_retry");
    public static readonly EventId PlannerWriteFailed = new(2103, "planner.write.failed");
    public static readonly EventId PlannerReadCompleted = new(2110, "planner.read.completed");
    public static readonly EventId NutritionPlanOperation = new(2120, "nutrition_plan.operation.completed");
    public static readonly EventId CatalogueOperation = new(2130, "catalogue.operation.completed");
    public static readonly EventId NutritionPreview = new(2140, "nutrition.preview.completed");
    public static readonly EventId RecoveryDeliveryFailed = new(2200, "recovery.delivery.failed");
    public static readonly EventId RecoveryCleanupFailed = new(2201, "recovery.cleanup.failed");
    public static readonly EventId AccountCleanupRecordFailed = new(2210, "account_cleanup.record.failed");
    public static readonly EventId AccountCleanupRetry = new(2211, "account_cleanup.retry_scheduled");
    public static readonly EventId AccountCleanupPassFailed = new(2212, "account_cleanup.pass.failed");

    public static string? Name(int id) => id switch
    {
        1000 => RequestCompleted.Name, 1001 => RequestFailed.Name, 1002 => RequestCancelled.Name, 1003 => RequestException.Name,
        1100 => Starting.Name, 1101 => Started.Name, 1102 => Stopping.Name, 1199 => StartupFailed.Name,
        2000 => BlobUploadCompleted.Name, 2001 => BlobDeleteCompleted.Name, 2002 => BlobFailed.Name,
        2100 => PlannerWriteCompleted.Name, 2101 => PlannerWriteUnchanged.Name, 2102 => PlannerDeadlockRetry.Name,
        2103 => PlannerWriteFailed.Name, 2110 => PlannerReadCompleted.Name, 2120 => NutritionPlanOperation.Name,
        2130 => CatalogueOperation.Name, 2140 => NutritionPreview.Name, 2200 => RecoveryDeliveryFailed.Name,
        2201 => RecoveryCleanupFailed.Name, 2210 => AccountCleanupRecordFailed.Name, 2211 => AccountCleanupRetry.Name,
        2212 => AccountCleanupPassFailed.Name, _ => null
    };
}

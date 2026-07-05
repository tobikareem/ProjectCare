namespace CarePath.Application.Abstractions.Audit;

public enum AuditAction
{
    Read = 1,
    Create = 2,
    Update = 3,
    Delete = 4,
    AccessDenied = 5,
    BackgroundJobStarted = 6,
    BackgroundJobCompleted = 7,
    BackgroundJobFailed = 8
}
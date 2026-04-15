namespace AutoPBR.App.Services;

/// <summary>Reports background work from non-UI threads; implementations must marshal to the UI thread.</summary>
public interface IBackgroundTaskSink
{
    void BeginTask(string taskId);
    void ReportTask(string taskId, double? fraction);
    void EndTask(string taskId);
}

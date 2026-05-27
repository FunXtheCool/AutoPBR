using Avalonia.Threading;

using CommunityToolkit.Mvvm.Input;

using AutoPBR.App.Lang;
using AutoPBR.App.Models;
using AutoPBR.App.Services;
using AutoPBR.Core;
using AutoPBR.Core.Models;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{
    private static string ResolveBackgroundTaskLabel(string taskId) => taskId switch
    {
        BackgroundTaskIds.MaterialTags => LocalizedStrings.BackgroundTaskMaterialTags,
        BackgroundTaskIds.ExploreCache => LocalizedStrings.BackgroundTaskExploreCache,
        _ => taskId
    };

    void IBackgroundTaskSink.BeginTask(string taskId) => BackgroundSinkBegin(taskId);

    void IBackgroundTaskSink.ReportTask(string taskId, double? fraction) => BackgroundSinkReport(taskId, fraction);

    void IBackgroundTaskSink.EndTask(string taskId) => BackgroundSinkEnd(taskId);

    private void BackgroundSinkBegin(string taskId)
    {
        void Core()
        {
            var existing = BackgroundTasks.FirstOrDefault(x => x.Id == taskId);
            if (existing is not null)
            {
                existing.Label = ResolveBackgroundTaskLabel(taskId);
                existing.IsIndeterminate = true;
                return;
            }

            BackgroundTasks.Add(new BackgroundTaskItem(taskId, ResolveBackgroundTaskLabel(taskId)));
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Core();
        }
        else
        {
            Dispatcher.UIThread.Post(Core);
        }
    }

    private void BackgroundSinkReport(string taskId, double? fraction)
    {
        void Core()
        {
            var item = BackgroundTasks.FirstOrDefault(x => x.Id == taskId);
            if (item is null)
            {
                return;
            }

            if (fraction is { } f)
            {
                item.IsIndeterminate = false;
                item.Progress = Math.Clamp(f, 0, 1);
            }
            else
            {
                item.IsIndeterminate = true;
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Core();
        }
        else
        {
            Dispatcher.UIThread.Post(Core);
        }
    }

    private void BackgroundSinkEnd(string taskId)
    {
        void Core()
        {
            var item = BackgroundTasks.FirstOrDefault(x => x.Id == taskId);
            if (item is null)
            {
                return;
            }

            BackgroundTasks.Remove(item);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Core();
        }
        else
        {
            Dispatcher.UIThread.Post(Core);
        }
    }

}

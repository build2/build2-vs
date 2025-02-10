using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace B2VS
{
    internal class OutputUtils
    {
        private static readonly Guid Build2OutputWindowPane = new Guid("{9980E4F2-35AF-4EC5-940C-CE6AFA034FB7}");

        internal static IVsWindowFrame GetOutputWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var vsUiShell = ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;
            if (vsUiShell != null)
            {
                uint flags = (uint)__VSFINDTOOLWIN.FTW_fForceCreate;
                if (vsUiShell.FindToolWindow(flags, VSConstants.StandardToolWindows.Output, out IVsWindowFrame windowFrame) == VSConstants.S_OK)
                {
                    return windowFrame;
                }
            }

            return null;
        }

        internal static IVsOutputWindowPane GetOutputWindowPane(Guid paneGuid, string nameToCreate = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsOutputWindowPane outputPane = null;
            var outputWindow = ServiceProvider.GlobalProvider.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow != null && ErrorHandler.Failed(outputWindow.GetPane(paneGuid, out outputPane)) && nameToCreate != null)
            {
                IVsWindowFrame windowFrame = GetOutputWindow();
                if (windowFrame != null)
                {
                    windowFrame.Show();
                }

                outputWindow.CreatePane(paneGuid, nameToCreate, 1, 1);
                outputWindow.GetPane(paneGuid, out outputPane);
                outputPane.Activate();
            }

            return outputPane;
        }

        internal static IVsOutputWindowPane GetBuild2CustomOutputPane()
        {
            return GetOutputWindowPane(Build2OutputWindowPane, "build2");
        }

        internal static async Task OutputWindowPaneRawAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var pane = GetBuild2CustomOutputPane();
            pane?.OutputStringThreadSafe(message);
        }

        internal static async Task OutputWindowPaneAsync(string message)
        {
            await OutputWindowPaneRawAsync(message + "\n");
        }

        internal static async Task ClearBuildOutputPaneAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var pane = GetOutputWindowPane(VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid);
            pane?.Clear();            
        }
    }
}

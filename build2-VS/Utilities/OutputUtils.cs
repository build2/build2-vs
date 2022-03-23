﻿using System;
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
        internal static async Task OutputWindowPaneRawAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IVsOutputWindowPane outputPane = null;
            var outputWindow = ServiceProvider.GlobalProvider.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow != null && ErrorHandler.Failed(outputWindow.GetPane(Build2OutputWindowPane, out outputPane)))
            {
                IVsWindowFrame windowFrame;
                var vsUiShell = ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;
                if (vsUiShell != null)
                {
                    uint flags = (uint)__VSFINDTOOLWIN.FTW_fForceCreate;
                    vsUiShell.FindToolWindow(flags, VSConstants.StandardToolWindows.Output, out windowFrame);
                    windowFrame.Show();
                }

                outputWindow.CreatePane(Build2OutputWindowPane, "build2", 1, 1);
                outputWindow.GetPane(Build2OutputWindowPane, out outputPane);
                outputPane.Activate();
            }

            outputPane?.OutputStringThreadSafe(message);
        }

        internal static async Task OutputWindowPaneAsync(string message)
        {
            await OutputWindowPaneRawAsync(message + "\n");
        }
    }
}

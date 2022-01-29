using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Extensions.VS;
using Task = System.Threading.Tasks.Task;
using System.ComponentModel.Design;
using BuildContextTypes = Microsoft.VisualStudio.Workspace.Build.BuildContextTypes;
using B2VS.VSPackage;

namespace B2VS.Contexts
{
    /// <summary>
    /// Action provider for build2 buildfiles.
    /// </summary>
    [ExportFileContextActionProvider(
        (FileContextActionProviderOptions)VsCommandActionProviderOptions.SupportVsCommands,
        ProviderType, 
        ProviderPriority.Normal, 
        PackageIds.BuildfileContextType, BuildContextTypes.BuildContextType, BuildContextTypes.BuildAllContextType)]
    internal class BuildfileActionProviderFactory : IWorkspaceProviderFactory<IFileContextActionProvider>, IVsCommandActionProvider
    {
        // Unique Guid for WordCountActionProvider.
        private const string ProviderType = "053266F0-F0C0-40D9-9FFC-94E940AABD61";

        private static readonly Guid ProviderCommandGroup = PackageIds.Build2GeneralCmdSet;
        private static readonly IReadOnlyList<CommandID> SupportedCommands = new List<CommandID>
            {
                new CommandID(ProviderCommandGroup, PackageIds.TestCmdId),
            };

        public IFileContextActionProvider CreateProvider(IWorkspace workspaceContext)
        {
            return new BuildfileActionProvider(workspaceContext);
        }

        public IReadOnlyCollection<CommandID> GetSupportedVsCommands()
        {
            return SupportedCommands;
        }

        internal class BuildfileActionProvider : IFileContextActionProvider
        {
            private static readonly Guid ActionOutputWindowPane = new Guid("{9980E4F2-35AF-4EC5-940C-CE6AFA034FB7}");
            private IWorkspace workspaceContext;

            // @NOTE: See https://docs.microsoft.com/en-us/visualstudio/extensibility/workspace-build?view=vs-2022
            private const uint BuildCommandId = 0x1000;
            private const uint RebuildCommandId = 0x1010;
            private const uint CleanCommandId = 0x1020;
            private const string BuildCommandGroupGuidStr = "16537f6e-cb14-44da-b087-d1387ce3bf57";
            private static readonly Guid BuildCommandGroupGuid = new Guid(BuildCommandGroupGuidStr);

            internal BuildfileActionProvider(IWorkspace workspaceContext)
            {
                this.workspaceContext = workspaceContext;
            }

            public Task<IReadOnlyList<IFileContextAction>> GetActionsAsync(string filePath, FileContext fileContext, CancellationToken cancellationToken)
            {
                if (fileContext.ContextType == PackageIds.BuildfileContextTypeGuid)
                {
                    return Task.FromResult<IReadOnlyList<IFileContextAction>>(new IFileContextAction[]
                    {
                    // Test command:
                    new MyContextAction(
                        fileContext,
                        new Tuple<Guid, uint>(ProviderCommandGroup, PackageIds.TestCmdId),
                        "Looks like a buildfile...", //+ fileContext.DisplayName,
                        async (fCtxt, progress, ct) =>
                        {
                            await OutputWindowPaneAsync("Yup! " + fCtxt.Context.ToString() + "\n");
                        }),
                    });
                }

                if (fileContext.ContextType == BuildContextTypes.BuildContextTypeGuid)
                {
                    return Task.FromResult<IReadOnlyList<IFileContextAction>>(new IFileContextAction[] {
                        // Build command:
                        new MyContextAction(
                            fileContext,
                            new Tuple<Guid, uint>(BuildCommandGroupGuid, BuildCommandId),
                            "", // @NOTE: Unused as the display name for the built int 'Build' action will be used.
                            async (fCtxt, progress, ct) =>
                            {
                                await OutputWindowPaneAsync("(Not) Building...\n");
                            }),
                        });
                }
                //else if (fileContext.ContextType == BuildContextTypes.BuildAllContextTypeGuid)
                //{
                //    return Task.FromResult<IReadOnlyList<IFileContextAction>>(new IFileContextAction[] {
                //        // Build All command:
                //        new MyContextAction(
                //            fileContext,
                //            new Tuple<Guid, uint>(BuildCommandGroupGuid, 0x1000), //BuildCommandId),
                //            "???",
                //            async (fCtxt, progress, ct) =>
                //            {
                //                await OutputWindowPaneAsync("(Not) Building all...\n");
                //            }),
                //        });
                //}

                throw new NotImplementedException();
            }

            internal static async Task OutputWindowPaneAsync(string message)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                IVsOutputWindowPane outputPane = null;
                var outputWindow = ServiceProvider.GlobalProvider.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                if (outputWindow != null && ErrorHandler.Failed(outputWindow.GetPane(ActionOutputWindowPane, out outputPane)))
                {
                    IVsWindowFrame windowFrame;
                    var vsUiShell = ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;
                    if (vsUiShell != null)
                    {
                        uint flags = (uint)__VSFINDTOOLWIN.FTW_fForceCreate;
                        vsUiShell.FindToolWindow(flags, VSConstants.StandardToolWindows.Output, out windowFrame);
                        windowFrame.Show();
                    }

                    outputWindow.CreatePane(ActionOutputWindowPane, "build2", 1, 1);
                    outputWindow.GetPane(ActionOutputWindowPane, out outputPane);
                    outputPane.Activate();
                }

                outputPane?.OutputStringThreadSafe(message);
            }

            internal class MyContextAction : IFileContextAction, IVsCommandItem
            {
                internal MyContextAction(
                    FileContext fileContext,
                    Tuple<Guid, uint> command,
                    string displayName,
                    Func<FileContext, IProgress<IFileContextActionProgressUpdate>, CancellationToken, Task> executeAction)
                {
                    this.CommandGroup = command.Item1;
                    this.CommandId = command.Item2;
                    this.DisplayName = displayName;
                    this.Source = fileContext;
                    this.executeAction = executeAction;
                }

                // IVsCommandItem interface
                public Guid CommandGroup { get; }
                public uint CommandId { get; }
                // End IVsCommandItem interface

                // IFileContextAction interface
                public string DisplayName { get; }
                public FileContext Source { get; }

                public async Task<IFileContextActionResult> ExecuteAsync(IProgress<IFileContextActionProgressUpdate> progress, CancellationToken cancellationToken)
                {
                    await this.executeAction(this.Source, progress, cancellationToken);
                    return new FileContextActionResult(true);
                }
                // End IFileContextAction interface

                private Func<FileContext, IProgress<IFileContextActionProgressUpdate>, CancellationToken, Task> executeAction;
            }
        }
    }
}

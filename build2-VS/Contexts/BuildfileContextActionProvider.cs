using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Build;
using Microsoft.VisualStudio.Workspace.Indexing;
using Microsoft.VisualStudio.Workspace.Extensions.VS;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;
using System.ComponentModel.Design;
using BuildContextTypes = Microsoft.VisualStudio.Workspace.Build.BuildContextTypes;
using B2VS.VSPackage;
using B2VS.Toolchain;

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

                //
                ThreadHelper.JoinableTaskFactory.Run(async delegate {
                    var configService = await workspaceContext.GetProjectConfigurationServiceAsync();
                    configService.OnBuildConfigurationChanged += async (object sender, BuildConfigurationChangedEventArgs args) =>
                    {
                        await OutputUtils.OutputWindowPaneAsync(String.Format("BuildConfigChanged! Config={0}, TargetFilePath={1}, Target={2}",
                            args.BuildConfiguration,
                            args.ProjectTargetFileContext.FilePath,
                            args.ProjectTargetFileContext.Target));
                    };
                });
                //
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
                            await OutputUtils.OutputWindowPaneAsync("Yup! " + fCtxt.Context.ToString());

                            //var configService = await workspaceContext.GetProjectConfigurationServiceAsync();
                            //string curProjectMsg = configService.CurrentProject != null ?
                            //    String.Format("Current project: Path={0}, Target={1}", configService.CurrentProject.FilePath, configService.CurrentProject.Target) :
                            //    "No current project";
                            //await OutputUtils.OutputWindowPaneAsync(curProjectMsg);
                            //string temp = String.Format("{0} configurations:\n", configService.AllProjectFileConfigurations.Count);
                            //foreach (var cfg in configService.AllProjectFileConfigurations)
                            //{
                            //    temp += string.Format("{0} | {1}\n", cfg.FilePath, cfg.Target);
                            //}
                            //await OutputUtils.OutputWindowPaneRawAsync(temp);

                            return true;
                        }),
                    });
                }

                if (fileContext.ContextType == BuildContextTypes.BuildContextTypeGuid)
                {
                    var buildCtx = fileContext.Context as Toolchain.ContextualBuildConfiguration;
                    if (buildCtx == null)
                    {
                        return Task.FromResult<IReadOnlyList<IFileContextAction>>(new IFileContextAction[] { });
                    }
                    
                    return Task.FromResult<IReadOnlyList<IFileContextAction>>(new IFileContextAction[] {
                        // Build command:
                        new MyContextAction(
                            fileContext,
                            new Tuple<Guid, uint>(BuildCommandGroupGuid, BuildCommandId),
                            "", // @NOTE: Unused as the display name for the built int 'Build' action will be used.
                            async (fCtxt, progress, ct) =>
                            {
                                OutputUtils.ClearBuildOutputPaneAsync();

                                var args = new string[] {
                                    "--verbose=2",
                                    "update",
                                    "-c", buildCtx.Configuration.ConfigDir, // apparently quoting breaks things..? String.Format("\"{0}\"", buildCtx.Configuration.ConfigDir),
                                    "-d", buildCtx.TargetPath,
                                };
                                Action<string> outputHandler = (string line) => OutputSimpleBuildMessage(workspaceContext, line + "\n");
                                var exitCode = await Toolchain.Build2Toolchain.BDep.InvokeQueuedAsync(args, cancellationToken, stdErrHandler: outputHandler); //.ConfigureAwait(false);
                                return exitCode == 0;
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
                //                await OutputWindowPaneAsync("(Not) Building all...");
                //            }),
                //        });
                //}

                throw new NotImplementedException();
            }

            internal static void OutputBuildMessage(IWorkspace workspace, BuildMessage message)
            {
                IBuildMessageService buildMessageService = workspace.GetBuildMessageService();

                if (buildMessageService != null)
                {
                    buildMessageService.ReportBuildMessages(new BuildMessage[] { message });
                }
            }

            internal static void OutputSimpleBuildMessage(IWorkspace workspace, string message)
            {
                OutputBuildMessage(workspace, new BuildMessage() {
                    Type = BuildMessage.TaskType.None, // Error,
                    LogMessage = message
                    });
            }

            internal class MyContextAction : IFileContextAction, IVsCommandItem
            {
                internal MyContextAction(
                    FileContext fileContext,
                    Tuple<Guid, uint> command,
                    string displayName,
                    Func<FileContext, IProgress<IFileContextActionProgressUpdate>, CancellationToken, Task<bool>> executeAction)
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
                    bool result = await this.executeAction(this.Source, progress, cancellationToken);
                    return new FileContextActionResult(result);
                }
                // End IFileContextAction interface

                private Func<FileContext, IProgress<IFileContextActionProgressUpdate>, CancellationToken, Task<bool>> executeAction;
            }
        }
    }
}

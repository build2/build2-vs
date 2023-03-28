using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Build;
using Microsoft.VisualStudio.Workspace.Indexing;
using Microsoft.VisualStudio.Workspace.Extensions.VS;
using Microsoft.VisualStudio.Workspace.Settings;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;
using System.ComponentModel.Design;
using BuildContextTypes = Microsoft.VisualStudio.Workspace.Build.BuildContextTypes;
using B2VS.VSPackage;
using B2VS.Toolchain;
using B2VS.Language.Manifest;

namespace B2VS.Contexts
{
    /// <summary>
    /// Action provider for build2 buildfiles.
    /// </summary>
    [ExportFileContextActionProvider(
        (FileContextActionProviderOptions)VsCommandActionProviderOptions.SupportVsCommands,
        ProviderType, 
        ProviderPriority.Normal,
        BuildContextTypes.BuildContextType,
        BuildContextTypes.RebuildContextType,
        BuildContextTypes.CleanContextType,
        PackageIds.BuildfileContextType)]
    internal class BuildfileActionProviderFactory : IWorkspaceProviderFactory<IFileContextActionProvider>, IVsCommandActionProvider
    {
        // Unique Guid for WordCountActionProvider.
        private const string ProviderType = "053266F0-F0C0-40D9-9FFC-94E940AABD61";

        private static readonly Guid ProviderCommandGroup = PackageIds.Build2GeneralCmdSet;
        private static readonly IReadOnlyList<CommandID> SupportedCommands = new List<CommandID>
            {
                //new CommandID(ProviderCommandGroup, PackageIds.TestCmdId),
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
                MyContextAction CreateMultiBuildAction(uint cmdId, IEnumerable<string[]> cmdArgs)
                {
                    return new MyContextAction(
                        fileContext,
                        new Tuple<Guid, uint>(PackageIds.BuildCommandGroupGuid, cmdId),
                        "", // @NOTE: Unused as the display name for the built in 'Build' action will be used.
                        async (fCtxt, progress, ct) =>
                        {
                            OutputUtils.ClearBuildOutputPaneAsync();

                            Action<string> outputHandler = (string line) => OutputSimpleBuildMessage(workspaceContext, line + "\n");
                            foreach (var cmd in cmdArgs)
                            {
                                var exitCode = await Build2Toolchain.BDep.InvokeQueuedAsync(cmd, cancellationToken, stdErrHandler: outputHandler); //.ConfigureAwait(false);
                                if (exitCode != 0)
                                {
                                    return false;
                                }
                            }
                            return true;
                        });
                }

                MyContextAction CreateSingleBuildAction(uint cmdId, string[] cmdArgs)
                {
                    var cmds = new List<string[]>();
                    cmds.Add(cmdArgs);
                    return CreateMultiBuildAction(cmdId, cmds);
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
                        CreateSingleBuildAction(BuildCommandId, new string[] {
                            "--verbose=2",
                            "update",
                            "-c", buildCtx.Configuration.ConfigDir, // apparently quoting breaks things..? String.Format("\"{0}\"", buildCtx.Configuration.ConfigDir),
                            "-d", buildCtx.TargetPath,
                        })
                        });
                }
                else if (fileContext.ContextType == BuildContextTypes.RebuildContextTypeGuid)
                {
                    var buildCtx = fileContext.Context as Toolchain.ContextualBuildConfiguration;
                    if (buildCtx == null)
                    {
                        return Task.FromResult<IReadOnlyList<IFileContextAction>>(new IFileContextAction[] { });
                    }

                    // Rebuild command (although this can be done with a single b invocation, it seems bdep does not have an equivalent)
                    var cmds = new List<string[]>();
                    cmds.Add(new string[] {
                        "--verbose=2",
                        "clean",
                        "-c", buildCtx.Configuration.ConfigDir, // apparently quoting breaks things..? String.Format("\"{0}\"", buildCtx.Configuration.ConfigDir),
                        "-d", buildCtx.TargetPath,
                    });
                    cmds.Add(new string[] {
                        "--verbose=2",
                        "update",
                        "-c", buildCtx.Configuration.ConfigDir, // apparently quoting breaks things..? String.Format("\"{0}\"", buildCtx.Configuration.ConfigDir),
                        "-d", buildCtx.TargetPath,
                    });
                    return Task.FromResult<IReadOnlyList<IFileContextAction>>(new IFileContextAction[] {
                        CreateMultiBuildAction(RebuildCommandId, cmds),
                        });
                }
                else if (fileContext.ContextType == BuildContextTypes.CleanContextTypeGuid)
                {
                    var buildCtx = fileContext.Context as Toolchain.ContextualBuildConfiguration;
                    if (buildCtx == null)
                    {
                        return Task.FromResult<IReadOnlyList<IFileContextAction>>(new IFileContextAction[] { });
                    }

                    return Task.FromResult<IReadOnlyList<IFileContextAction>>(new IFileContextAction[] {
                        // Clean command:
                        CreateSingleBuildAction(CleanCommandId, new string[] {
                            "--verbose=2",
                            "clean",
                            "-c", buildCtx.Configuration.ConfigDir, // apparently quoting breaks things..? String.Format("\"{0}\"", buildCtx.Configuration.ConfigDir),
                            "-d", buildCtx.TargetPath,
                        })
                        });
                }

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

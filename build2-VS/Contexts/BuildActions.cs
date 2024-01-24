using B2VS.Toolchain;
using B2VS.VSPackage;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Build;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace B2VS.Contexts
{
    internal static class BuildActions
    {
        // @NOTE: See https://docs.microsoft.com/en-us/visualstudio/extensibility/workspace-build?view=vs-2022
        private const uint BuildCommandId = 0x1000;
        private const uint RebuildCommandId = 0x1010;
        private const uint CleanCommandId = 0x1020;

        // @TODO: Via Build2VS.json
        private const int Build2Verbosity = 1;

        private static string VerbosityArg { get { return string.Format("--verbose={0}", Build2Verbosity); } }

        public static BasicContextAction CreateBuildAction(IWorkspace workspace, FileContext fileContext)
        {
            var buildCtx = fileContext.Context as ContextualBuildConfiguration;
            return CreateSingleAction(workspace, fileContext, BuildCommandId, new string[] {
                VerbosityArg,
                "update",
                "-c", buildCtx.Configuration.ConfigDir, // apparently quoting breaks things..? String.Format("\"{0}\"", buildCtx.Configuration.ConfigDir),
                "-d", buildCtx.TargetPath,
            });
        }

        public static BasicContextAction CreateRebuildAction(IWorkspace workspace, FileContext fileContext)
        {
            var buildCtx = fileContext.Context as ContextualBuildConfiguration;
            // Rebuild command (although this can be done with a single b invocation, it seems bdep does not have an equivalent)
            var cmds = new List<string[]>();
            cmds.Add(new string[] {
                VerbosityArg,
                "clean",
                "-c", buildCtx.Configuration.ConfigDir, // apparently quoting breaks things..? String.Format("\"{0}\"", buildCtx.Configuration.ConfigDir),
                "-d", buildCtx.TargetPath,
            });
            cmds.Add(new string[] {
                VerbosityArg,
                "update",
                "-c", buildCtx.Configuration.ConfigDir, // apparently quoting breaks things..? String.Format("\"{0}\"", buildCtx.Configuration.ConfigDir),
                "-d", buildCtx.TargetPath,
            });
            return CreateMultiAction(workspace, fileContext, RebuildCommandId, cmds);
        }

        public static BasicContextAction CreateCleanAction(IWorkspace workspace, FileContext fileContext)
        {
            var buildCtx = fileContext.Context as ContextualBuildConfiguration;
            return CreateSingleAction(workspace, fileContext, CleanCommandId, new string[] {
                VerbosityArg,
                "clean",
                "-c", buildCtx.Configuration.ConfigDir, // apparently quoting breaks things..? String.Format("\"{0}\"", buildCtx.Configuration.ConfigDir),
                "-d", buildCtx.TargetPath,
            });
        }

        public static BasicContextAction CreateActionFromFileContext(IWorkspace workspace, FileContext fileContext)
        {
            Debug.Assert(fileContext.Context is ContextualBuildConfiguration);

            switch (fileContext.ContextType)
            {
                case var type when (type == BuildContextTypes.BuildContextTypeGuid):
                    return CreateBuildAction(workspace, fileContext);
                case var type when (type == BuildContextTypes.RebuildContextTypeGuid):
                    return CreateRebuildAction(workspace, fileContext);
                case var type when (type == BuildContextTypes.CleanContextTypeGuid):
                    return CreateCleanAction(workspace, fileContext);
            }
            return null;
        }

        internal static BasicContextAction CreateMultiAction(IWorkspace workspace, FileContext fileContext, uint cmdId, IEnumerable<string[]> cmdArgs)
        {
            return new BasicContextAction(
                fileContext,
                new Tuple<Guid, uint>(PackageIds.BuildCommandGroupGuid, cmdId),
                "", // @NOTE: Unused as the display name for the built in 'Build' action will be used.
                async (fCtxt, progress, ct) =>
                {
                    OutputUtils.ClearBuildOutputPaneAsync();

                    Action<string> outputHandler = (string line) => OutputSimpleBuildMessage(workspace, line + "\n");
                    foreach (var cmd in cmdArgs)
                    {
                        var exitCode = await Build2Toolchain.BDep.InvokeQueuedAsync(cmd, ct, stdErrHandler: outputHandler); //.ConfigureAwait(false);
                        if (exitCode != 0)
                        {
                            return false;
                        }
                    }
                    return true;
                });
        }

        internal static BasicContextAction CreateSingleAction(IWorkspace workspace, FileContext fileContext, uint cmdId, string[] cmdArgs)
        {
            return CreateMultiAction(workspace, fileContext, cmdId, new List<string[]>
                {
                    cmdArgs
                });
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
            OutputBuildMessage(workspace, new BuildMessage()
            {
                Type = BuildMessage.TaskType.None, // Error,
                LogMessage = message
            });
        }
    }
}

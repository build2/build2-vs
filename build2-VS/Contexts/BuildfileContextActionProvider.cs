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

            const string Build2VSEnvironmentName = "build2-VS";
            const string Build2VSGeneratedEnvVarName = "BUILD2-VS-GENERATED";

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
                else if (fileContext.ContextType == PackageIds.BuildfileContextTypeGuid)
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

                            const string CppPropertiesSettingsType = "CppProperties";

                            IWorkspaceSettingsManager settingsManager = workspaceContext.GetSettingsManager();
/*                          {
                                IWorkspaceSettings settings = settingsManager.GetAggregatedSettings(CppPropertiesSettingsType);
                                int count = 0;
                                foreach (var key in settings.GetKeys())
                                {
                                    ++count;
                                }
                                await OutputUtils.OutputWindowPaneAsync(String.Format("Settings keys count: {0}", count));
                            }
*/

                            var configs = await Toolchain.Build2Configs.EnumerateBuildConfigsForProjectPathAsync(workspaceContext.Location, cancellationToken);
                            using (var persistence = await settingsManager.GetPersistanceAsync(autoCommit: true))
                            {
                                var configSettings = new List<IWorkspaceSettingsSourceWriter>();
                                var writer = await persistence.GetWriter(CppPropertiesSettingsType, ""); // @NOTE: Empty string -> workspace root.

                                var existingEnvironments = writer.PropertyArray<IWorkspaceSettingsSourceWriter>("environments");
                                var environments = existingEnvironments.ToList();
                                var b2vsEnv = environments.Find(obj => obj.Property<string>("name", "") == Build2VSEnvironmentName);
                                if (b2vsEnv == null)
                                {
                                    b2vsEnv = writer.CreateNew();
                                    b2vsEnv.SetProperty("name", Build2VSEnvironmentName);
                                    environments.Add(b2vsEnv);
                                }
                                // We specify an env var with an empty string value, so we can just use this inside auto-generated entries so we know which 
                                // entries we can safely remove without stomping on entries manually added by users.
                                // @TODO: this isn't actually implemented yet - we need to merge our updates with the existing settings.
                                b2vsEnv.SetProperty(Build2VSGeneratedEnvVarName, "");

                                var indexService = workspaceContext.GetIndexWorkspaceService();

                                var packageLocations = await Workspace.Build2Workspace.EnumeratePackageLocationsAsync(workspaceContext);
                                
                                // For each package, retrieve name and list of build configs it's in.
                                Func<string, Task<IEnumerable<Build2BuildConfiguration>>> packageLocationToBuildConfigs = async (string relPackageLocation) => //Build2Manifest manifest) =>
                                    {
                                        var absPackageLocation = Path.Combine(workspaceContext.Location, relPackageLocation);
                                        var packageManifestFilepath = Path.Combine(absPackageLocation, Build2Constants.PackageManifestFilename);
                                        var packageBuildConfigValues = await indexService.GetFileDataValuesAsync<Build2BuildConfiguration>(packageManifestFilepath, PackageIds.Build2ConfigDataValueTypeGuid);
                                        return packageBuildConfigValues.Select(entry => entry.Value);
                                    };
                                var packagesInfo = await Task.WhenAll(packageLocations.Select(async location =>
                                {
                                    // @todo: pull package name from package manifest data values (not yet implemented)
                                    //var location = entry.Value.Entries["location"];
                                    string name;
                                    // TEMPPPPPPPPPPPPP
                                    if (location == ".")
                                    {
                                        name = "test";
                                    }
                                    else
                                    {
                                        var startIdx = location.LastIndexOf('/', location.Length - 2, location.Length - 1);
                                        startIdx = startIdx == -1 ? 0 : startIdx + 1;
                                        name = location.Substring(startIdx, location.Length - startIdx - 1);
                                    }
                                    //
                                    var packageConfigs = await packageLocationToBuildConfigs(location); //entry.Value);
                                    return (name, packageConfigs);
                                }).ToList());

                                foreach (var config in configs)
                                {
                                    var configPath = config.ConfigDir;
                                    
                                    // For each config, we just issue build commands for the packages in the project; don't want to spend time
                                    // and bloat the compile commands with entries for building out-of-project dependencies.
                                    var packagesInConfig = packagesInfo
                                        .Where(entry => entry.packageConfigs.Any(cfg => cfg.BuildConfiguration == config.BuildConfiguration))
                                        .Select(entry => entry.name);

                                    var buildTargets = packagesInConfig.Select(pkgName => Path.Combine(configPath, pkgName) + '/');

                                    var compileCmds = await Build2CompileCommands.GenerateAsync(buildTargets, cancellationToken);
                                    var includePaths = compileCmds.SelectMany(perTU => perTU.IncludePaths).Distinct();
                                    var definitions = compileCmds.SelectMany(perTU => perTU.Definitions).Distinct();
                                    var compilerOptions = compileCmds.SelectMany(perTU => perTU.CompilerOptions).Distinct();

                                    //await OutputUtils.OutputWindowPaneAsync(
                                    //    String.Format("Compile commands:\nInclude:\n{0}\nDefines:\n{1}",
                                    //    String.Join("\n", includePaths),
                                    //    String.Join("\n", definitions)));

                                    var configEntry = writer.CreateNew();
                                    configEntry.SetProperty("name", config.BuildConfiguration);

                                    {
                                        var convertedPaths = new List<string>(new string[] { "${env.INCLUDE}" });
                                        convertedPaths.AddRange(includePaths.Select(path => String.Format("${{env.{0}}}{1}", Build2VSGeneratedEnvVarName, path)));
                                        configEntry.SetProperty("includePath", convertedPaths.ToArray());
                                    }
                                    {
                                        var convertedDefines = new List<string>();
                                        convertedDefines.AddRange(definitions.Select(def => String.Format("${{env.{0}}}{1}", Build2VSGeneratedEnvVarName, def)));
                                        configEntry.SetProperty("defines", convertedDefines.ToArray());
                                    }
                                    {
                                        // @note: Appears variables can't be expanded inside of the compilerSwitches property...
                                        //var convertedCompilerOptions = new List<string>();
                                        //convertedCompilerOptions.AddRange(compilerOptions.Select(opt => String.Format("${{env.{0}}}{1}", Build2VSGeneratedEnvVarName, opt)));
                                        configEntry.SetProperty("compilerSwitches", String.Join(" ", compilerOptions));// convertedCompilerOptions));
                                    }

                                    configSettings.Add(configEntry);
                                }

                                writer.SetProperty("environments", environments.ToArray());
                                writer.SetProperty("configurations", configSettings.ToArray());
                            }

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

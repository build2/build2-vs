using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using B2VS.VSPackage;
using B2VS.Workspace;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Build;
using Microsoft.VisualStudio.Workspace.Settings;
using BCLConfig = B2VS.Toolchain.Json.Bdep.Config.List.Configuration;
using BSConfigStatus = B2VS.Toolchain.Json.Bdep.Status.ConfigurationPackageStatus;

namespace B2VS.Toolchain
{
    internal static class Build2CompileCommands
    {
        public class CompileCmds<Coll>
        {
            public Coll IncludePaths { get; set; }
            public Coll Definitions { get; set; }
            public Coll CompilerOptions { get; set; }
        }

        public class PerTranslationUnitCompileCmds<Coll> : CompileCmds<Coll>
        {
            public string TranslationUnit { get; set; }
        }

        internal static bool ParseCommandToken(IEnumerator<string> tok, CompileCmds<List<string>> cmds)
        {
            const string includePrefix = "-I";
            const string definePrefix = "-D";
            
            string[] knownCompilerOptionPrefixes = new string[]{
                "/std:",
                "/experimental:",
                "/Zc:",
                "/permissive",
                "/utf8",

                "-std=",
                "--std=",
                // @todo: need to support options specified with 2 tokens, eg: --std c++20
            };

            if (tok.Current == includePrefix)
            {
                if (!tok.MoveNext())
                {
                    return false;
                }
                cmds.IncludePaths.Add(tok.Current);
            }
            else if (tok.Current.StartsWith(includePrefix))
            {
                cmds.IncludePaths.Add(tok.Current.Substring(includePrefix.Length));
            }
            else if (tok.Current == definePrefix)
            {
                if (!tok.MoveNext())
                {
                    return false;
                }
                cmds.Definitions.Add(tok.Current);
            }
            else if (tok.Current.StartsWith(definePrefix))
            {
                cmds.Definitions.Add(tok.Current.Substring(definePrefix.Length));
            }
            // @todo: generalize/additional options. also, ideally we should generate the 'compilers' section of CppProperties.json (see schema)
            else if (knownCompilerOptionPrefixes.Any(prefix => tok.Current.StartsWith(prefix)))
            {
                cmds.CompilerOptions.Add(tok.Current);
            }
            return true;
        }

        static bool IsCompilerInvocation(string command)
        {
            // @todo: more generic implementation

            if (String.IsNullOrEmpty(command))
            {
                return false;
            }

            if (command.Length >= 2 && (
                (command.First() == '\'' && command.Last() == '\'')
                || (command.First() == '\"' && command.Last() == '\"')))
            {
                command = command.Substring(1, command.Length - 2).Trim();
            }

            return command.EndsWith("cl") || command.EndsWith("cl.exe") || command.EndsWith("clang++") || command.EndsWith("clang++.exe");
        }

        internal static PerTranslationUnitCompileCmds<List<string>> ParseCommandsFromLine(string line)
        {
            var tokens = Regex.Matches(line, @"[\""].+?[\""]|[^ ]+")
                .Cast<Match>()
                .Select(m => m.Value)
                .ToList();
            if (tokens.Count < 2)
            {
                // Need at least the compiler invocation command and the source file.
                throw new ArgumentException();
            }

            var command = tokens[0];
            if (!IsCompilerInvocation(command))
            {
                // Not a compiler invocation
                return null;
            }

            var cmds = new PerTranslationUnitCompileCmds<List<string>>();
            cmds.IncludePaths = new List<string>();
            cmds.Definitions = new List<string>();
            cmds.CompilerOptions = new List<string>();
            var sourceFile = tokens.Last();
            cmds.TranslationUnit = sourceFile;

            tokens = tokens.GetRange(1, tokens.Count - 2);
            var tok = tokens.GetEnumerator();
            while(tok.MoveNext())
            {
                if (!ParseCommandToken(tok, cmds))
                {
                    break;
                }
            }
            return cmds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stream">Stream to parse. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        internal static async Task<IReadOnlyCollection<PerTranslationUnitCompileCmds<List<string>>>> ParseFromStreamAsync(System.IO.StreamReader stream, CancellationToken cancellationToken)
        {
            List<PerTranslationUnitCompileCmds<List<string>>> perTU = new List<PerTranslationUnitCompileCmds<List<string>>>();
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await stream.ReadLineAsync();
                if (line == null)
                {
                    break;
                }

                perTU.Add(ParseCommandsFromLine(line));
            }

            return perTU;
        }

        internal static async Task<IReadOnlyCollection<PerTranslationUnitCompileCmds<List<string>>>> GenerateAsync(IEnumerable<string> configPaths, CancellationToken cancellationToken)
        {
            List<PerTranslationUnitCompileCmds<List<string>>> perTU = new List<PerTranslationUnitCompileCmds<List<string>>>();
            {
                Action<string> outputHandler = (string line) =>
                    {
                        OutputUtils.OutputWindowPaneAsync(line);

                        var result = ParseCommandsFromLine(line);
                        if (result != null)
                        {
                            perTU.Add(result);
                        }
                    };
                //Action<string> errHandler = (string line) =>
                //{
                //    OutputUtils.OutputWindowPaneAsync(line);
                //};
                var quotedPaths = String.Join(" ", configPaths.Select(path => '\"' + path + '\"'));
                //foreach (var path in configPaths)
                {
                    var operation = String.Format("{{clean update}}({0})", quotedPaths);
                    var args = new string[] { "-nv", operation };
                    var exitCode = await Build2Toolchain.B.InvokeQueuedAsync(args, cancellationToken, stdOutHandler: outputHandler, stdErrHandler: outputHandler);
                    if (exitCode != 0)
                    {
                        //throw new Exception("b invocation for compile commands failed");
                        OutputUtils.OutputWindowPaneAsync("WARNING: b invocation for compile commands failed, results may be incomplete");
                    }
                }
            }
            return perTU;
        }

        const string Build2VSEnvironmentName = "build2-VS";
        const string Build2VSGeneratedEnvVarName = "BUILD2-VS-GENERATED";

        internal static async Task<bool> UpdateProjectCompilationCommandsAsync(IWorkspace workspace, string relativePath, IProgress<IFileContextActionProgressUpdate> progress, CancellationToken cancellationToken)
        {
            await OutputUtils.OutputWindowPaneAsync("Beginning regeneration of compiler commands...");

            const string CppPropertiesSettingsType = "CppProperties";

            IWorkspaceSettingsManager settingsManager = workspace.GetSettingsManager();
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

            var indexService = workspace.GetIndexWorkspaceService();

            // @NOTE: Current approach is to generate a separate CppProperties.json for each package in the workspace (irrespective of project).
            // VS will for any given source file search outwards (up to the top level workspace folder) for a CppProperties.json and use the first one it finds.
            // By generating one per package, it means we can potentially open a package subfolder and still get intellisense support, and may also give
            // an intellisense performance gain by not always using a single huge database containing all include paths.

            Func<string, bool> packageFilter = (string loc) =>
            {
                if (Build2Settings.get(workspace).GetProperty("compileCommands", out IWorkspaceSettings compileCommandsSettings)
                    == Microsoft.VisualStudio.Workspace.Settings.WorkspaceSettingsResult.Success)
                {
                    // @todo: if switch .get to using scoped settings, this doesn't really make sense since packages exist at a specific subtree.
                    // so it would seem instead a bool setting 'suppressCompileCommands' would make more sense.
                    if (compileCommandsSettings.GetProperty("ignorePackagePatterns", out string[] ignorePackagePatterns)
                        == Microsoft.VisualStudio.Workspace.Settings.WorkspaceSettingsResult.Success)
                    {
                        // @todo: consider package location vs name
                        return !ignorePackagePatterns.Any(pattern =>
                        {
                            var match = Regex.Match(loc, pattern);
                            return match.Success; // && match.Length == loc.Length;
                        });
                    }
                }
                return true;
            };

            Func<Build2BuildConfiguration, bool> configFilter = (Build2BuildConfiguration cfg) =>
            {
                if (Build2Settings.get(workspace).GetProperty("compileCommands", out IWorkspaceSettings compileCommandsSettings)
                    == Microsoft.VisualStudio.Workspace.Settings.WorkspaceSettingsResult.Success)
                {
                    if (compileCommandsSettings.GetProperty("ignoreBuildConfigPatterns", out string[] ignoreBuildConfigPatterns)
                        == Microsoft.VisualStudio.Workspace.Settings.WorkspaceSettingsResult.Success)
                    {
                        return !ignoreBuildConfigPatterns.Any(pattern =>
                        {
                            var match = Regex.Match(cfg.BuildConfiguration, pattern);
                            return match.Success && match.Length == cfg.BuildConfiguration.Length;
                        });
                    }
                }
                return true;
            };

            var unfilteredPackageLocations = await Workspace.Build2Workspace.EnumeratePackageLocationsAsync(workspace, cancellationToken: cancellationToken);
            var packageLocations = unfilteredPackageLocations.Where(packageFilter);

            // For each package, retrieve name and list of build configs it's in.
            Func<string, Task<IEnumerable<Build2BuildConfiguration>>> packageLocationToBuildConfigs = async (string relPackageLocation) => //Build2Manifest manifest) =>
            {
                var absPackageLocation = Path.Combine(workspace.Location, relPackageLocation);
                var packageManifestFilepath = Path.Combine(absPackageLocation, Build2Constants.PackageManifestFilename);
                var packageBuildConfigValues = await indexService.GetFileDataValuesAsync<Build2BuildConfiguration>(packageManifestFilepath, PackageIds.Build2ConfigDataValueTypeGuid);
                return packageBuildConfigValues.Select(entry => entry.Value);
            };

            await Task.WhenAll(packageLocations.Select(async location =>
            {
                // @todo: pull package name from package manifest data values (not yet implemented)
                // for now assuming directory name is package name
                string pkgName = Path.GetFileName(location);

                // Configurations the package is initialized in.
                var unfilteredConfigs = await packageLocationToBuildConfigs(location);
                var configs = unfilteredConfigs.Where(configFilter);

                await OutputUtils.OutputWindowPaneAsync(string.Format("Package {0}: {1}/{2} configurations passed filter.", pkgName, configs.Count(), unfilteredConfigs.Count()));

                using (var persistence = await settingsManager.GetPersistanceAsync(autoCommit: true))
                {
                    var writer = await persistence.GetWriter(CppPropertiesSettingsType, location);

                    // @todo: obviously just want to add our environment to the existing list if it doesn't already exist, but f knows how to achieve
                    // that with this insane api. if try to call set property with modified existing list it complains that "the node already has a parent".
                    // so for now we just stomp on any other entries that may have been there...

                    //var existingEnvironments = writer.PropertyArray<IWorkspaceSettingsSourceWriter>("environments");
                    //var environments = existingEnvironments.ToList();
                    //var b2vsEnv = environments.Find(obj => obj.Property<string>("name", "") == Build2VSEnvironmentName);
                    //if (b2vsEnv == null)
                    //{
                    //    b2vsEnv = writer.CreateNew();
                    //    environments.Add(b2vsEnv);
                    //}
                    //b2vsEnv.SetProperty("name", Build2VSEnvironmentName);
                    //b2vsEnv.SetProperty(Build2VSGeneratedEnvVarName, "");
                    //writer.Delete("environments");
                    //writer.SetProperty("environments", environments.ToArray());

                    {
                        var b2vsEnv = writer.CreateNew();
                        b2vsEnv.SetProperty("name", Build2VSEnvironmentName);
                        // We specify an env var with an empty string value, so we can just use this inside auto-generated entries so we know which 
                        // entries we can safely remove without stomping on entries manually added by users.
                        // @TODO: this isn't actually implemented yet - we need to merge our updates with the existing settings.
                        b2vsEnv.SetProperty(Build2VSGeneratedEnvVarName, "");

                        writer.Delete("environments");
                        writer.SetProperty("environments", new IWorkspaceSettingsSourceWriter[] { b2vsEnv });
                    }

                    writer.Delete("configurations"); // @todo: see above, should maintain anything existing that we didn't generate
                    if (configs.Count() > 0)
                    {
                        var configSettings = new List<IWorkspaceSettingsSourceWriter>();
                        foreach (var config in configs)
                        {
                            var configPath = config.ConfigDir;
                            var packageBuildPath = Path.Combine(configPath, pkgName) + '/';

                            var compileCmds = await Build2CompileCommands.GenerateAsync(new string[] { packageBuildPath }, cancellationToken);
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
                        writer.SetProperty("configurations", configSettings.ToArray());
                    }
                }
            }).ToList());


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

            await OutputUtils.OutputWindowPaneAsync("Compiler command generation completed.");

            return true;
        }
    }
}

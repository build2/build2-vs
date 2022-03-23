using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace.Build;

namespace B2VS.Toolchain
{
    internal static class Build2Configs
    {
        internal static async Task<IReadOnlyCollection<Build2BuildConfiguration>> EnumerateBuildConfigsForProjectPathAsync(string path, CancellationToken cancellationToken)
        {
            var configListOutput = new List<string>();
            {
                var args = new string[] { "config", "list", "-d", path };
                Action<string> outputHandler = (string line) => configListOutput.Add(line);
                var exitCode = await BDep.InvokeQueuedAsync(args, cancellationToken, stdOutHandler: outputHandler);
                if (exitCode != 0)
                {
                    throw new Exception("'bdep config list' failed");
                }
            }

            Func<string, Build2BuildConfiguration> parseConfigInfo = (string info) =>
            {
                // @NOTE: Quick hack. Doesn't look like output of bdep config list is reliably parsable (for example, if config dir paths have spaces).
                var tokens = info.Split(new char[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                {
                    return null;
                }
                if (tokens[0].First() == '@')
                {
                    if (tokens.Length < 2)
                    {
                        return null;
                    }
                    return new Build2BuildConfiguration(tokens[0].Substring(1), tokens[1]);
                }
                else
                {
                    return new Build2BuildConfiguration(tokens[0], tokens[0]);
                }
            };

            return configListOutput.Select(parseConfigInfo).Where(cfg => cfg != null).ToList();
        }

        internal static async Task<IReadOnlyList<T>> FilterUnorderedAsync<T>(
            this IEnumerable<T> source, Func<T, Task<bool>> predicate)
        {
            var results = new ConcurrentQueue<T>();
            var tasks = source.Select(
                async x =>
                {
                    if (await predicate(x))
                        results.Enqueue(x);
                });
            // Force serialized calls in debug builds to keep things simple.
#if DEBUG
            foreach (var t in tasks)
            {
                await t;
            }
#else
            await Task.WhenAll(tasks);
#endif
            return results.ToList();
        }

        internal static async Task<IReadOnlyCollection<Build2BuildConfiguration>> FilterBuildConfigsForPackagePathAsync(
            IEnumerable<Build2BuildConfiguration> projectConfigs, 
            string path, 
            CancellationToken cancellationToken)
        {
            Func<Build2BuildConfiguration, Task<bool>> isPackageInConfig = async (Build2BuildConfiguration cfg) =>
            {
                var args = new string[] { "status", "-d", path, "-c", cfg.ConfigDir };
                Action<string> outputHandler = (string line) => { System.Console.WriteLine(String.Format("'bdep {0}': {1}", args, line)); };
                try
                {
                    var exitCode = await BDep.InvokeQueuedAsync(args, cancellationToken, stdOutHandler: outputHandler);
                    return exitCode == 0;
                }
                catch (Exception e)
                {
                    return false;
                }
            };
            return await projectConfigs.FilterUnorderedAsync(isPackageInConfig);
        }

        internal static async Task<IReadOnlyCollection<Build2BuildConfiguration>> EnumerateBuildConfigsForPackagePathAsync(
            string projectPath,
            string packagePath,
            CancellationToken cancellationToken)
        {
            var projectConfigs = await EnumerateBuildConfigsForProjectPathAsync(projectPath, cancellationToken);
            return await FilterBuildConfigsForPackagePathAsync(projectConfigs, packagePath, cancellationToken);
        }
    }
}

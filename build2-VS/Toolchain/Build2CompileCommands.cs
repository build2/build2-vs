using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace.Build;
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

            return command.EndsWith("cl") || command.EndsWith("cl.exe");
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
    }
}

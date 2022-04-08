using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.VisualStudio.Threading;

namespace B2VS.Toolchain
{
    //public enum OutStreamId
    //{
    //    None,
    //    StdOut,
    //    StdErr,
    //}

    internal class Build2Tool
    {
        string ToolName { get; }

        public Build2Tool(string toolName)
        {
            this.ToolName = toolName;
        }
        
        internal class OutputStreamContext
        {
            public System.IO.StreamReader stream;
            public Action<string> lineHandler;

            public OutputStreamContext(System.IO.StreamReader strm, Action<string> handler)
            {
                stream = strm;
                lineHandler = handler;
            }
        }

        /// <summary>
        /// Asynchronous invocation of bdep command.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="stdOutHandler"></param>
        /// <param name="stdErrHandler"></param>
        /// <returns></returns>
        private async Task<int> InternalInvokeAsync(IEnumerable< string > args, CancellationToken cancellationToken, Action<string> stdOutHandler = null, Action<string> stdErrHandler = null)
        {
            var startInfo = new ProcessStartInfo(ToolName);
            startInfo.UseShellExecute = false;
            startInfo.Arguments = String.Join(" ", args);
            if (stdOutHandler != null || Build2Toolchain.DebugHandler != null)
            {
                startInfo.RedirectStandardOutput = true;
            }
            if (stdErrHandler != null || Build2Toolchain.DebugHandler != null)
            {
                startInfo.RedirectStandardError = true;
            }
            
            startInfo.CreateNoWindow = true;

            Build2Toolchain.DebugHandler?.Invoke(String.Format("{0} {1}", startInfo.FileName, startInfo.Arguments));

            var process = new Process();
            process.StartInfo = startInfo;
            //var tcs = new TaskCompletionSource<int>();
            //process.EnableRaisingEvents = true;
            //process.Exited += (sender, eventArgs) => tcs.TrySetResult(process.ExitCode);
            //if (cancellationToken != default(CancellationToken))
            //{
            //    cancellationToken.Register(() => {
            //        process.Kill();
            //        tcs.SetCanceled();
            //    });
            //}

            Func<OutputStreamContext, Task<OutputStreamContext>> makeStreamTask = (OutputStreamContext ctx) =>
            {
                return Task.Run(async () =>
                {
                    var line = await ctx.stream.ReadLineAsync();
                    if (line != null)
                    {
                        ctx.lineHandler?.Invoke(line);
                        Build2Toolchain.DebugHandler?.Invoke(line);
                    }
                    return ctx;
                });
            };

            process.Start();

            var activeStreams = new List<OutputStreamContext>();
            if (startInfo.RedirectStandardOutput)
            {
                activeStreams.Add(new OutputStreamContext(process.StandardOutput, stdOutHandler));
            }
            if (startInfo.RedirectStandardError)
            {
                activeStreams.Add(new OutputStreamContext(process.StandardError, stdErrHandler));
            }

            // @todo: this is a mess. think perhaps a yield-based enumerable for each stream would make this simpler?

            var outstandingReadTasks = new List<Task<OutputStreamContext>>(activeStreams.Select(ctx => makeStreamTask(ctx)));
            while (activeStreams.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    process.Kill();
                    break;
                }

                var completedTask = await Task.WhenAny(outstandingReadTasks);//.ConfigureAwait(false);
                outstandingReadTasks.Remove(completedTask);

                var context = await completedTask;
                if (context.stream.EndOfStream)
                {
                    activeStreams.Remove(context);
                }
                else
                {
                    outstandingReadTasks.Add(makeStreamTask(context));
                }
            }

            process.WaitForExit(); // @todo: should prob use above commented out approach to async wait, but this should be irrelevant in the common case
                // where we had >1 output stream handler, since in that case we've already ensured the stream is ended before getting here.
            return process.ExitCode; //tcs.Task;
        }

        //private class SerializedTaskQueue
        //{
        //    private SemaphoreSlim semaphore;

        //    public SerializedTaskQueue()
        //    {
        //        // Max one at a time
        //        semaphore = new SemaphoreSlim(1);
        //    }

        //    public async Task<T> EnqueueAsync<T>(Func<Task<T>> taskGenerator)
        //    {
        //        await semaphore.WaitAsync().ConfigureAwait(false);
        //        try
        //        {
        //            return await taskGenerator().ConfigureAwait(false);
        //        }
        //        finally
        //        {
        //            semaphore.Release();
        //        }
        //    }
        //}

        // @todo: probably better to instance this somewhere once per oped build2 project.
        //private static SerializedTaskQueue serializedQueue = new SerializedTaskQueue();
        private static NonConcurrentSynchronizationContext nonConcurrentContext = new NonConcurrentSynchronizationContext(true); // Sticky=true important to ensure that code following first await will also run on the non-concurrent context.

        public async Task<int> InvokeQueuedAsync(IEnumerable<string> args, CancellationToken cancellationToken, Action<string> stdOutHandler = null, Action<string> stdErrHandler = null)
        {
            //return serializedQueue.EnqueueAsync<int>(() => InternalInvokeAsync(args, cancellationToken, stdOutHandler, stdErrHandler));

            SynchronizationContext.SetSynchronizationContext(nonConcurrentContext);
            var task = InternalInvokeAsync(args, cancellationToken, stdOutHandler, stdErrHandler);
            //nonConcurrentContext.Post((object state) => { task.RunSynchronously(); }, null);
            //return task;
            return await task;
        }
    }

    internal static class Build2Toolchain
    {
        //static string ToolchainBinDir { get { return ""; } }
        static string B_Executable { get { return "b"; } }
        static string BPkg_Executable { get { return "bpkg"; } }
        static string BDep_Executable { get { return "bdep"; } }

        public static Action<string> DebugHandler { get; set; }

        public static Build2Tool B { get; }
        public static Build2Tool BPkg { get; }
        public static Build2Tool BDep { get; }

        static Build2Toolchain()
        {
            B = new Build2Tool(B_Executable);
            BPkg = new Build2Tool(BPkg_Executable);
            BDep = new Build2Tool(BDep_Executable);

#if true //DEBUG
            DebugHandler = (string line) => OutputUtils.OutputWindowPaneAsync(line);
#endif
        }
    }
}

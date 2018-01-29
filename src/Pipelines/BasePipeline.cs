using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Pipelines
{
    public interface INotifyException
    {
        void Notify(Exception ex);
    }

    public abstract class BasePipeline<T, U> : INotifyException
    {
        protected BlockingCollection<T> input;
        protected BlockingCollection<U> output;
        protected CancellationTokenSource cancel;

        private Task[] tasks;
        private bool completed;
        private Task completionTask;
        private bool consumerAttached;
        private INotifyException notify;
        private Exception exception;

        public BasePipeline(int processingThreads = 1, int inputCap = 0)
        {
            this.input = inputCap == 0 ? new BlockingCollection<T>() : new BlockingCollection<T>(inputCap);
            this.output = new BlockingCollection<U>();
            this.cancel = new CancellationTokenSource();

            this.tasks = Enumerable.Range(0, processingThreads).Select(CreateProcessingTask).ToArray();
            this.completed = false;
            this.completionTask = Task.Factory.StartNew(WaitForCompletion, TaskCreationOptions.LongRunning);
            this.consumerAttached = false;
        }

        protected abstract Task CreateProcessingTask(int taskId);

        private async Task WaitForCompletion()
        {
            try
            {
                await Task.WhenAll(tasks);
            }
            finally
            {
                output.CompleteAdding();
                completed = true;
                tasks = null;
                input = null;
            }
        }

        /// <summary>
        /// Add an item to the pipeline. Call CompleteAdding once all items have been added.
        /// </summary>
        /// <param name="item"></param>
        public void Add(T item)
        {
            input.Add(item);
        }

        /// <summary>
        /// Add items to the pipeline. Call CompleteAdding once all items have been added.
        /// </summary>
        /// <param name="items"></param>
        public void Add(params T[] items)
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }

        /// <summary>
        /// Add items to the pipeline. Call CompleteAdding once all items have been added.
        /// </summary>
        /// <param name="items"></param>
        public void Add(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }

        /// <summary>
        /// Signal that there are no more items to be added, and the pipeline can complete
        /// </summary>
        public void CompleteAdding()
        {
            input.CompleteAdding();
        }

        /// <summary>
        /// Indicates that all inputs have been processed to completion
        /// </summary>
        public bool Completed { get => completed; }

        /// <summary>
        /// Cancel execution of the pipeline
        /// </summary>
        public void CancelProcessing()
        {
            cancel.Cancel();
        }

        /// <summary>
        /// I encountered an error and must abort
        /// </summary>
        /// <param name="ex"></param>
        protected void CancelProcessing(Exception ex)
        {
            CancelProcessing();
            notify?.Notify(ex);
            exception = ex;
        }

        /// <summary>
        /// The parent pipeline encountered an error
        /// </summary>
        /// <param name="ex"></param>
        public void Notify(Exception ex)
        {
            CancelProcessing(ex);
            exception = ex;
        }

        /// <summary>
        /// Create a new AsyncPipeline and attach to the output of this Pipeline
        /// </summary>
        /// <typeparam name="V"></typeparam>
        /// <param name="processor"></param>
        /// <param name="processingThreads"></param>
        /// <param name="inputCap"></param>
        /// <returns></returns>
        public AsyncPipeline<U, V> AttachNew<V>(Func<U, Task<V>> processor, int processingThreads = 1, int inputCap = 0)
        {
            var pipeline = new AsyncPipeline<U, V>(processor, processingThreads, inputCap);
            AttachOutputTo(pipeline);
            return pipeline;
        }

        /// <summary>
        /// Create a new Pipeline and attach to the output of this Pipeline
        /// </summary>
        /// <typeparam name="V"></typeparam>
        /// <param name="processor"></param>
        /// <param name="processingThreads"></param>
        /// <param name="inputCap"></param>
        /// <returns></returns>
        public Pipeline<U, V> AttachNew<V>(Func<U, V> processor, int processingThreads = 1, int inputCap = 0)
        {
            var pipeline = new Pipeline<U, V>(processor, processingThreads, inputCap);
            AttachOutputTo(pipeline);
            return pipeline;
        }

        /// <summary>
        /// Attach the output of this pipeline to the input of the given destination pipeline
        /// </summary>
        /// <typeparam name="X"></typeparam>
        /// <param name="destinationPipeline"></param>
        public void AttachOutputTo<X>(BasePipeline<U, X> destinationPipeline)
        {
            this.notify = destinationPipeline;
            this.ConsumeOutput(destinationPipeline.Add).ContinueWith(t => destinationPipeline.CompleteAdding());
        }

        /// <summary>
        /// Consumes the output of the pipeline. Note: this will never finish unless the pipeline has completed!
        /// </summary>
        /// <param name="outputProcessor"></param>
        /// <returns></returns>
        public Task ConsumeOutput(Action<U> outputProcessor)
        {
            if (consumerAttached) throw new InvalidOperationException($"Consumer of pipeline is already attached");

            consumerAttached = true;

            return Task.Factory.StartNew(
                () =>
                {
                    foreach (var item in output.GetConsumingEnumerable())
                    {
                        outputProcessor(item);
                    }

                    if (exception != null)
                    {
                        throw exception;
                    }
                },
                TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Consumes the output of the pipeline and returns a list of the results. Note: this will never finish unless the pipeline has completed!
        /// </summary>
        /// <returns></returns>
        public async Task<List<U>> ToListAsync()
        {
            var results = new List<U>();
            await ConsumeOutput(item => results.Add(item));
            return results;
        }
    }
}

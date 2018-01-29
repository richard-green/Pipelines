using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pipelines
{
    /// <summary>
    /// A Pipeline that takes inputs of type <see cref="T"/>, processes them using a function, and produces outputs of type <see cref="U"/>.
    /// Output can be attached to another pipeline, or consumed directly, using the <code>ConsumeOutput</code> function.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="U"></typeparam>
    public class Pipeline<T, U> : BasePipeline<T, U>
    {
        private Func<T, U> processor;

        /// <summary>
        /// Create a new Pipeline 
        /// </summary>
        /// <param name="processor">The function to process an item of type <see cref="T"/> and return an item of type <see cref="U"/></param>
        /// <param name="processingThreads">Number of threads to create for parallel processing</param>
        /// <param name="inputCap">Maximum number of items allowed in the pipeline's input queue</param>
        public Pipeline(Func<T, U> processor, int processingThreads = 1, int inputCap = 0) : base(processingThreads, inputCap)
        {
            this.processor = processor;
        }

        protected override Task CreateProcessingTask(int taskId)
        {
            return Task.Factory.StartNew(
                () =>
                {
                    Thread.CurrentThread.Name = "PipelineThread" + taskId.ToString();

                    try
                    {
                        foreach (var item in input.GetConsumingEnumerable(cancel.Token))
                        {
                            try
                            {
                                if (item != null)
                                {
                                    var mapped = processor(item);
                                    output.Add(mapped);
                                }
                            }
                            catch (Exception ex)
                            {
                                CancelProcessing(ex);
                                break;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Ignore - the pipeline was canceled
                    }
                },
                TaskCreationOptions.LongRunning);
        }
    }
}

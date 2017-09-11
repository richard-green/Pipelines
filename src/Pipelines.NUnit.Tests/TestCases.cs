using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Pipelines.Tests
{
    [TestFixture]
    public class TestCases
    {
        [Test]
        public async Task TestSyncPipeline()
        {
            int processingThreads = 1;
            int inputCap = 0;

            var pipe1 = new Pipeline<string, string>(AddSync, processingThreads, inputCap);

            pipe1.Add("1", "2", "3");
            pipe1.CompleteAdding();

            var results = await pipe1.ToListAsync();

            Assert.IsTrue(results.Contains("1 sync"), "results.Contains('1 sync')");
            Assert.IsTrue(results.Contains("2 sync"), "results.Contains('2 sync')");
            Assert.IsTrue(results.Contains("3 sync"), "results.Contains('3 sync')");
            Assert.AreEqual(3, results.Count, "results.Count");
        }

        [Test]
        public async Task TestMultiSyncPipeline()
        {
            int processingThreads = 3;
            int inputCap = 0;

            var pipe1 = new Pipeline<string, string>(AddSync, processingThreads, inputCap);
            var pipe2 = pipe1.AttachNew(AddSync, processingThreads, inputCap);
            var pipe3 = pipe2.AttachNew(AddSync, processingThreads, inputCap);

            pipe1.Add("1", "2", "3");
            pipe1.CompleteAdding();

            var results = await pipe3.ToListAsync();

            Assert.IsTrue(results.Contains("1 sync sync sync"), "results.Contains('1 sync sync sync')");
            Assert.IsTrue(results.Contains("2 sync sync sync"), "results.Contains('2 sync sync sync')");
            Assert.IsTrue(results.Contains("3 sync sync sync"), "results.Contains('3 sync sync sync')");
            Assert.AreEqual(3, results.Count, "results.Count");
        }

        [Test]
        public async Task TestAsyncPipeline()
        {
            int processingThreads = 3;
            int inputCap = 0;

            var pipe1 = new AsyncPipeline<string, string>(AddAsync, processingThreads, inputCap);

            pipe1.Add("1", "2", "3");
            pipe1.CompleteAdding();

            var results = await pipe1.ToListAsync();

            Assert.IsTrue(results.Contains("1 async"), "results.Contains('1 async')");
            Assert.IsTrue(results.Contains("2 async"), "results.Contains('2 async')");
            Assert.IsTrue(results.Contains("3 async"), "results.Contains('3 async')");
            Assert.AreEqual(3, results.Count, "results.Count");
        }

        [Test]
        public async Task TestMultiAsyncPipeline()
        {
            int processingThreads = 3;
            int inputCap = 0;

            var pipe1 = new AsyncPipeline<string, string>(AddAsync, processingThreads, inputCap);
            var pipe2 = new AsyncPipeline<string, string>(AddAsync, processingThreads, inputCap);
            var pipe3 = new AsyncPipeline<string, string>(AddAsync, processingThreads, inputCap);

            // Manually attaching pipelines together
            pipe1.AttachOutputTo(pipe2);
            pipe2.AttachOutputTo(pipe3);

            pipe1.Add("1", "2", "3");
            pipe1.CompleteAdding();

            var results = await pipe3.ToListAsync();

            Assert.IsTrue(results.Contains("1 async async async"), "results.Contains('1 async async async')");
            Assert.IsTrue(results.Contains("2 async async async"), "results.Contains('2 async async async')");
            Assert.IsTrue(results.Contains("3 async async async"), "results.Contains('3 async async async')");
            Assert.AreEqual(3, results.Count, "results.Count");
        }

        [Test]
        public async Task TestMultiSyncAndAsyncPipeline()
        {
            int processingThreads = 3;
            int inputCap = 0;

            var pipe1 = new Pipeline<string, string>(AddSync, processingThreads, inputCap);
            var pipe2 = pipe1.AttachNew(AddAsync, processingThreads, inputCap);
            var pipe3 = pipe2.AttachNew(AddSync, processingThreads, inputCap);

            pipe1.Add("1", "2", "3");
            pipe1.CompleteAdding();

            var results = await pipe3.ToListAsync();

            Assert.IsTrue(results.Contains("1 sync async sync"), "results.Contains('1 sync async sync')");
            Assert.IsTrue(results.Contains("2 sync async sync"), "results.Contains('2 sync async sync')");
            Assert.IsTrue(results.Contains("3 sync async sync"), "results.Contains('3 sync async sync')");
            Assert.AreEqual(3, results.Count, "results.Count");
        }

        [Test]
        public async Task TestDelayedAddPipeline()
        {
            int processingThreads = 3;
            int inputCap = 0;

            var pipe1 = new Pipeline<string, string>(AddSync, processingThreads, inputCap);
            var pipe2 = pipe1.AttachNew(AddAsync, processingThreads, inputCap);
            var pipe3 = pipe2.AttachNew(AddSync, processingThreads, inputCap);

            var results = new List<string>();
            var consumerTask = pipe3.ConsumeOutput(s => results.Add(s));

            pipe1.Add("1", "2", "3");
            pipe1.CompleteAdding();

            await consumerTask;

            Assert.IsTrue(results.Contains("1 sync async sync"), "results.Contains('1 sync async sync')");
            Assert.IsTrue(results.Contains("2 sync async sync"), "results.Contains('2 sync async sync')");
            Assert.IsTrue(results.Contains("3 sync async sync"), "results.Contains('3 sync async sync')");
            Assert.AreEqual(3, results.Count, "results.Count");
        }

        [Test]
        public async Task TestInputCap()
        {
            int processingThreads = 1;
            int inputCap = 1;

            var pipe1 = new AsyncPipeline<string, string>(AddAsync, processingThreads, inputCap);

            var timer = Stopwatch.StartNew();

            pipe1.Add("1"); // First gets added to queue, then immediately consumed, queue becomes empty
            pipe1.Add("2"); // Second gets added to queue and held for 1st to finish processing
            var t = timer.ElapsedMilliseconds;

            for (int i = 3; i <= 10; i++)
            {
                // Subsequent items will find an item in the queue so will have to wait until queue has free capacity before adding the item
                pipe1.Add(i.ToString());
                var tn = timer.ElapsedMilliseconds;
                Assert.Greater(tn - t, 95, $"tn - t"); // Should take ~100ms to add another element to the input queue
                t = tn;
            }

            pipe1.CompleteAdding();

            var results = await pipe1.ToListAsync();

            Assert.AreEqual(10, results.Count, "results.Count");
        }

        [Test]
        public async Task TestCancellation()
        {
            int processingThreads = 1;
            int inputCap = 0;

            var pipe1 = new AsyncPipeline<string, string>(AddAsync, processingThreads, inputCap);

            var timer = Stopwatch.StartNew();

            pipe1.Add(Enumerable.Range(0, 100).Select(i => i.ToString()));
            pipe1.CompleteAdding();

            var resultsTask = pipe1.ToListAsync();
            var timeoutTask = Task.Delay(500);

            if (await Task.WhenAny(resultsTask, timeoutTask) == timeoutTask)
            {
                // Timeout occurred, abort
                pipe1.CancelProcessing();
                // Collect aborted results
                var results = await resultsTask;
                Assert.Less(results.Count, 10, "results.Count");
            }
            else
            {
                Assert.Fail("Did not timeout when expected");
            }
        }

        private static string AddSync(string input)
        {
            return input + " sync";
        }

        private static async Task<string> AddAsync(string input)
        {
            await Task.Delay(100);
            return input + " async";
        }

        private static string ToString(int i)
        {
            return i.ToString();
        }
    }
}

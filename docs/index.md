## Pipelines for .NET

Pipelines are an encapsulation of practices that are required when complex multi-stage operations are required to be performed on large batches of work, in an asynchronous manner.

The basic premise is, you create a pipeline that takes inputs of type T, and with a processing function that works on those items to produce an output of type U, you then add all the items into the pipeline, and they follow the defined pipeline process and arrive at the output.

Chains of pipelines can be attached to each other to push each item through consecutive pipeline processes.

Each pipeline can control the number of threads allocated to processing the work queue, with an additional parameter that serves to "cap" the number of inputs a queue can accept before the throughput is automatically throttled.

As an example, you could construct a pipeline that computes SHA1 hashes of all files in a given directory like so:

```C#
var pipeline = new Pipeline<string, FileSha1Result>(FileSha1Result.Compute, processingThreads: 4);
pipeline.Add(Directory.EnumerateFiles(@"D:\Git\Pipelines", "*", SearchOption.AllDirectories));
pipeline.CompleteAdding();
var results = await pipeline.ToListAsync();
```

In this example, the FileSha1Result class would be defined as follows:

```C#
public class FileSha1Result
{
    public string FileName { get; set; }
    public string Hash { get; set; }
    public bool Success { get; set; }

    public static FileSha1Result Compute(string fileName)
    {
        try
        {
            using (var stream = File.OpenRead(fileName))
            {
                var algo = SHA1.Create();
                var hash = algo.ComputeHash(stream);

                var results = new FileSha1Result()
                {
                    FileName = fileName,
                    Hash = BitConverter.ToString(hash),
                    Success = true
                };

                Debug.WriteLine($"{results.FileName} ==> {results.Hash}");

                return results;
            }
        }
        catch (Exception ex)
        {
            var results = new FileSha1Result()
            {
                FileName = fileName,
                Success = false
            };

            Debug.WriteLine($"{results.FileName} ==> {ex.Message}");

            return results;
        }
    }
}
```
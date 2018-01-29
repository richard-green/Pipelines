# Pipelines

Simply create a pipeline of tasks, where each step can process in parallel, using synchronous or asynchronous code

https://richard-green.github.io/Pipelines/

Quick start:

```C#
    var pipeline = new Pipeline<string, FileSha1Result>(FileSha1Result.Compute, processingThreads: 4);
    pipeline.Add(Directory.EnumerateFiles(@"D:\Git\Pipelines", "*", SearchOption.AllDirectories));
    pipeline.CompleteAdding();
    var results = await pipeline.ToListAsync();
```

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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SecureFileMonitor.Core.Services
{
    public class ThreadedHasherService : IHasherService
    {
        private readonly int _maxDegreeOfParallelism;

        public ThreadedHasherService()
        {
            // Use 75% of available cores to avoid choking the system
            _maxDegreeOfParallelism = Math.Max(1, (int)(Environment.ProcessorCount * 0.75));
        }

        public async Task<string> ComputeHashAsync(string filePath)
        {
            // For a single file, parallelizing INSIDE the file stream read is complex and often IO bound.
            // "Threaded" benefit comes from processing MULTIPLE files in parallel, 
            // OR processing blocks of a very large file in parallel (if we did Merkle tree logic).
            // For this implementation, we will stick to standard efficient async stream reading.
            // The parallelism happens at the caller level (FileScannerService) or if we implement block hashing.
            
            return await Task.Run(async () => 
            {
                using var sha256 = SHA256.Create();
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                byte[] hash = await sha256.ComputeHashAsync(stream);
                return Convert.ToHexString(hash);
            });
        }

        public async Task<string> ComputeHashAsync(Stream stream)
        {
            using var sha256 = SHA256.Create();
            byte[] hash = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hash);
        }

        public async Task<string[]> ComputeBlockHashesAsync(string filePath, int blockSize)
        {
             // This is where threading shines for a single file: Reading chunks in parallel 
             // (if Random Access is fast, e.g. NVMe) and hashing them.
             
             var fileInfo = new FileInfo(filePath);
             long fileSize = fileInfo.Length;
             int blockCount = (int)Math.Ceiling((double)fileSize / blockSize);
             string[] results = new string[blockCount];

             var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };

             await Task.Run(() => 
             {
                 Parallel.For(0, blockCount, options, i =>
                 {
                     long offset = (long)i * blockSize;
                     long size = Math.Min(blockSize, fileSize - offset);
                     
                     byte[] buffer = new byte[size];
                     
                     // Each thread needs its own stream or synchronized access. 
                     // Using independent streams is safer for parallel but heavier on handles.
                     using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                     {
                         fs.Seek(offset, SeekOrigin.Begin);
                         // CA2022: Ensure exact read
                         int bytesRead = 0;
                         while (bytesRead < size)
                         {
                            int n = fs.Read(buffer, bytesRead, (int)size - bytesRead);
                            if (n == 0) break;
                            bytesRead += n;
                         }
                     }
                     
                     using (var sha256 = SHA256.Create())
                     {
                         byte[] hash = sha256.ComputeHash(buffer);
                         results[i] = Convert.ToHexString(hash);
                     }
                 });
             });

             return results;
        }
    }
}

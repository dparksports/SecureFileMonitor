using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SecureFileMonitor.Core.Services
{
    public class CpuHasherService : IHasherService
    {
        public async Task<string> ComputeHashAsync(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            {
                return await ComputeHashAsync(stream);
            }
        }

        public async Task<string> ComputeHashAsync(Stream stream)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = await sha256.ComputeHashAsync(stream);
                return Convert.ToHexString(hashBytes);
            }
        }

        public async Task<string[]> ComputeBlockHashesAsync(string filePath, int blockSize)
        {
             List<string> hashes = new List<string>();
             byte[] buffer = new byte[blockSize];

             using (var stream = File.OpenRead(filePath))
             {
                 int bytesRead;
                 while ((bytesRead = await stream.ReadAsync(buffer, 0, blockSize)) > 0)
                 {
                     using (var sha256 = SHA256.Create())
                     {
                         // Handle last block if smaller
                         if (bytesRead < blockSize)
                         {
                             byte[] lastBlock = new byte[bytesRead];
                             Array.Copy(buffer, lastBlock, bytesRead);
                             hashes.Add(Convert.ToHexString(sha256.ComputeHash(lastBlock)));
                         }
                         else
                         {
                             hashes.Add(Convert.ToHexString(sha256.ComputeHash(buffer)));
                         }
                     }
                 }
             }
             return hashes.ToArray();
        }
    }
}

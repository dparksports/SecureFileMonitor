using System.Threading.Tasks;
using SecureFileMonitor.Core.Models;

namespace SecureFileMonitor.Core.Services
{
    public interface IMerkleTreeService
    {
        Task<MerkleNode> BuildTreeAsync(string filePath);
        string CalculateRoot(string[] leafHashes);
        List<int> GetChangedBlocks(MerkleNode oldTree, MerkleNode newTree);
    }
}

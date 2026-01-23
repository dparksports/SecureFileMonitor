using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SecureFileMonitor.Core.Models;

namespace SecureFileMonitor.Core.Services
{
    public class MerkleTreeService : IMerkleTreeService
    {
        private const int BlockSize = 4 * 1024 * 1024; // 4MB

        public string SerializeTree(MerkleNode root)
        {
            return System.Text.Json.JsonSerializer.Serialize(root);
        }

        public MerkleNode? DeserializeTree(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return System.Text.Json.JsonSerializer.Deserialize<MerkleNode>(json);
        }

        private readonly HasherFactory _hasherFactory;

        public MerkleTreeService(HasherFactory hasherFactory)
        {
            _hasherFactory = hasherFactory;
        }

        public async Task<MerkleNode> BuildTreeAsync(string filePath)
        {
            // For Merkle Tree construction, we defaulting to CPU for now for stability.
            // Future improvement: Pass in settings to use GPU.
            var hasher = _hasherFactory.Create(useGpu: false, useThreads: false);

            var leafHashes = await hasher.ComputeBlockHashesAsync(filePath, BlockSize);
            
            var leaves = leafHashes.Select((h, i) => new MerkleNode 
            { 
                Hash = h, 
                IsLeaf = true, 
                BlockIndex = i 
            }).ToList();

            if (leaves.Count == 0) return new MerkleNode { Hash = string.Empty };

            return BuildTreeRecursive(leaves);
        }

        private MerkleNode BuildTreeRecursive(List<MerkleNode> nodes)
        {
            if (nodes.Count == 1) return nodes[0];

            var parents = new List<MerkleNode>();

            for (int i = 0; i < nodes.Count; i += 2)
            {
                var left = nodes[i];
                MerkleNode? right = (i + 1 < nodes.Count) ? nodes[i + 1] : null;

                var parentHash = ComputeParentHash(left.Hash, right?.Hash);
                
                parents.Add(new MerkleNode
                {
                    Hash = parentHash,
                    Left = left,
                    Right = right,
                    IsLeaf = false
                });
            }

            return BuildTreeRecursive(parents);
        }

        public string CalculateRoot(string[] leafHashes)
        {
            // Simplified version just for root calc if needed
            if (leafHashes.Length == 0) return string.Empty;
            
            var currentLevel = leafHashes.ToList();
            while (currentLevel.Count > 1)
            {
                var nextLevel = new List<string>();
                for (int i = 0; i < currentLevel.Count; i += 2)
                {
                    string left = currentLevel[i];
                    string? right = (i + 1 < currentLevel.Count) ? currentLevel[i + 1] : null;
                    nextLevel.Add(ComputeParentHash(left, right));
                }
                currentLevel = nextLevel;
            }
            return currentLevel[0];
        }

        private string ComputeParentHash(string left, string? right)
        {
            using (var sha256 = SHA256.Create())
            {
                var valToHash = (right != null) ? (left + right) : (left + left);
                var inputBytes = Encoding.UTF8.GetBytes(valToHash);
                var hashBytes = sha256.ComputeHash(inputBytes);
                return Convert.ToHexString(hashBytes); // Fix: simplified logic to avoid complex null checks
            }
        }

        public List<int> GetChangedBlocks(MerkleNode oldTree, MerkleNode newTree)
        {
            var changes = new List<int>();
            CompareNodes(oldTree, newTree, changes);
            return changes;
        }

        private void CompareNodes(MerkleNode? node1, MerkleNode? node2, List<int> changes)
        {
            if (node1 == null && node2 == null) return;
            
            // If structure changed significantly (e.g. appended), difficult to map. 
            // Simple assumption: if hashes differ, recurse.
            
            if (node1?.Hash == node2?.Hash) return;

            if ((node1 != null && node1.IsLeaf) || (node2 != null && node2.IsLeaf))
            {
                // Leaf changed
                if (node1 != null) changes.Add(node1.BlockIndex);
                else if (node2 != null) changes.Add(node2.BlockIndex);
                return;
            }

            CompareNodes(node1?.Left, node2?.Left, changes);
            CompareNodes(node1?.Right, node2?.Right, changes);
        }
    }
}

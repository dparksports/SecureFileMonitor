using System.Collections.Generic;

namespace SecureFileMonitor.Core.Models
{
    public class MerkleNode
    {
        public string Hash { get; set; } = string.Empty;
        public MerkleNode? Left { get; set; }
        public MerkleNode? Right { get; set; }
        public bool IsLeaf { get; set; }
        public int BlockIndex { get; set; } = -1; // Only for leaves
    }
}

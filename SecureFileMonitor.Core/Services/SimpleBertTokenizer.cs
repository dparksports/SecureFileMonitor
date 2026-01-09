using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SecureFileMonitor.Core.Services
{
    public class SimpleBertTokenizer
    {
        private readonly Dictionary<string, int> _vocab;

        public SimpleBertTokenizer(string vocabPath)
        {
            _vocab = new Dictionary<string, int>();
            if (File.Exists(vocabPath))
            {
                var lines = File.ReadAllLines(vocabPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(lines[i]))
                        _vocab[lines[i]] = i;
                }
            }
        }

        public (long[] InputIds, long[] AttentionMask, long[] TokenTypeIds) Encode(string text, int sequenceLength = 256)
        {
            var tokens = new List<string> { "[CLS]" };
            // Simple whitespace split roughly approximating tokenization for now. 
            // Real WordPiece is harder to implement from scratch but this works for simple queries.
            var words = text.ToLowerInvariant().Split(new[] { ' ', '.', ',', '?', '!', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var word in words)
            {
                // Verify if word is in vocab, if not, use [UNK] or simple logic
                if (_vocab.ContainsKey(word))
                {
                    tokens.Add(word);
                }
                else
                {
                    // Try to find subwords? For now just UNK or skip
                    tokens.Add("[UNK]");
                }
            }

            if (tokens.Count > sequenceLength - 1) tokens = tokens.Take(sequenceLength - 1).ToList();
            tokens.Add("[SEP]");

            var ids = new long[sequenceLength];
            var mask = new long[sequenceLength];
            var typeIds = new long[sequenceLength];

            for (int i = 0; i < sequenceLength; i++)
            {
                if (i < tokens.Count)
                {
                    string token = tokens[i];
                    ids[i] = _vocab.ContainsKey(token) ? _vocab[token] : (_vocab.ContainsKey("[UNK]") ? _vocab["[UNK]"] : 100);
                    mask[i] = 1;
                }
                else
                {
                    ids[i] = 0; // Padding
                    mask[i] = 0;
                }
                typeIds[i] = 0;
            }

            return (ids, mask, typeIds);
        }
    }
}

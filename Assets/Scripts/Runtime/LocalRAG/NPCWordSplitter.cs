using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace NPCSystem
{
    [Serializable]
    public class NPCWordSplitter : NPCChunking
    {
        [Tooltip("number of words by which to split phrases into chunks")]
        [FormerlySerializedAs("numWords")]
        [SerializeField]
        int _numWords = 10;

        public int NumWords => _numWords;

        bool IsBoundary(char c) => char.IsPunctuation(c) || char.IsWhiteSpace(c);

        public override async Task<List<(int, int)>> Split(string input)
        {
            List<(int, int)> indices = new List<(int, int)>();
            await Task.Run(() =>
            {
                List<(int, int)> wordIndices = new List<(int, int)>();
                int startIndex = 0;
                int endIndex;
                for (int i = 0; i < input.Length; i++)
                {
                    if (i == input.Length - 1 || IsBoundary(input[i]))
                    {
                        while (i < input.Length - 1 && IsBoundary(input[i + 1]))
                            i++;
                        endIndex = i;
                        wordIndices.Add((startIndex, endIndex));
                        startIndex = i + 1;
                    }
                }

                for (int i = 0; i < wordIndices.Count; i += _numWords)
                {
                    int iTo = Math.Min(wordIndices.Count - 1, i + _numWords - 1);
                    indices.Add((wordIndices[i].Item1, wordIndices[iTo].Item2));
                }
            });
            return indices;
        }
    }
}

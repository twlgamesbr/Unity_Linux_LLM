using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using UnityEngine;

namespace NPCSystem
{
    [DefaultExecutionOrder(-2)]
    public class NPCSimpleSearch : NPCSearchMethod
    {
        protected SortedDictionary<int, float[]> embeddings = new SortedDictionary<int, float[]>();
        protected Dictionary<int, List<(int, float)>> incrementalSearchCache = new Dictionary<int, List<(int, float)>>();

        protected override void AddInternal(int key, float[] embedding)
        {
            embeddings[key] = embedding;
        }

        protected override void RemoveInternal(int key)
        {
            embeddings.Remove(key);
        }

        public override int IncrementalSearch(float[] embedding, string group = "")
        {
            int key = nextIncrementalSearchKey++;

            List<(int, float)> sortedLists = new List<(int, float)>();
            if (dataSplits.TryGetValue(group, out List<int> dataSplit))
            {
                if (dataSplit.Count >= 0)
                {
                    float[][] embeddingsSplit = new float[dataSplit.Count][];
                    for (int i = 0; i < dataSplit.Count; i++) embeddingsSplit[i] = embeddings[dataSplit[i]];

                    float[] unsortedDistances = InverseDotProduct(embedding, embeddingsSplit);
                    sortedLists = dataSplit.Zip(unsortedDistances, (first, second) => (first, second))
                        .OrderBy(item => item.Item2).ToList();
                }
            }
            incrementalSearchCache[key] = sortedLists;
            return key;
        }

        public override ValueTuple<int[], float[], bool> IncrementalFetchKeys(int fetchKey, int k)
        {
            if (!incrementalSearchCache.ContainsKey(fetchKey))
            {
                Debug.LogError($"[NPC] No IncrementalSearch cached with key: {fetchKey}");
                return (new int[0], new float[0], true);
            }

            bool completed;
            List<(int, float)> sortedLists;
            if (k == -1)
            {
                sortedLists = incrementalSearchCache[fetchKey];
                completed = true;
            }
            else
            {
                int getK = Math.Min(k, incrementalSearchCache[fetchKey].Count);
                sortedLists = incrementalSearchCache[fetchKey].GetRange(0, getK);
                incrementalSearchCache[fetchKey].RemoveRange(0, getK);
                completed = incrementalSearchCache[fetchKey].Count == 0;
            }
            if (completed) IncrementalSearchComplete(fetchKey);

            int[] results = new int[sortedLists.Count];
            float[] distances = new float[sortedLists.Count];
            for (int i = 0; i < sortedLists.Count; i++)
            {
                results[i] = sortedLists[i].Item1;
                distances[i] = sortedLists[i].Item2;
            }
            return (results.ToArray(), distances.ToArray(), completed);
        }

        public override void IncrementalSearchComplete(int fetchKey)
        {
            incrementalSearchCache.Remove(fetchKey);
        }

        protected override void ClearInternal()
        {
            embeddings.Clear();
            incrementalSearchCache.Clear();
        }

        protected override void SaveInternal(ZipArchive archive)
        {
            NPCArchiveSaver.Save(archive, embeddings, GetSavePath("embeddings"));
            NPCArchiveSaver.Save(archive, incrementalSearchCache, GetSavePath("incrementalSearchCache"));
        }

        protected override void LoadInternal(ZipArchive archive)
        {
            embeddings = NPCArchiveSaver.Load<SortedDictionary<int, float[]>>(archive, GetSavePath("embeddings"));
            incrementalSearchCache = NPCArchiveSaver.Load<Dictionary<int, List<(int, float)>>>(archive, GetSavePath("incrementalSearchCache"));
        }
    }
}

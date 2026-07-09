using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NPCSystem
{
    [Serializable]
    public abstract class NPCChunking : NPCSearchPlugin
    {
        protected bool returnChunks = false;
        protected Dictionary<string, List<int>> dataSplitToPhrases =
            new Dictionary<string, List<int>>();
        protected Dictionary<int, int[]> phraseToSentences = new Dictionary<int, int[]>();
        protected Dictionary<int, int> sentenceToPhrase = new Dictionary<int, int>();
        protected Dictionary<int, int[]> hexToPhrase = new Dictionary<int, int[]>();
        protected int nextKey = 0;

        public void ReturnChunks(bool rc) => returnChunks = rc;

        public abstract Task<List<(int, int)>> Split(string input);

        public override string Get(int key)
        {
            StringBuilder phraseBuilder = new StringBuilder();
            foreach (int sentenceId in phraseToSentences[key])
                phraseBuilder.Append(search.Get(sentenceId));
            return phraseBuilder.ToString();
        }

        public override async Task<int> Add(string inputString, string group = "")
        {
            int key = nextKey++;
            List<int> sentenceIds = new List<int>();
            foreach ((int startIndex, int endIndex) in await Split(inputString))
            {
                string sentenceText = inputString.Substring(startIndex, endIndex - startIndex + 1);
                int sentenceId = await search.Add(sentenceText, group);
                sentenceIds.Add(sentenceId);
                sentenceToPhrase[sentenceId] = key;
            }
            phraseToSentences[key] = sentenceIds.ToArray();

            if (!dataSplitToPhrases.ContainsKey(group))
                dataSplitToPhrases[group] = new List<int> { key };
            else
                dataSplitToPhrases[group].Add(key);

            int hash = inputString.GetHashCode();
            if (!hexToPhrase.TryGetValue(hash, out int[] entries))
                entries = new int[0];
            List<int> matchingHash = new List<int>(entries);
            matchingHash.Add(key);
            hexToPhrase[hash] = matchingHash.ToArray();
            return key;
        }

        public override void Remove(int key)
        {
            if (!phraseToSentences.TryGetValue(key, out int[] sentenceIds))
                return;
            int hash = Get(key).GetHashCode();
            phraseToSentences.Remove(key);
            foreach (int sentenceId in sentenceIds)
            {
                search.Remove(sentenceId);
                sentenceToPhrase.Remove(sentenceId);
            }
            foreach (var dataSplitPhrases in dataSplitToPhrases.Values)
                dataSplitPhrases.Remove(key);

            if (hexToPhrase.TryGetValue(hash, out int[] phraseIds))
            {
                List<int> updatedIds = phraseIds.ToList();
                updatedIds.Remove(key);
                if (updatedIds.Count == 0)
                    hexToPhrase.Remove(hash);
                else
                    hexToPhrase[hash] = updatedIds.ToArray();
            }
        }

        public override int Remove(string inputString, string group = "")
        {
            int hash = inputString.GetHashCode();
            if (!hexToPhrase.TryGetValue(hash, out int[] entries))
                return 0;
            List<int> removeIds = new List<int>();
            foreach (int key in entries)
            {
                if (dataSplitToPhrases[group].Contains(key) && Get(key) == inputString)
                    removeIds.Add(key);
            }
            foreach (int removeId in removeIds)
                Remove(removeId);
            return removeIds.Count;
        }

        public override int Count() => phraseToSentences.Count;

        public override int Count(string group)
        {
            if (!dataSplitToPhrases.TryGetValue(group, out List<int> dataSplitPhrases))
                return 0;
            return dataSplitPhrases.Count;
        }

        public override async Task<int> IncrementalSearch(string queryString, string group = "") =>
            await search.IncrementalSearch(queryString, group);

        public override ValueTuple<int[], float[], bool> IncrementalFetchKeys(int fetchKey, int k)
        {
            if (returnChunks)
                return search.IncrementalFetchKeys(fetchKey, k);

            List<int> phraseKeys = new List<int>();
            List<float> distancesList = new List<float>();
            bool done = false;
            bool completed;
            do
            {
                var fetchResult = search.IncrementalFetchKeys(fetchKey, k);
                int[] resultKeys = fetchResult.Item1;
                float[] distancesIter = fetchResult.Item2;
                completed = fetchResult.Item3;
                for (int i = 0; i < resultKeys.Length; i++)
                {
                    int phraseId = sentenceToPhrase[resultKeys[i]];
                    if (phraseKeys.Contains(phraseId))
                        continue;
                    phraseKeys.Add(phraseId);
                    distancesList.Add(distancesIter[i]);
                    if (phraseKeys.Count == k)
                    {
                        done = true;
                        break;
                    }
                }
                if (completed)
                    break;
            } while (!done);

            if (completed)
                IncrementalSearchComplete(fetchKey);
            return (phraseKeys.ToArray(), distancesList.ToArray(), completed);
        }

        public override ValueTuple<string[], float[], bool> IncrementalFetch(int fetchKey, int k)
        {
            (int[] resultKeys, float[] distances, bool completed) = IncrementalFetchKeys(
                fetchKey,
                k
            );
            string[] results = new string[resultKeys.Length];
            for (int i = 0; i < resultKeys.Length; i++)
                results[i] = returnChunks ? search.Get(resultKeys[i]) : Get(resultKeys[i]);
            return (results, distances, completed);
        }

        public override void IncrementalSearchComplete(int fetchKey) =>
            search.IncrementalSearchComplete(fetchKey);

        public override void Clear()
        {
            nextKey = 0;
            dataSplitToPhrases.Clear();
            phraseToSentences.Clear();
            sentenceToPhrase.Clear();
            hexToPhrase.Clear();
            search.Clear();
        }

        protected override void SaveInternal(ZipArchive archive)
        {
            NPCArchiveSaver.Save(archive, dataSplitToPhrases, GetSavePath("dataSplitToPhrases"));
            NPCArchiveSaver.Save(archive, phraseToSentences, GetSavePath("phraseToSentences"));
            NPCArchiveSaver.Save(archive, sentenceToPhrase, GetSavePath("sentenceToPhrase"));
            NPCArchiveSaver.Save(archive, hexToPhrase, GetSavePath("hexToPhrase"));
            NPCArchiveSaver.Save(archive, nextKey, GetSavePath("nextKey"));
        }

        protected override void LoadInternal(ZipArchive archive)
        {
            dataSplitToPhrases = NPCArchiveSaver.Load<Dictionary<string, List<int>>>(
                archive,
                GetSavePath("dataSplitToPhrases")
            );
            phraseToSentences = NPCArchiveSaver.Load<Dictionary<int, int[]>>(
                archive,
                GetSavePath("phraseToSentences")
            );
            sentenceToPhrase = NPCArchiveSaver.Load<Dictionary<int, int>>(
                archive,
                GetSavePath("sentenceToPhrase")
            );
            hexToPhrase = NPCArchiveSaver.Load<Dictionary<int, int[]>>(
                archive,
                GetSavePath("hexToPhrase")
            );
            nextKey = NPCArchiveSaver.Load<int>(archive, GetSavePath("nextKey"));
        }
    }
}

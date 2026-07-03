using System;
using System.IO.Compression;
using System.Threading.Tasks;
using UnityEngine;

namespace NPCSystem
{
    public enum NPCSearchMethods
    {
        SimpleSearch = 1,
    }

    public enum NPCChunkingMethods
    {
        NoChunking,
        TokenSplitter,
        WordSplitter,
        SentenceSplitter
    }

    [Serializable]
    public class NPCLocalRAG : NPCSearchable
    {
        [Tooltip("Search method type for local RAG (SimpleSearch only in this project).")]
        public NPCSearchMethods searchType = NPCSearchMethods.SimpleSearch;

        [Tooltip("Search method GameObject.")]
        public NPCSearchMethod search;

        [Tooltip("Chunking method for splitting inputs.")]
        public NPCChunkingMethods chunkingType = NPCChunkingMethods.NoChunking;

        [Tooltip("Chunking method GameObject.")]
        public NPCChunking chunking;

        public void Init(NPCSearchMethods searchMethod = NPCSearchMethods.SimpleSearch,
            NPCChunkingMethods chunkingMethod = NPCChunkingMethods.NoChunking)
        {
            searchType = searchMethod;
            chunkingType = chunkingMethod;
            UpdateGameObjects();
        }

        public void ReturnChunks(bool returnChunks)
        {
            if (chunking != null) chunking.ReturnChunks(returnChunks);
        }

        protected void ConstructSearch()
        {
            search = ConstructComponent<NPCSearchMethod>(typeof(NPCSimpleSearch),
                (previous, current) => { /* embedder assignment handled by UpdateGameObjects */ });
        }

        protected void ConstructChunking()
        {
            Type type = null;
            if (chunkingType != NPCChunkingMethods.NoChunking)
                type = Type.GetType("NPCSystem." + chunkingType.ToString());
            chunking = ConstructComponent<NPCChunking>(type);
            if (chunking != null) chunking.SetSearch(search);
        }

        public override void UpdateGameObjects()
        {
            if (this == null) return;
            ConstructSearch();
            ConstructChunking();
        }

        protected NPCSearchable GetSearcher()
        {
            if (chunking != null) return chunking;
            if (search != null) return search;
            Debug.LogError("[NPC] Local RAG search GameObject is null");
            return null;
        }

#if UNITY_EDITOR
        private void OnValidateUpdate()
        {
            UnityEditor.EditorApplication.delayCall -= OnValidateUpdate;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) UpdateGameObjects();
            };
        }
#endif

        public override string Get(int key) => GetSearcher()?.Get(key);
        public override async Task<int> Add(string inputString, string group = "")
        {
            var searcher = GetSearcher();
            if (searcher == null) return -1;
            return await searcher.Add(inputString, group);
        }
        public override int Remove(string inputString, string group = "")
            => GetSearcher()?.Remove(inputString, group) ?? 0;
        public override void Remove(int key) => GetSearcher()?.Remove(key);
        public override int Count() => GetSearcher()?.Count() ?? 0;
        public override int Count(string group) => GetSearcher()?.Count(group) ?? 0;
        public override void Clear() => GetSearcher()?.Clear();
        public override async Task<int> IncrementalSearch(string queryString, string group = "")
        {
            var searcher = GetSearcher();
            if (searcher == null) return -1;
            return await searcher.IncrementalSearch(queryString, group);
        }
        public override (string[], float[], bool) IncrementalFetch(int fetchKey, int k)
            => GetSearcher()?.IncrementalFetch(fetchKey, k) ?? (new string[0], new float[0], true);
        public override (int[], float[], bool) IncrementalFetchKeys(int fetchKey, int k)
            => GetSearcher()?.IncrementalFetchKeys(fetchKey, k) ?? (new int[0], new float[0], true);
        public override void IncrementalSearchComplete(int fetchKey)
            => GetSearcher()?.IncrementalSearchComplete(fetchKey);
        public override void Save(ZipArchive archive) => GetSearcher()?.Save(archive);
        public override void Load(ZipArchive archive) => GetSearcher()?.Load(archive);
    }
}

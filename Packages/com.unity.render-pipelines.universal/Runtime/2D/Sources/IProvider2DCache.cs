using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    internal interface IProvider2DCache
    {
        public IEnumerable<Provider2DKVPair> Cache { get; }
        public void UpdateCache(GameObject gameObj);
    }
}

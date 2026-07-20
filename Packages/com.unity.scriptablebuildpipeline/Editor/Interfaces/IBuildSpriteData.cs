using System;
using System.Collections.Generic;
using UnityEditor.Build.Content;
using UnityEngine; // explicitly imported for GUID backwards compatibility

namespace UnityEditor.Build.Pipeline.Interfaces
{
    /// <summary>
    /// The importer data about a sprite asset.
    /// </summary>
    [Serializable]
    public class SpriteImporterData
    {
        /// <summary>
        /// Property if this sprite asset is packed by the sprite packer.
        /// </summary>
        public bool PackedSprite { get; set; }

        /// <summary>
        /// Object identifier of the source texture for the sprite.
        /// </summary>
        public ObjectIdentifier SourceTexture { get; set; }
    }

    /// <summary>
    /// Base interface for the storing sprite importer data for sprite assets.
    /// </summary>
    public interface IBuildSpriteData : IContextObject
    {
        /// <summary>
        /// Map of sprite asset to importer data.
        /// </summary>
        Dictionary<GUID, SpriteImporterData> ImporterData { get; }
    }
}

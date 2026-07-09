using System.Collections.Generic;
using UnityEditor;

namespace GladeAgenticAI.Core.Tools.Implementations.Assets
{
    public class RefreshAssetDatabaseTool : ITool
    {
        public string Name => "refresh_asset_database";

        public string Execute(Dictionary<string, object> args)
        {
            AssetDatabase.Refresh();
            return ToolUtils.CreateSuccessResponse("AssetDatabase refreshed");
        }
    }
}

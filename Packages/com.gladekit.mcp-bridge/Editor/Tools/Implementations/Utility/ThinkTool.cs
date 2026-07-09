using System.Collections.Generic;

namespace GladeAgenticAI.Core.Tools.Implementations.Utility
{
    public class ThinkTool : ITool
    {
        public string Name => "think";

        public string Execute(Dictionary<string, object> args)
        {
            string thought = args.ContainsKey("thought") ? args["thought"]?.ToString() : "";
            if (string.IsNullOrEmpty(thought))
                return ToolUtils.CreateErrorResponse("thought is required");
            return ToolUtils.CreateSuccessResponse("Thinking logged");
        }
    }
}

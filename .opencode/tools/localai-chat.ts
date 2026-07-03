import { tool } from "@opencode-ai/plugin"
import { join } from "path"

export default tool({
  description: "Chat with a LocalAI model directly. Use small models (qwen2.5-1.5b, llama-3.2-3b) for bulk work, medium (llama-3.1-8b) for quality.",
  args: {
    model: tool.schema.string().describe("Model name: qwen2.5-1.5b-instruct-q4-k-m, llama-3.2-3b-instruct:q8_0, llama-3.1-8b-q4-k-m, gemma-4-e2b-it, or modal-vllm-qwen"),
    prompt: tool.schema.string().describe("The prompt to send"),
    temperature: tool.schema.number().optional().describe("Temperature 0-1 (default 0.7)"),
  },
  async execute(args, context) {
    const script = join(context.worktree, ".opencode/scripts/localai.sh")
    const temp = args.temperature ?? 0.7
    const proc = Bun.$`bash ${script} chat ${args.model} ${args.prompt} ${temp}`
    const result = (await proc.text()).trim()
    return result || "No response from model."
  },
})

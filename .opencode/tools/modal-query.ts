import { tool } from "@opencode-ai/plugin"

export default tool({
  description: "Query the Modal vLLM endpoint (Qwen3-8B on H100) or Modal Ollama (qwen2.5:32b on A100) directly for heavy inference tasks",
  args: {
    model: tool.schema.string().describe("Model: 'qwen3-8b' for vLLM (default) or 'qwen2.5:32b' for Ollama"),
    prompt: tool.schema.string().describe("The prompt to send"),
  },
  async execute(args) {
    const model = args.model || "qwen3-8b"

    if (model === "qwen3-8b") {
      // Route through LocalAI gateway which proxies to Modal vLLM
      const proc = Bun.$`curl -s http://localhost:8080/v1/chat/completions \
        -H 'Content-Type: application/json' \
        -d '{"model":"modal-vllm-qwen","messages":[{"role":"user","content":${args.prompt}}],"stream":false}'`
      const raw = (await proc.text()).trim()
      const data = JSON.parse(raw)
      return data.choices?.[0]?.message?.content || JSON.stringify(data.error || data)
    } else {
      // Direct Modal Ollama endpoint
      const proc = Bun.$`curl -s https://andretwl--example-ollama-llama-serve.modal.run/v1/chat/completions \
        -H 'Content-Type: application/json' \
        -d '{"model":"qwen2.5:32b","messages":[{"role":"user","content":${args.prompt}}],"stream":false}'`
      const raw = (await proc.text()).trim()
      const data = JSON.parse(raw)
      return data.choices?.[0]?.message?.content || JSON.stringify(data.error || data)
    }
  },
})

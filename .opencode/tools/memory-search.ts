import { tool } from "@opencode-ai/plugin"
import { readFileSync } from "fs"
import { join } from "path"

export default tool({
  description: "Semantic search across agent memory using LocalAI embeddings and cosine similarity",
  args: {
    query: tool.schema.string().describe("Search query"),
    topK: tool.schema.number().optional().describe("Number of results (default 5)"),
  },
  async execute(args, context) {
    const memoryDir = join(context.worktree, ".opencode/memory")

    // Get embedding from LocalAI
    const embedProc = Bun.$`bash ${join(context.worktree, ".opencode/scripts/localai.sh")} embed ${args.query}`
    const embeddingJson = (await embedProc.text()).trim()
    const queryEmbedding = JSON.parse(embeddingJson) as number[]

    // Load index
    const indexRaw = readFileSync(join(memoryDir, "index.json"), "utf-8")
    const index = JSON.parse(indexRaw)

    if (!index.entries.length) {
      return "No memory entries found in index."
    }

    // Cosine similarity
    function cosineSimilarity(a: number[], b: number[]): number {
      let dot = 0, magA = 0, magB = 0
      for (let i = 0; i < a.length; i++) {
        dot += a[i] * b[i]
        magA += a[i] * a[i]
        magB += b[i] * b[i]
      }
      return dot / (Math.sqrt(magA) * Math.sqrt(magB))
    }

    const scored = index.entries
      .map((entry: any) => ({
        ...entry,
        score: cosineSimilarity(queryEmbedding, entry.embedding as number[]),
      }))
      .sort((a: any, b: any) => b.score - a.score)
      .slice(0, args.topK || 5)

    if (scored.length === 0) {
      return "No matches found."
    }

    let result = `Top ${scored.length} memory matches for "${args.query}":\n\n`
    for (const entry of scored) {
      result += `[${(entry.score * 100).toFixed(1)}%] ${entry.text}\n`
      result += `   → ${entry.source} (${entry.timestamp})\n\n`
    }
    return result
  },
})

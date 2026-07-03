import { tool } from "@opencode-ai/plugin"
import { readFileSync, writeFileSync } from "fs"
import { join } from "path"

export default tool({
  description: "Store a fact in long-term memory with automatic embedding",
  args: {
    fact: tool.schema.string().describe("The fact to remember"),
    category: tool.schema.string().optional().describe("Category (e.g. system, project, user, decision)"),
  },
  async execute(args, context) {
    const memoryDir = join(context.worktree, ".opencode/memory")
    const longTermPath = join(memoryDir, "long-term.json")
    const indexPath = join(memoryDir, "index.json")
    const timestamp = new Date().toISOString()
    const category = args.category || "general"

    // Get embedding
    const embedProc = Bun.$`bash ${join(context.worktree, ".opencode/scripts/localai.sh")} embed ${args.fact}`
    const embeddingJson = (await embedProc.text()).trim()
    const embedding = JSON.parse(embeddingJson) as number[]

    // Update long-term.json
    const longTerm = JSON.parse(readFileSync(longTermPath, "utf-8"))
    const factId = longTerm.facts.length + 1
    longTerm.facts.push({
      id: factId,
      timestamp,
      fact: args.fact,
      category,
    })
    longTerm.session_count++
    longTerm.last_session = timestamp
    writeFileSync(longTermPath, JSON.stringify(longTerm, null, 2))

    // Update index.json
    const index = JSON.parse(readFileSync(indexPath, "utf-8"))
    index.entries.push({
      id: `fact_${factId}`,
      text: args.fact,
      category,
      timestamp,
      source: "long-term.json",
      embedding,
    })
    writeFileSync(indexPath, JSON.stringify(index, null, 2))

    return `Stored fact #${factId} [${category}]: ${args.fact}`
  },
})

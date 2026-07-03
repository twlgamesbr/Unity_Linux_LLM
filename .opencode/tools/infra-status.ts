import { tool } from "@opencode-ai/plugin"

export default tool({
  description: "Check Docker container status for project infrastructure (LocalAI, Qdrant, PostgreSQL, NPC server)",
  args: {
    container: tool.schema.string().optional().describe("Filter by container name (optional)"),
  },
  async execute(args) {
    const filter = args.container ? ` --filter name=${args.container}` : ""
    const proc = Bun.$`docker ps${filter} --format '{{json .}}'`
    const raw = (await proc.text()).trim()
    if (!raw) return "No running containers found."

    const lines = raw.split("\n")
    const containers = lines.map((line: string) => JSON.parse(line))

    let result = `Found ${containers.length} running container(s):\n\n`
    for (const c of containers) {
      const ports = c.Ports || "none"
      result += `  ${c.Names}\n`
      result += `    Image:  ${c.Image}\n`
      result += `    Ports:  ${ports}\n`
      result += `    Status: ${c.Status}\n`
      result += `    State:  ${c.State}\n\n`
    }
    return result
  },
})

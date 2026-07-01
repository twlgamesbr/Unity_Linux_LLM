# Architecture Diagrams Reference

This directory contains professional architecture and data flow diagrams for the NPC LLM Dialogue system.

## Diagram Categories

### Architecture Diagrams

These diagrams show the high-level system structure and component relationships.

**System Context Diagram** (Mermaid)
- Shows external systems and integration points
- Displays data flow between Unity game, LocalAI, Qdrant, Cognee, GladeKit
- Useful for understanding what this system connects to

**Layer Architecture Diagram** (Mermaid)
- Shows internal layer stack from UI to infrastructure
- Displays dependencies between layers
- Useful for understanding internal design

**Component Interaction Diagram** (Mermaid)
- Shows how key components (Manager, Profile, Planner, RAG, LLM) interact
- Traces dialogue flow through components
- Useful for understanding dialogue lifecycle

### Data Flow Diagrams

**Complete Dialogue Exchange**
- Shows sequence of steps from player input to response
- Includes async patterns and event emissions
- Useful for understanding request lifecycle

**Network Multiplayer Dialogue**
- Shows client-server architecture
- Displays WebSocket communication
- Useful for understanding distributed scenarios

**RAG Context Retrieval**
- Shows how queries are embedded and searched
- Displays vector similarity matching
- Useful for understanding semantic search

### Integration Diagrams

**LLMUnity Integration**
- Shows how models are loaded and inference executed
- Displays local vs remote options
- Useful for understanding LLM provider selection

**LocalAI Integration**
- Shows HTTP API communication
- Displays model management
- Useful for remote inference setup

**Qdrant Integration**
- Shows vector database communication
- Displays collection management
- Useful for RAG setup

## How to Use These Diagrams

1. **New to the system?** Start with System Context Diagram
2. **Understanding a specific component?** Check Component Interaction Diagram
3. **Debugging data flow?** Look at Data Flow Diagrams
4. **Setting up external services?** Check Integration Diagrams

## Diagram Formats

All diagrams are created in **Mermaid** format (embedded in Markdown):

- **Editable**: Can be edited directly in the Markdown files
- **Version Controllable**: Git-friendly text format
- **Renderable**: Display properly on GitHub, GitLab, Mermaid Live Editor
- **Convertible**: Can be exported to PNG/SVG/PDF using Mermaid CLI

### Exporting Diagrams

To export diagrams to PNG or PDF:

```bash
# Install Mermaid CLI
npm install -g @mermaid-js/mermaid-cli

# Export a diagram from markdown
mmdc -i README.md -o diagram.png

# Or convert individual Mermaid files
mmdc -i system-context.mmd -o system-context.png -t dark
```

## Diagram Maintenance

When the system architecture changes:

1. Update relevant diagrams in their Markdown source
2. Re-export PNG/PDF files if external sharing needed
3. Add version note (date and change description)
4. Keep this README updated with new diagrams

## Quick Reference

| Diagram | Use Case | Located In |
|---------|----------|-----------|
| System Context | External integrations | [2_Architecture/README.md](../2_Architecture/README.md) |
| Layer Architecture | Internal design | [2_Architecture/README.md](../2_Architecture/README.md) |
| Component Interaction | Dialogue lifecycle | [2_Architecture/README.md](../2_Architecture/README.md) |
| Dialogue Exchange | Request flow | [2_Architecture/README.md](../2_Architecture/README.md) |
| Multiplayer Network | Client-server | [2_Architecture/README.md](../2_Architecture/README.md) |
| RAG Retrieval | Vector search | [2_Architecture/README.md](../2_Architecture/README.md) |
| LLMUnity | Model loading | [4_Integration_Guides/README.md](../4_Integration_Guides/README.md) |
| LocalAI | Remote inference | [4_Integration_Guides/README.md](../4_Integration_Guides/README.md) |
| Qdrant | Vector database | [4_Integration_Guides/README.md](../4_Integration_Guides/README.md) |

---

**Tip**: All diagrams are created using Mermaid syntax and are embedded inline in the documentation Markdown files. Open the Markdown source to see and edit them.

// ============================================================
// Shared utilities for NPC memory orchestration Edge Functions
// ============================================================

// LocalAI is assumed to be at host.docker.internal:8080 from
// inside the Edge Runtime container.  Override via env var.
const LOCALAI_HOST = Deno.env.get('LOCALAI_HOST') ?? 'host.docker.internal'
const LOCALAI_PORT = Deno.env.get('LOCALAI_PORT') ?? '8080'
const LOCALAI_BASE = `http://${LOCALAI_HOST}:${LOCALAI_PORT}`

const EMBEDDING_MODEL =
  Deno.env.get('LOCALAI_EMBEDDING_MODEL') ?? 'nomic-embed-text-v1.5'
const LLM_MODEL =
  Deno.env.get('LOCALAI_LLM_MODEL') ?? 'qwen2.5-1.5b'

export interface EmbeddingResponse {
  data: Array<{ embedding: number[]; index: number }>
  model: string
  usage: { prompt_tokens: number; total_tokens: number }
}

export interface ChatResponse {
  choices: Array<{
    message: { content: string; role: string }
    finish_reason: string
  }>
  usage: { prompt_tokens: number; completion_tokens: number }
}

/**
 * Generate an embedding vector for the given text using LocalAI.
 * Returns a 768-dimension vector (nomic-embed-text-v1.5).
 */
export async function generateEmbedding(
  text: string,
): Promise<number[] | null> {
  try {
    const res = await fetch(`${LOCALAI_BASE}/v1/embeddings`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        model: EMBEDDING_MODEL,
        input: text.slice(0, 8192), // truncate to model context
      }),
    })

    if (!res.ok) {
      console.error(
        `[embedding] HTTP ${res.status}: ${await res.text()}`,
      )
      return null
    }

    const json: EmbeddingResponse = await res.json()
    return json.data?.[0]?.embedding ?? null
  } catch (err) {
    console.error(`[embedding] fetch error: ${err}`)
    return null
  }
}

/**
 * Simple hash (djb2) used as a content_hash for deduplication.
 */
export function hashContent(text: string): string {
  let hash = 5381
  for (let i = 0; i < text.length; i++) {
    hash = ((hash << 5) + hash + text.charCodeAt(i)) & 0xffffffff
  }
  return hash.toString(16)
}

/**
 * Format an embedding array as a pgvector SQL literal string,
 * e.g. '[0.001,0.002,...]'
 */
export function formatVector(vec: number[]): string {
  return `[${vec.join(',')}]`
}

// ─── Session Summarization ──────────────────────────────

const SUMMARY_SYSTEM_PROMPT = `You are a dialogue session analyst. Given the transcript of a conversation between a player and an NPC in a mystery/detective game, produce a concise 2-3 sentence summary covering:

1. What the player learned (key facts, clues, or revelations)
2. How the NPC's relationship with the player changed (trust, mood)
3. Any items or information exchanged
4. Current narrative status

Focus only on what actually happened in the transcript. Do not invent details.`

/**
 * Call LocalAI to generate a session summary from dialogue turns.
 */
export async function summarizeSession(
  turns: Array<{ role: string; content: string }>,
): Promise<string | null> {
  try {
    const messages = [
      { role: 'system', content: SUMMARY_SYSTEM_PROMPT },
      ...turns.map((t) => ({
        role: t.role === 'assistant' ? 'assistant' : 'user',
        content: t.content,
      })),
    ]

    const res = await fetch(`${LOCALAI_BASE}/v1/chat/completions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        model: LLM_MODEL,
        messages,
        temperature: 0.3,
        max_tokens: 256,
      }),
    })

    if (!res.ok) {
      console.error(
        `[summarize] HTTP ${res.status}: ${await res.text()}`,
      )
      return null
    }

    const json: ChatResponse = await res.json()
    return json.choices?.[0]?.message?.content?.trim() ?? null
  } catch (err) {
    console.error(`[summarize] fetch error: ${err}`)
    return null
  }
}

/**
 * Analyze a dialogue turn and return an inferred trust delta and mood.
 */
export async function analyzeDialogue(
  npcSlug: string,
  playerMessage: string,
  npcResponse: string,
  currentTrust: number,
): Promise<{ trustDelta: number; mood: string } | null> {
  const prompt = `Given an NPC named "${npcSlug}" with current trust level ${currentTrust}/100, analyze this dialogue exchange:

Player: "${playerMessage}"
NPC: "${npcResponse}"

Respond with exactly one JSON object:
{"trustDelta": <integer -20 to 20>, "mood": "<one of: hostile, guarded, neutral, friendly, trusting>"}

trustDelta: positive if the NPC's response shows openness/cooperation, negative if guarded or hostile. Consider context — a guarded response from a naturally suspicious NPC should be neutral, not penalized.
mood: the NPC's inferred emotional state after this exchange.`

  try {
    const res = await fetch(`${LOCALAI_BASE}/v1/chat/completions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        model: LLM_MODEL,
        messages: [
          { role: 'system', content: 'You analyze NPC dialogue and respond with JSON only.' },
          { role: 'user', content: prompt },
        ],
        temperature: 0.2,
        max_tokens: 128,
      }),
    })

    if (!res.ok) {
      console.error(`[analyze] HTTP ${res.status}: ${await res.text()}`)
      return null
    }

    const json: ChatResponse = await res.json()
    const raw = json.choices?.[0]?.message?.content?.trim()
    if (!raw) return null

    // Parse JSON (handle potential markdown fences)
    const cleaned = raw.replace(/```json\s*/gi, '').replace(/```/g, '').trim()
    return JSON.parse(cleaned)
  } catch (err) {
    console.error(`[analyze] parse/fetch error: ${err}`)
    return null
  }
}

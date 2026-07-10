// ============================================================
// Main Edge Function — Supabase Edge Runtime entry point
// ============================================================
// This function routes incoming HTTP calls to sub-handlers
// based on the path prefix. Each route is a self-contained
// game backend operation.
//
// Edge Functions run on Deno. All env vars from docker-compose
// are available via Deno.env.get().
//
// Deploy (in production): supabase functions deploy <name>
// ============================================================

import { createClient } from 'jsr:@supabase/supabase-js@2'
import {
  generateEmbedding,
  hashContent,
  formatVector,
  summarizeSession,
  analyzeDialogue,
} from './lib/localai.ts'

const SUPABASE_URL = Deno.env.get('SUPABASE_URL') ?? 'http://auth:9999'
const SUPABASE_SERVICE_KEY =
  Deno.env.get('SUPABASE_SERVICE_ROLE_KEY') ?? 'dev-local-service-role-key'
const supabase = createClient(SUPABASE_URL, SUPABASE_SERVICE_KEY)

Deno.serve(async (req: Request) => {
  const url = new URL(req.url)
  const path = url.pathname

  try {
    // ── Route dispatch ──────────────────────────────────
    switch (path) {
      case '/health':
        return jsonResponse({ status: 'ok' })

      case '/matchmaking/join':
        return await handleMatchmakingJoin(req)

      case '/npc/dialogue-hook':
        return await handleNpcDialogueHook(req)

      // ── Memory orchestration routes (Phase 4) ─────────
      case '/memory/process-turn':
        return await handleProcessTurn(req)

      case '/memory/summarize-session':
        return await handleSummarizeSession(req)

      case '/memory/update-relationship':
        return await handleUpdateRelationship(req)

      // ── Room broadcast routes (Phase 6) ──────────────
      case '/room/broadcast-dialogue':
        return await handleRoomBroadcast(req)

      default:
        return jsonResponse({ error: 'Not found' }, 404)
    }
  } catch (err) {
    const msg = err instanceof Error ? err.message : 'Internal error'
    return jsonResponse({ error: msg }, 500)
  }
})

// ── Helpers ─────────────────────────────────────────────

function jsonResponse(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

// ── Existing routes ─────────────────────────────────────

async function handleMatchmakingJoin(req: Request): Promise<Response> {
  const body = await req.json()
  const { playerId, gameMode } = body

  if (!playerId || !gameMode) {
    return jsonResponse(
      { error: 'playerId and gameMode required' },
      400,
    )
  }

  const { error } = await supabase.rpc('pgmq_create', {
    queue_name: 'matchmaking',
    message: JSON.stringify({
      playerId,
      gameMode,
      joinedAt: new Date().toISOString(),
    }),
  })

  if (error) throw error

  return jsonResponse({ status: 'queued', playerId, gameMode })
}

async function handleNpcDialogueHook(req: Request): Promise<Response> {
  const body = await req.json()
  const { npcSlug, playerId, dialogueText } = body

  if (!npcSlug || !playerId || !dialogueText) {
    return jsonResponse(
      { error: 'npcSlug, playerId, dialogueText required' },
      400,
    )
  }

  const { error } = await supabase.rpc('pg_net_http_post', {
    url:
      Deno.env.get('GAME_SERVER_WEBHOOK_URL') ??
      'http://game-server:8080/webhook',
    body: JSON.stringify({
      event: 'npc_dialogue',
      npcSlug,
      playerId,
      dialogueText,
    }),
  })

  if (error) throw error

  return jsonResponse({ status: 'forwarded' }, 202)
}

// ═══════════════════════════════════════════════════════
// Phase 4: Memory Orchestration Routes
// ═══════════════════════════════════════════════════════

/**
 * POST /memory/process-turn
 *
 * Generates an embedding for a dialogue turn and stores it in
 * dialogue_turn_vectors.  Called after a turn is inserted.
 *
 * Body:
 * {
 *   "turn_id": 123,
 *   "user_id": "...",
 *   "npc_slug": "butler",
 *   "role": "user",
 *   "content": "Hello there!"
 * }
 */
async function handleProcessTurn(req: Request): Promise<Response> {
  const body = await req.json()
  const { turn_id, user_id, npc_slug, role, content } = body

  if (!turn_id || !user_id || !npc_slug || !role || !content) {
    return jsonResponse(
      {
        error:
          'turn_id, user_id, npc_slug, role, and content are required',
      },
      400,
    )
  }

  // 1. Generate embedding
  const embedding = await generateEmbedding(content)
  if (!embedding) {
    return jsonResponse(
      { error: 'Failed to generate embedding' },
      502,
    )
  }

  // 2. Check for existing vector by content hash (dedup)
  const contentHash = hashContent(content)
  const { data: existing } = await supabase
    .from('dialogue_turn_vectors')
    .select('id')
    .eq('content_hash', contentHash)
    .eq('turn_id', turn_id)
    .maybeSingle()

  if (existing) {
    return jsonResponse({
      status: 'skipped',
      reason: 'duplicate',
      turn_id,
    })
  }

  // 3. Insert vector record
  const { error: insertError } = await supabase.from('dialogue_turn_vectors').insert(
    {
      turn_id,
      user_id,
      npc_slug,
      role,
      content_hash: contentHash,
      embedding: formatVector(embedding),
    },
  )

  if (insertError) throw insertError

  return jsonResponse({
    status: 'embedded',
    turn_id,
    content_hash: contentHash,
    dimensions: embedding.length,
  })
}

/**
 * POST /memory/summarize-session
 *
 * Generates a summary for a completed dialogue session by
 * loading all turns, calling LocalAI for summarization, and
 * storing the result in dialogue_sessions.summary.
 *
 * Body:
 * {
 *   "session_id": "...",
 *   "user_id": "..."
 * }
 */
async function handleSummarizeSession(req: Request): Promise<Response> {
  const body = await req.json()
  const { session_id, user_id } = body

  if (!session_id) {
    return jsonResponse(
      { error: 'session_id is required' },
      400,
    )
  }

  // 1. Load all turns for this session
  const { data: turns, error: turnsError } = await supabase
    .from('dialogue_turns')
    .select('role, content')
    .eq('session_id', session_id)
    .order('created_at', { ascending: true })

  if (turnsError) throw turnsError
  if (!turns || turns.length === 0) {
    return jsonResponse(
      { error: 'No turns found for session', session_id },
      404,
    )
  }

  // 2. Generate summary via LocalAI
  const summary = await summarizeSession(turns)
  if (!summary) {
    return jsonResponse(
      { error: 'Failed to generate summary' },
      502,
    )
  }

  // 3. Store summary in dialogue_sessions
  const { error: updateError } = await supabase
    .from('dialogue_sessions')
    .update({ summary, ended_at: new Date().toISOString() })
    .eq('session_id', session_id)

  if (updateError) throw updateError

  return jsonResponse({
    status: 'summarized',
    session_id,
    summary_length: summary.length,
  })
}

/**
 * POST /memory/update-relationship
 *
 * Analyzes a completed dialogue exchange and adjusts the
 * player_npc_relationships trust/mood scores accordingly.
 *
 * Body:
 * {
 *   "user_id": "...",
 *   "npc_slug": "butler",
 *   "player_message": "Hello!",
 *   "npc_response": "Good day, friend.",
 *   "current_trust": 50
 * }
 */
async function handleUpdateRelationship(req: Request): Promise<Response> {
  const body = await req.json()
  const {
    user_id,
    npc_slug,
    player_message,
    npc_response,
    current_trust,
  } = body

  if (!user_id || !npc_slug || !player_message || !npc_response) {
    return jsonResponse(
      {
        error:
          'user_id, npc_slug, player_message, and npc_response are required',
      },
      400,
    )
  }

  // 1. Analyze the exchange via LocalAI
  const analysis = await analyzeDialogue(
    npc_slug,
    player_message,
    npc_response,
    current_trust ?? 50,
  )

  if (!analysis) {
    return jsonResponse(
      { error: 'Failed to analyze dialogue' },
      502,
    )
  }

  // 2. Upsert relationship record via secure RPC
  //    (the RPC also increments dialogue_count atomically)
  const { error: rpcError } = await supabase.rpc(
    'upsert_npc_relationship',
    {
      p_user_id: user_id,
      p_npc_slug: npc_slug,
      p_trust_score: Math.max(
        0,
        Math.min(100, (current_trust ?? 50) + analysis.trustDelta),
      ),
      p_current_mood: analysis.mood,
      p_last_interaction_at: new Date().toISOString(),
    },
  )

  if (rpcError) throw rpcError

  return jsonResponse({
    status: 'updated',
    npc_slug,
    trust_delta: analysis.trustDelta,
    new_mood: analysis.mood,
  })
}

// ═══════════════════════════════════════════════════════
// Phase 6: Room Broadcast Routes
// ═══════════════════════════════════════════════════════

/**
 * POST /room/broadcast-dialogue
 *
 * Broadcasts an NPC dialogue response to all members of a room
 * via the pgmq session_events queue.  Room members poll this
 * queue on their Realtime subscriptions.
 *
 * Body:
 * {
 *   "room_id": "...",
 *   "npc_slug": "butler",
 *   "dialogue_message": "Greetings, friend.",
 *   "player_name": "Andre",
 *   "session_id": "..."
 * }
 */
async function handleRoomBroadcast(req: Request): Promise<Response> {
  const body = await req.json()
  const {
    room_id,
    npc_slug,
    dialogue_message,
    player_name,
    session_id,
  } = body

  if (!room_id || !npc_slug || !dialogue_message) {
    return jsonResponse(
      {
        error:
          'room_id, npc_slug, and dialogue_message are required',
      },
      400,
    )
  }

  // Enqueue broadcast event in pgmq
  const { error } = await supabase.rpc('pgmq_send', {
    queue_name: 'session_events',
    message: JSON.stringify({
      event_type: 'room_dialogue_broadcast',
      room_id,
      npc_slug,
      dialogue_message,
      player_name: player_name ?? '',
      session_id: session_id ?? null,
      timestamp: new Date().toISOString(),
    }),
  })

  if (error) throw error

  return jsonResponse({
    status: 'broadcast_enqueued',
    room_id,
    npc_slug,
  })
}

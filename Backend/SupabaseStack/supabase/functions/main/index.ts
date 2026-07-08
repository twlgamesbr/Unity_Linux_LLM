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
        return new Response(JSON.stringify({ status: 'ok' }), {
          headers: { 'Content-Type': 'application/json' },
        })

      case '/matchmaking/join':
        return await handleMatchmakingJoin(req)

      case '/npc/dialogue-hook':
        return await handleNpcDialogueHook(req)

      default:
        return new Response(JSON.stringify({ error: 'Not found' }), {
          status: 404,
          headers: { 'Content-Type': 'application/json' },
        })
    }
  } catch (err) {
    const msg = err instanceof Error ? err.message : 'Internal error'
    return new Response(JSON.stringify({ error: msg }), {
      status: 500,
      headers: { 'Content-Type': 'application/json' },
    })
  }
})

// ── Route handlers ─────────────────────────────────────

async function handleMatchmakingJoin(req: Request): Promise<Response> {
  const body = await req.json()
  const { playerId, gameMode } = body

  if (!playerId || !gameMode) {
    return new Response(
      JSON.stringify({ error: 'playerId and gameMode required' }),
      { status: 400, headers: { 'Content-Type': 'application/json' } },
    )
  }

  // Enqueue matchmaking request via pgmq
  const { error } = await supabase.rpc('pgmq_create', {
    queue_name: 'matchmaking',
    message: JSON.stringify({ playerId, gameMode, joinedAt: new Date().toISOString() }),
  })

  if (error) throw error

  return new Response(
    JSON.stringify({ status: 'queued', playerId, gameMode }),
    { status: 200, headers: { 'Content-Type': 'application/json' } },
  )
}

async function handleNpcDialogueHook(req: Request): Promise<Response> {
  const body = await req.json()
  const { npcSlug, playerId, dialogueText } = body

  if (!npcSlug || !playerId || !dialogueText) {
    return new Response(
      JSON.stringify({ error: 'npcSlug, playerId, dialogueText required' }),
      { status: 400, headers: { 'Content-Type': 'application/json' } },
    )
  }

  // Fire async webhook to game server
  const { error } = await supabase.rpc('pg_net_http_post', {
    url: Deno.env.get('GAME_SERVER_WEBHOOK_URL') ?? 'http://game-server:8080/webhook',
    body: JSON.stringify({
      event: 'npc_dialogue',
      npcSlug,
      playerId,
      dialogueText,
    }),
  })

  if (error) throw error

  return new Response(JSON.stringify({ status: 'forwarded' }), {
    status: 202,
    headers: { 'Content-Type': 'application/json' },
  })
}

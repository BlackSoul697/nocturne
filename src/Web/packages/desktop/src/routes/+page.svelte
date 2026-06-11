<script lang="ts">
  import { invoke } from "@tauri-apps/api/core";
  import { listen } from "@tauri-apps/api/event";
  import { onMount } from "svelte";
  import {
    CheckCircle2,
    KeyRound,
    Link2,
    Loader2,
    Monitor,
    RotateCcw,
  } from "lucide-svelte";

  type CommandError = { status?: number | null; message: string };
  type LinkInfo = { serverUrl: string };
  type CompleteResponse = {
    success: boolean;
    username?: string | null;
    country?: string | null;
  };

  type Phase = "link" | "ready" | "signing-in" | "completing" | "done";

  let phase = $state<Phase>("link");
  let linkCodeInput = $state("");
  let serverUrl = $state<string | null>(null);
  let region = $state<"EU" | "US">("EU");
  let busy = $state(false);
  let error = $state<string | null>(null);
  let connectedUsername = $state<string | null>(null);

  function describeError(e: unknown): string {
    const err = e as CommandError;
    if (err?.status === 401) {
      return "The link code has expired. Generate a fresh one in Nocturne and link again.";
    }
    return err?.message ?? "Something went wrong.";
  }

  /** A 401 means the short-lived link token is dead — go back to the link step. */
  function handleApiError(e: unknown) {
    error = describeError(e);
    if ((e as CommandError)?.status === 401) {
      phase = "link";
      serverUrl = null;
    }
  }

  async function linkServer() {
    busy = true;
    error = null;
    try {
      const info = await invoke<LinkInfo>("link", { linkCode: linkCodeInput });
      serverUrl = info.serverUrl;
      phase = "ready";
      linkCodeInput = "";
    } catch (e) {
      error = describeError(e);
    } finally {
      busy = false;
    }
  }

  async function startConnect() {
    busy = true;
    error = null;
    try {
      await invoke("start_connect", { region });
      phase = "signing-in";
    } catch (e) {
      handleApiError(e);
    } finally {
      busy = false;
    }
  }

  async function completeConnect(code: string) {
    phase = "completing";
    error = null;
    try {
      const res = await invoke<CompleteResponse>("complete_connect", { code });
      if (!res.success) {
        error = "Sign-in could not be completed. Please try again.";
        phase = "ready";
        return;
      }
      connectedUsername = res.username ?? null;
      phase = "done";
    } catch (e) {
      const expired = (e as CommandError)?.status === 401;
      handleApiError(e);
      if (!expired) phase = "ready";
    }
  }

  async function cancelSignIn() {
    await invoke("cancel_login");
    phase = "ready";
  }

  function startOver() {
    phase = "link";
    serverUrl = null;
    error = null;
    connectedUsername = null;
  }

  onMount(() => {
    const unlistenCode = listen<string>("carelink-code", (event) => {
      completeConnect(event.payload);
    });
    const unlistenClosed = listen("carelink-login-closed", () => {
      // Fires after a capture too (we close the window) — only react while still waiting.
      if (phase === "signing-in") {
        phase = "ready";
        error = "The sign-in window was closed before finishing.";
      }
    });
    return () => {
      unlistenCode.then((fn) => fn());
      unlistenClosed.then((fn) => fn());
    };
  });
</script>

<main class="mx-auto flex min-h-screen max-w-md flex-col gap-6 p-6">
  <header class="flex items-center gap-3">
    <Monitor class="h-6 w-6 text-sky-400" />
    <div>
      <h1 class="text-lg font-semibold">Nocturne Companion</h1>
      <p class="text-sm text-zinc-400">Connect CareLink to your Nocturne site</p>
    </div>
  </header>

  {#if error}
    <p class="rounded-md border border-red-900 bg-red-950 px-3 py-2 text-sm text-red-300">
      {error}
    </p>
  {/if}

  {#if phase === "link"}
    <section class="space-y-3">
      <h2 class="flex items-center gap-2 text-sm font-medium">
        <Link2 class="h-4 w-4" /> Link your Nocturne site
      </h2>
      <p class="text-sm text-zinc-400">
        In Nocturne, open <span class="text-zinc-200">Connectors → CareLink</span> and choose
        <span class="text-zinc-200">Generate link code</span>, then paste it here. The code is
        valid for 10 minutes.
      </p>
      <textarea
        bind:value={linkCodeInput}
        rows={3}
        placeholder="nocturne-connect://link?server=…&token=…"
        class="w-full rounded-md border border-zinc-800 bg-zinc-900 px-3 py-2 font-mono text-xs text-zinc-100 placeholder:text-zinc-600 focus:border-sky-600 focus:outline-none"
        disabled={busy}
      ></textarea>
      <button
        onclick={linkServer}
        disabled={busy || !linkCodeInput.trim()}
        class="rounded-md bg-sky-600 px-4 py-2 text-sm font-medium text-white hover:bg-sky-500 disabled:opacity-50"
      >
        {busy ? "Linking…" : "Link"}
      </button>
    </section>
  {/if}

  {#if phase === "ready"}
    <section class="space-y-4">
      <p class="text-sm text-zinc-400">
        Linked to <span class="font-mono text-zinc-200">{serverUrl}</span>
      </p>
      <div class="space-y-2">
        <h2 class="flex items-center gap-2 text-sm font-medium">
          <KeyRound class="h-4 w-4" /> Connect your CareLink account
        </h2>
        <p class="text-sm text-zinc-400">
          A CareLink sign-in window will open. Sign in and solve the captcha there — this app
          captures the result automatically. Your password never leaves Medtronic's page.
        </p>
        <div class="flex gap-2">
          <button
            onclick={() => (region = "EU")}
            disabled={busy}
            class="rounded-md border px-3 py-1.5 text-sm {region === 'EU'
              ? 'border-sky-600 bg-sky-600 text-white'
              : 'border-zinc-700 text-zinc-300 hover:border-zinc-500'}"
          >
            EU / Outside-US
          </button>
          <button
            onclick={() => (region = "US")}
            disabled={busy}
            class="rounded-md border px-3 py-1.5 text-sm {region === 'US'
              ? 'border-sky-600 bg-sky-600 text-white'
              : 'border-zinc-700 text-zinc-300 hover:border-zinc-500'}"
          >
            US
          </button>
        </div>
        <p class="text-xs text-zinc-500">
          Australia, NZ, Europe and most of the world use EU. Choose US only for a US CareLink
          account.
        </p>
      </div>
      <div class="flex items-center gap-3">
        <button
          onclick={startConnect}
          disabled={busy}
          class="rounded-md bg-sky-600 px-4 py-2 text-sm font-medium text-white hover:bg-sky-500 disabled:opacity-50"
        >
          {busy ? "Starting…" : "Connect CareLink"}
        </button>
        <button onclick={startOver} class="text-sm text-zinc-400 hover:text-zinc-200">
          Use a different site
        </button>
      </div>
    </section>
  {/if}

  {#if phase === "signing-in" || phase === "completing"}
    <section class="flex flex-col items-center gap-3 py-8 text-center">
      <Loader2 class="h-6 w-6 animate-spin text-sky-400" />
      {#if phase === "signing-in"}
        <p class="text-sm text-zinc-300">Waiting for you to sign in to CareLink…</p>
        <p class="text-xs text-zinc-500">
          Finish signing in (and the captcha) in the CareLink window. The code is captured
          automatically when Medtronic redirects.
        </p>
        <button onclick={cancelSignIn} class="text-sm text-zinc-400 hover:text-zinc-200">
          Cancel
        </button>
      {:else}
        <p class="text-sm text-zinc-300">Code captured — finishing the connection…</p>
      {/if}
    </section>
  {/if}

  {#if phase === "done"}
    <section class="space-y-4">
      <div class="flex items-start gap-2">
        <CheckCircle2 class="h-5 w-5 shrink-0 text-green-500" />
        <div class="text-sm">
          <p class="font-medium">CareLink connected.</p>
          <p class="text-zinc-400">
            {connectedUsername ? `Signed in as ${connectedUsername}. ` : ""}Nocturne stored the
            refresh token and will sync automatically. You can close this app.
          </p>
        </div>
      </div>
      <button
        onclick={startOver}
        class="flex items-center gap-2 rounded-md border border-zinc-700 px-3 py-1.5 text-sm text-zinc-300 hover:border-zinc-500"
      >
        <RotateCcw class="h-3.5 w-3.5" /> Connect another account
      </button>
    </section>
  {/if}
</main>

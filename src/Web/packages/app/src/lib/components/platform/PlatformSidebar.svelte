<script lang="ts">
  import { page } from "$app/state";
  import { Activity, Bell, BookOpen, TrendingUp, Users } from "lucide-svelte";
  import type { AuthUser } from "$lib/stores/auth-store.svelte";

  interface Props {
    user: AuthUser;
    apexHost: string;
    attentionCount: number;
    tenantCount: number;
  }

  let { user, apexHost, attentionCount, tenantCount }: Props = $props();

  const nav = $derived([
    { href: "/roster",     label: "Roster",      icon: Users,      count: tenantCount },
    { href: "/attention",  label: "Attention",    icon: Bell,       count: attentionCount },
    { href: "/trends",     label: "Trends",       icon: TrendingUp, count: undefined },
    { href: "/activity",   label: "Activity",     icon: Activity,   count: undefined },
    { href: "/care-plans", label: "Care plans",   icon: BookOpen,   count: undefined },
  ]);
</script>

<aside class="flex h-full w-56 flex-col border-r border-border bg-sidebar px-3 py-4 shrink-0">
  <!-- Brand -->
  <div class="mb-6 flex items-center gap-2 px-2">
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4"
      stroke-linecap="round" stroke-linejoin="round" class="text-foreground">
      <path d="M22 12h-4l-3 9L9 3l-3 9H2" />
    </svg>
    <span class="font-semibold tracking-tight">Nocturne</span>
  </div>

  <!-- Identity -->
  <div class="mb-4 px-2">
    <div class="text-xs text-muted-foreground">Signed in as</div>
    <div class="truncate font-medium text-sm">{user.name}</div>
    <div class="truncate text-xs text-muted-foreground font-mono">{apexHost}</div>
  </div>

  <!-- Nav -->
  <nav class="flex flex-col gap-0.5 flex-1">
    {#each nav as item}
      {@const active = $page.url.pathname.startsWith(item.href)}
      <a
        href={item.href}
        class="flex items-center gap-2.5 rounded-md px-2 py-1.5 text-sm transition-colors
               {active ? 'bg-accent text-accent-foreground font-medium' : 'text-muted-foreground hover:bg-accent/50 hover:text-foreground'}"
      >
        <item.icon class="size-4 shrink-0" />
        <span class="flex-1">{item.label}</span>
        {#if item.count != null}
          <span class="rounded-full bg-muted px-1.5 py-0.5 text-xs font-medium tabular-nums">
            {item.count}
          </span>
        {/if}
      </a>
    {/each}
  </nav>

  <!-- Footer -->
  <div class="mt-4 flex items-center gap-2 px-2 pt-4 border-t border-border">
    <div class="size-7 rounded-full bg-muted flex items-center justify-center text-xs font-semibold shrink-0">
      {user.name.split(" ").map((p: string) => p[0]).slice(0, 2).join("").toUpperCase()}
    </div>
    <div class="flex-1 min-w-0">
      <div class="truncate text-sm font-medium">{user.name}</div>
    </div>
  </div>
</aside>

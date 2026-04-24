<script lang="ts">
  import { Checkbox } from "$lib/components/ui/checkbox";

  interface PermissionDef {
    key: string;
    label: string;
    impliedBy?: string; // e.g. "entries.readwrite" implies "entries.read"
  }

  interface PermissionCategory {
    name: string;
    permissions: PermissionDef[];
  }

  const categories: PermissionCategory[] = [
    {
      name: "Blood Glucose",
      permissions: [
        { key: "entries.read", label: "Read", impliedBy: "entries.readwrite" },
        { key: "entries.readwrite", label: "Read & Write" },
      ],
    },
    {
      name: "Treatments",
      permissions: [
        {
          key: "treatments.read",
          label: "Read",
          impliedBy: "treatments.readwrite",
        },
        { key: "treatments.readwrite", label: "Read & Write" },
      ],
    },
    {
      name: "Devices",
      permissions: [
        {
          key: "devicestatus.read",
          label: "Read",
          impliedBy: "devicestatus.readwrite",
        },
        { key: "devicestatus.readwrite", label: "Read & Write" },
      ],
    },
    {
      name: "Profile",
      permissions: [
        {
          key: "profile.read",
          label: "Read",
          impliedBy: "profile.readwrite",
        },
        { key: "profile.readwrite", label: "Read & Write" },
      ],
    },
    {
      name: "Notifications",
      permissions: [
        {
          key: "notifications.read",
          label: "Read",
          impliedBy: "notifications.readwrite",
        },
        { key: "notifications.readwrite", label: "Read & Write" },
      ],
    },
    {
      name: "Reports",
      permissions: [{ key: "reports.read", label: "Read" }],
    },
    {
      name: "Health",
      permissions: [{ key: "health.read", label: "Read" }],
    },
    {
      name: "Identity",
      permissions: [{ key: "identity.read", label: "Read" }],
    },
    {
      name: "Administration",
      permissions: [
        { key: "roles.manage", label: "Manage Roles" },
        { key: "members.invite", label: "Invite Members" },
        { key: "members.manage", label: "Manage Members" },
        { key: "tenant.settings", label: "Tenant Settings" },
        { key: "sharing.manage", label: "Manage Sharing" },
      ],
    },
    {
      name: "Audit",
      permissions: [
        { key: "audit.read", label: "View Audit Logs", impliedBy: "audit.manage" },
        { key: "audit.manage", label: "Manage Audit Settings" },
      ],
    },
  ];

  let { selected = $bindable<string[]>([]) }: { selected: string[] } =
    $props();

  /** Whether a permission is currently in the selected list (either explicitly or implied). */
  function isSelected(key: string): boolean {
    return selected.includes(key);
  }

  /** Whether a read permission is implied by its readwrite counterpart being selected. */
  function isImplied(perm: PermissionDef): boolean {
    return !!perm.impliedBy && selected.includes(perm.impliedBy);
  }

  function toggle(perm: PermissionDef, checked: boolean | "indeterminate") {
    if (checked === true) {
      if (!selected.includes(perm.key)) {
        // If selecting a readwrite permission, also add its implied read permission
        const toAdd = [perm.key];
        const impliedRead = categories
          .flatMap((c) => c.permissions)
          .find((p) => p.impliedBy === perm.key);
        if (impliedRead && !selected.includes(impliedRead.key)) {
          toAdd.push(impliedRead.key);
        }
        selected = [...selected, ...toAdd];
      }
    } else {
      // If unchecking a readwrite, keep the read; if unchecking a read, also uncheck the readwrite
      if (perm.impliedBy) {
        // This is a read permission — also remove its readwrite counterpart
        selected = selected.filter(
          (s) => s !== perm.key && s !== perm.impliedBy,
        );
      } else {
        selected = selected.filter((s) => s !== perm.key);
      }
    }
  }
</script>

<div class="space-y-4">
  {#each categories as category}
    <div class="space-y-2">
      <p class="text-sm font-medium">{category.name}</p>
      <div class="grid gap-2 sm:grid-cols-2">
        {#each category.permissions as perm}
          {@const implied = isImplied(perm)}
          <div class="flex items-center gap-2">
            <Checkbox
              id="perm-{perm.key}"
              checked={isSelected(perm.key) || implied}
              disabled={implied}
              onCheckedChange={(checked) => toggle(perm, checked)}
            />
            <label
              for="perm-{perm.key}"
              class="text-sm text-foreground select-none"
              class:cursor-pointer={!implied}
              class:opacity-60={implied}
            >
              {perm.label}
            </label>
          </div>
        {/each}
      </div>
    </div>
  {/each}
</div>

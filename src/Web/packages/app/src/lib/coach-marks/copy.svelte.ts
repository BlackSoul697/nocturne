import type { CoachLabels, CoachMarkStep } from "@nocturne/coach";

/** Built per call so wuchale reads them in the current locale. */
export function coachCopy() {
  return {
    "quick-tour.current-bg": {
      title: "Your glucose, live",
      description:
        "This updates in real-time as new readings arrive from your CGM.",
    },
    "quick-tour.widgets": {
      title: "Customizable widgets",
      description:
        "Reorder or swap these in Settings \u2192 Appearance to show the stats you care about.",
    },
    "quick-tour.chart": {
      title: "Interactive chart",
      description:
        "Drag to pan, pinch or scroll to zoom. Tap any point to see the exact reading and time.",
    },
    "feature-intro.calendar-views": {
      title: "View modes",
      description:
        "Switch between Time in Range and Profile views to see different patterns.",
    },
    "feature-intro.calendar-trackers": {
      title: "Tracker events",
      description:
        "Tracker events appear on your calendar \u2014 colored by urgency.",
    },
    "feature-intro.meals-matching": {
      title: "Smart matching",
      description:
        "Nocturne suggests food matches based on your history \u2014 review them here.",
    },
    "feature-intro.appearance-widgets": {
      title: "Widget order",
      description: "Drag to reorder your dashboard widgets.",
    },
    "setup-reports.categories": {
      title: "Start with Executive Summary",
      description:
        "It combines your key metrics into a single page \u2014 great for clinic visits or sharing with your endo.",
    },
    "setup-connectors.sources": {
      title: "Waiting for data",
      description:
        "Once you set up an uploader app or cloud connector below, your data source will appear here automatically.",
    },
    "setup-connectors.server-connectors": {
      title: "Cloud connectors",
      description:
        "Pull data directly from Dexcom, LibreLink, or Glooko \u2014 no uploader app needed.",
    },
    "onboarding.sharing": {
      title: "Share with a caretaker",
      description:
        "Share your glucose data with a parent, partner, or clinician.",
    },
    "setup-invite.create-link": {
      title: "Start here",
      description:
        "Create a shareable link to invite a caretaker, partner, or clinician.",
    },
    "setup-invite.roles": {
      title: "Choose their access",
      description:
        "Roles control what they can see and do. Viewer for a quick glance, Caretaker if they help manage your diabetes.",
    },
    "onboarding.patient-details": {
      title: "Why this matters",
      description:
        "Your diabetes type determines how Nocturne calculates bolus suggestions and categorizes treatments. Select your type and save.",
    },
    "onboarding.devices": {
      title: "Add your current device",
      description:
        "Add the CGM, pump, or meter you use right now and mark it as current. Historical devices can be added later.",
    },
    "onboarding.insulins": {
      title: "Add your current insulin",
      description:
        "Add at least one insulin and mark it as current. The brand and type help Nocturne estimate active insulin curves.",
    },
    "onboarding.therapy-profile": {
      title: "Automatically synced",
      description:
        "Your basal rates, carb ratios, and sensitivity factors are imported from your uploader (AAPS, Loop, xDrip+). No manual entry needed.",
    },
  } satisfies Record<string, CoachMarkStep>;
}

export type CoachCopyKey = keyof ReturnType<typeof coachCopy>;

export function coachFor(
  key: CoachCopyKey
): CoachMarkStep & { key: CoachCopyKey } {
  return { key, ...coachCopy()[key] };
}

export function inviteLinkCopy(expiryLabel: string): CoachMarkStep {
  return {
    title: "Send the link",
    description: `The link expires in ${expiryLabel}. They'll need to sign in or create an account to accept.`,
  };
}

/**
 * The popover's control text, built per call so wuchale reads it in the current
 * locale.
 */
export function coachLabels(): CoachLabels {
  return {
    dismiss: "Dismiss",
    showTip: "Show tip",
    back: "Back",
    next: "Next",
    gotIt: "Got it",
    stepCounter: (current, total) => `${current} of ${total}`,
  };
}

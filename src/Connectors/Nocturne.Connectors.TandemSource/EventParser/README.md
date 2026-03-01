# Event definitions (tconnectsync parity)

`EventDefinitions.json` is the single source of truth for Tandem pump event IDs and payload layouts. It is kept **identical in content** to the merged event definitions used by [tconnectsync](https://github.com/jwoglom/tconnectsync):

- **Base:** `tconnectsync/eventparser/events.json` (main event set)
- **Overrides/additions:** `tconnectsync/eventparser/custom_events.json` (merged second so it overwrites by event ID)

Merge order matches tconnectsync’s `build_events.py`: load `events.json` then apply `custom_events.json` so custom event IDs (e.g. 36, 37, 48, 81) replace or extend the base set.

When updating:

1. Pull latest from https://github.com/jwoglom/tconnectsync
2. Copy `tconnectsync/eventparser/events.json` and merge in `tconnectsync/eventparser/custom_events.json` (same structure: `{"events": { "id": { "name": "...", "data": { ... } }, ... } }`). Custom keys overwrite base.
3. Save the merged JSON as `EventDefinitions.json` in this folder.
4. Rebuild; the file is embedded as an assembly resource.

Field semantics (types, offsets, `uom`) must match tconnectsync so that binary payload parsing stays compatible. The Python project generates dataclasses from this JSON; we load it at runtime and use the same layout for `ParsedEvent` field access.

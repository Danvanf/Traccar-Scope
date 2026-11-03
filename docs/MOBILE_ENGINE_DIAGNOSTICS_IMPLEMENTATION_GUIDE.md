# Mobile Check-Engine Indicator and Trouble Codes

Junior developer implementation guide · 2026-10-25

**Status: planned; this document does not implement the feature or verify the Enclave's received telemetry.**

## 1. Intended result

On the mobile map, show an engine-shaped warning button near the top left when the selected vehicle has a reported check-engine warning. Tapping it opens a window with the vehicle name, lamp status, available diagnostic trouble codes (DTCs), and observation times. The engine button and existing Notifications button must remain independently visible and usable.

Display received codes and their broad system categories only. Do not diagnose a fault, recommend a repair, or clear codes.

The Enclave is the first real-data test candidate. The owner reports existing emissions-related faults involving two oxygen sensors. The owner started using this tracker around **September 1, 2026**; earlier vehicle history was imported from Bouncie. The owner may have disabled transmission of the DTC count to reduce data usage and is working on restoring it. These are investigation leads, not proof of the current lamp state, specific codes, or tracker settings. Do not invent the Enclave's actual codes.

## 2. Essential distinction: lamp, count, and codes

Treat these as three independently observed fields:

| Field | Meaning | Does it establish that the lamp is currently on? |
| --- | --- | --- |
| Explicit MIL/check-engine state | A documented vehicle-reported lamp state | Yes, for its observation time; currentness still depends on age |
| DTC count | A reported number of trouble codes | No |
| DTC code list | The codes the tracker supplied | No; the source might not identify stored, pending, or permanent status |

MIL means **Malfunction Indicator Lamp**, commonly called the check-engine light. The current repository mappings do not establish a verified explicit MIL-state field for the Enclave. This is a required discovery item, not a reason to label a positive count as “Check engine light on.”

If only a positive count or nonempty code list is available, show the engine-shaped diagnostics button with the label **“Trouble codes reported”**. Its window must say **“Check-engine light: status unavailable.”** This delivers access to received diagnostics while preserving the distinction. Use **“Check-engine light on”** only with a verified explicit lamp observation.

Do not derive lamp state from distance/time accumulated while MIL was on, historical maxima, ignition state, or the owner's description. Do not extract a lamp bit from `io30`; the documented Teltonika field is a count, not an established raw OBD monitor-status bitfield. [Teltonika parameter reference](https://wiki.teltonika-gps.com/view/FMM003_Teltonika_Data_Sending_Parameters_ID#OBD_elements)

## 3. Existing code to read first

Paths below are relative to the application repository root. Read `AGENTS.md` before changing files.

| File / symbol | Why it matters |
| --- | --- |
| `src/App.jsx`: `fetchLiveSnapshot` | Retrieves raw Traccar `/positions` responses, then merges them into map history |
| `src/App.jsx`: `deviceListSelectedId`, `profileContextWithSourceMappings` | Selected mobile device and effective vehicle/profile/source-mapping resolution |
| `src/App.jsx`: `activeTaskPresentation` | Existing task-window routing, including Notifications |
| `src/components/layout/AppLayout.jsx` | Mobile map, Notifications button, task-window integration, location-picking behavior |
| `src/components/layout/layout.css`: `.scope-map-notifications` | Existing Notifications button is absolutely positioned at top/left 12px; adding another at the same coordinates would overlap |
| `src/components/layout/TaskDialog.jsx` | Reusable modal dialog with keyboard and focus handling |
| `src/components/reports/ReportsWorkspace.jsx`: `liveVehicleHealthRows` | Computes the maximum mapped DTC count over the report's date range, not current lamp state |
| `src/lib/carProfiles.js`, `profileHistory.js`, `telemetrySourceMappings.js` | Existing translation and effective-dated mapping infrastructure |
| `src/lib/geo.js`: `toHistoryPosition` | Normalizes map points; currently drops raw `protocol` and supplies a fallback timestamp when dates are missing |
| `device-profiles/teltonika-fmm003.json`, `teltonika-fmm00a.json` | Existing raw/decoded count and text-code mappings |
| `backend/TraccarScope.Api/TraccarProxyEndpoints.cs` | Existing read-only Traccar proxy and device-access enforcement |

Inspect actual runtime profile assignments rather than assuming the Enclave uses a particular bundled profile. In the inspected bundled profiles, `io30` and `faultCount` have quantity `count`, while the report calls `mappedMetric(..., 'diagnostic_code_count')`. That helper also chooses the first matching mapping before checking whether its key is present in a sample. Verify the runtime mapping before interpreting a blank report cell. Do not copy that lookup behavior into the new diagnostics helper or change shared quantity conventions as a side effect of this feature.

## 4. Data discovery checklist — complete before enabling a confirmed MIL label

1. Identify the Enclave's vehicle UUID, Traccar device ID, configured tracker model, firmware if available, installed Traccar version, and effective device/profile assignment dates. Record identifiers privately; do not commit credentials, VINs, or complete live position exports.
2. Inspect a recent raw authorized `/positions` response, before `toHistoryPosition` normalization. Record only relevant field names, types, diagnostic values, source protocol, position ID, and valid timestamps in an approved local investigation note.
3. Check the candidate attributes below. A normalized name may replace a raw name after a Traccar model/configuration change. Search both exact keys where applicable; do not expect both in each sample.

| Purpose | Candidate source fields | Evidence / handling |
| --- | --- | --- |
| Count | `attributes.io30`, `attributes.faultCount` | Bundled mappings associate these with AVL 30 |
| Codes | `attributes.io281`, `attributes.dtcs` | Bundled mappings associate these with AVL 281; text, not a numeric graph series |
| Lamp | No confirmed key in this investigation | Require a documented field and a captured sample for the actual tracker/decoder combination |
| Historical context only | `io43` / `milDistance`; `io54` | Existing mappings describe accumulated distance/time with MIL on; not a current-state flag |

Teltonika documents AVL 281 as variable-length ASCII code text. The referenced Traccar decoder maps AVL 30 to `faultCount` for matching models and AVL 281 to `dtcs`, converting commas to spaces. Decoder behavior depends on the installed version and configured model. [Teltonika reference](https://wiki.teltonika-gps.com/view/FMM003_Teltonika_Data_Sending_Parameters_ID#OBD_elements), [Traccar v6.14.5 decoder](https://github.com/traccar/traccar/blob/v6.14.5/src/main/java/org/traccar/protocol/TeltonikaProtocolDecoder.java)

4. Inspect history from approximately **2026-09-01 through the investigation date**, using authorized requests in one-day windows. Narrow to the actual tracker assignment period once verified. Record the first and last observations of each relevant key and any gaps. Stop cleanly when retention or permissions prevent access; “not found in retained data” does not mean “never transmitted.”
5. Keep Bouncie/imported records separate. Confirm source provenance and effective assignments; a calendar cutoff alone does not prove origin. Do not use imported history to establish the current tracker's lamp or code state.
6. Ask the owner to confirm the saved tracker configuration and, after restoring diagnostics transmission, capture a new ignition-on/drive sample. Compare count and code-list delivery independently. Do not assume disabling AVL 30 also disabled AVL 281. Do not prescribe unverified configurator parameter numbers or send tracker commands from the app.
7. For a proposed explicit MIL field, record its exact key, type, on/off values, source documentation, model/firmware/decoder scope, and fresh examples compared with the dashboard lamp. Until confirmed, keep lamp status unavailable and use the diagnostics-only wording above.

Historical inspection must be read-only. The implementation can proceed with synthetic fixtures while fresh tracker output is unavailable. Real lamp confirmation remains a separate acceptance item.

## 5. Small implementation scope and file plan

Use the existing frontend polling and authorized Traccar proxy for the first version. No database migration, new tracker command, or diagnostic-code database is required.

| File | Work |
| --- | --- |
| **New:** `src/lib/vehicleDiagnostics.js` | Pure extraction, code normalization, observation reduction, freshness, and button-state helpers |
| **New:** `src/components/VehicleDiagnosticsPanel.jsx` | Read-only lamp/count/code/timestamp display |
| `src/App.jsx` | Feed raw live observations to diagnostics state; fetch bounded initialization/history; resolve selected vehicle; expose a diagnostics task |
| `src/components/layout/AppLayout.jsx` | Shared top-left action stack and engine button |
| `src/components/layout/layout.css` | Stack spacing, icon states, small-screen dialog content |
| **New:** `tests/vehicle-diagnostics.test.js` | Pure state/data tests |
| `tests/fixtures/mobile-layout.jsx` | Add synthetic diagnostics/Notifications coexistence cases where practical |

Do not repurpose `historyByDevice` as the source of current diagnostics. Loading an old trip, changing the displayed range, or trimming map history must not change the selected vehicle's last live diagnostic observations.

### Step A — define the observation model

Keep fields separately timestamped; sparse telemetry may send codes and count in different packets. Suggested shape:

```js
{
  vehicleId,
  deviceId,
  bindingId,
  lamp: { value: 'on', observedAt, receivedAt, positionId, key, provenance },
  count: { value: 2, observedAt, receivedAt, positionId, key, provenance },
  codes: { value: ['P0123', 'U0100'], observedAt, receivedAt, positionId, key, provenance },
  // Each observation is null until actually received and validated.
  lookupStatus: 'ready', // loading | ready | unavailable | error
}
```

These code values are **synthetic test examples**, not the Enclave's known faults. `provenance` should retain protocol, device ID, mapping identity/revision, and live-versus-history origin. Raw device data should remain available for debugging without being inserted into HTML.

Use an explicit valid device/fix timestamp as the observation time according to the verified source semantics; retain server receipt time separately. Reject malformed or implausibly future observation times. Never use the time a sample was fetched, `Date.now()`, or the map normalizer's fallback to make old/malformed data appear fresh.

Key state by vehicle plus effective device binding. On tracker replacement or reassignment, do not carry the former tracker's values into the new current binding. Clear account-scoped state and cancel requests on logout or loss of authorization.

### Step B — extract and normalize exact supported fields

1. Resolve the effective profile and source mappings for the sample's device and timestamp. Use exact validated keys and representations. Do not scan for arbitrary fields whose label happens to include “fault.”
2. For verified Teltonika count/code aliases, inspect keys actually present using `Object.hasOwn(attributes, key)`. Preserve zero as a valid count. Reject negative, fractional, nonnumeric, null, boolean, or sentinel counts unless the source explicitly documents a different representation.
3. Prefer the verified decoded representation when both raw and decoded aliases exist in the same sample; never add them together. If they conflict, retain a conflict indicator rather than silently reporting a definitive state. Do not assume similarly numbered AVL keys on unrelated protocols mean the same thing.
4. Accept decoded code strings and, only if actually supported by a documented source, arrays of strings. Split ordinary text on whitespace, commas, or semicolons; trim, uppercase, and deduplicate. Keep source order stable.
5. Recognize a conventional five-character OBD code with a full-token match such as `/^[PBCU][0-3][0-9A-F]{3}$/i`. Do not truncate extended codes or treat a code embedded inside a longer token as a match. Keep unexpected text separately as “Unrecognized tracker diagnostic data”; render as text, bound its visible size, and allow expansion.
6. Only label `P0000`, an empty string, or an empty array as an explicit no-codes observation after confirming that source's convention. Otherwise retain “codes not supplied/interpretation unavailable.” Missing, invalid, and explicitly empty are different states.
7. Do not infer pending/confirmed/permanent code status from the code text. Show “Code status not supplied” unless the source explicitly supplies it.

Allowed category legend: **P = Powertrain; B = Body; C = Chassis; U = Network/communication.** Explain this once in the window or show the category beside each recognized code. Do not include descriptions of individual faults, assume an oxygen sensor is defective, or simplify all second-digit values into universal generic/manufacturer-specific rules. [Snap-on category explanation](https://www.snapon.com/EN/UK/Diagnostics/News-Centre/technical-focus/Communication-Issues-U-Codes)

### Step C — reduce observations without inventing clears

| Incoming evidence | Required result |
| --- | --- |
| Newer explicit MIL on | Store on with its own observation time |
| Newer explicit MIL off | Store off; do not erase independently reported codes |
| Positive count or code list, no explicit MIL | Store diagnostics; lamp remains unknown |
| Explicit count zero | Update count only; not proof of lamp off or of a cleared historical list |
| Verified authoritative empty code snapshot | End the prior code-list observation; retain the previous list as historical detail if displayed |
| Packet omits diagnostics | Preserve last observations and age them; do not reset to zero/off |
| Older/out-of-order response | May fill a missing field, but must not overwrite a newer observation of that field |
| Count differs from parsed code-list length | Show both with their dates; do not invent missing codes or silently recalculate the reported count |
| Fetch fails or returns 401/403 | Show unavailable/error appropriately; do not imply lamp off |

Proposed freshness policy for this feature: readings up to **24 hours old** may be labeled recent; older readings must say **“Last reported…”** with a visible date/time and a stale marker. Keep this threshold in one named constant and document it as a UI policy, not a guarantee that a recent reading is still true. An offline vehicle can retain a warning button, but stale data must never use an unqualified current-state claim. An unrelated new GPS packet does not refresh an old diagnostics field.

Latest valid explicit lamp-off information supersedes an older lamp-on observation. Codes may still warrant a diagnostics button; lamp off and a nonempty code list are not mutually exclusive.

### Step D — connect current polling and bounded history

Feed diagnostics from the raw records in `fetchLiveSnapshot` before map-history conversion and cutoff filtering. Preserve the existing polling interval; do not add a second poller or scan historical trips on every update.

On selected binding change or page reload, seed from the authorized current snapshot, then inspect at most the last **7 days** of retained raw positions in bounded daily requests for fields missing from that snapshot. Reduce per-field observations chronologically; retain the original observation times. Treat this as initialization, not proof of current state. Keep bootstrap scheduling separate from map range/trip selection.

Provide an explicit **“Look for older readings”** action in the diagnostics window when needed. It may search the selected authorized binding's older history in bounded daily windows, with progress and cancellation. For the initial Enclave investigation, use the approximate September 1 start and stop at the investigation date or verified binding boundary. Do not hard-code that date as application policy for other vehicles. Label older findings as historical, even if they are the newest retained diagnostic samples.

Use an abort signal or request-generation guard: if the selection switches A → B, late A responses must never populate B's window or icon. Cap concurrent history requests (for example, two), avoid duplicate windows, and distinguish permission errors from empty successful responses. Do not create a new server endpoint that bypasses `TraccarProxyEndpoints` authorization.

For this first version, keep diagnostic observations in memory. After a reload, recover them through the bounded authorized initialization. Do not add persistent browser diagnostic caches. Older findings outside the bootstrap range require the explicit historical lookup; state that limitation in the window. Cross-session server snapshots can be a later task if field transmission is too sparse for this approach.

### Step E — add a shared top-left mobile action stack

Replace the independently positioned Notifications button with a shared container inside `.scope-map-area`, next to the existing `MapOverlay` rendering. Keep the container within the map's coordinate space rather than the page header.

```css
.scope-map-alert-actions {
  position: absolute;
  top: 12px;
  left: 12px;
  z-index: 1101;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
/* Child alert buttons: position: static; width/height at least 44px. */
```

Place the engine button first and Notifications second. Remove/reset Notifications' old `position`, `top`, and `left` rules when moving it into this stack. Do not solve overlap merely by giving the engine button a larger z-index. With two 44px controls and an 8px gap, the second button begins 52px below the first; hiding either must collapse its space naturally.

Use an inline SVG engine silhouette from the project's code-based icon approach. Give the warning an amber treatment with enough contrast on light/dark maps; do not rely on color alone. Labels distinguish “Check-engine light reported on,” “Trouble codes reported,” and stale “Last reported…” evidence. A zero count alone or unknown telemetry alone should not create an alarming engine icon. Keep an entry to the same diagnostics window under **More → Vehicle Diagnostics**, so missing-data/history investigation remains reachable without a warning icon.

Match existing mobile behavior: hide map action buttons while choosing a map location; respect the map area's inert state when a full sheet is open. Check clearance from Leaflet controls, location-picking banners, debug controls when enabled, and the mobile header/safe areas. Keep Notifications' existing click behavior. This initial feature targets compact/mobile layout; test iPad landscape in both compact and wider layout modes and record which is active. If the iPad uses the wide layout, the More entry needs an equivalent accessible menu entry there; do not force a global breakpoint change to expose the icon.

### Step F — open the diagnostics window

Add `activeTask === 'vehicle-diagnostics'` to the existing task routing and render `VehicleDiagnosticsPanel` through `TaskDialog`. Pass the selected vehicle's diagnostic state and retry/history actions. Restore focus to the invoking engine button or menu entry when closing; extend the existing fallback-focus plumbing if necessary.

Window content, in order:

1. **Vehicle Diagnostics — [vehicle name]**.
2. Lamp status: “Reported on,” “Reported off,” “Last reported on [date/time],” or “Status unavailable.” Use the observation time beside the status even for recent data.
3. Reported count and observation time, or “Count not supplied.”
4. Received code list with broad categories and its own observation time, or “No code list received.” Display an explicit verified no-codes snapshot as such. A count without a list must say that the tracker supplied the count but not the individual codes.
5. Any stale/source-conflict/count-mismatch note. For old codes after an explicit empty snapshot, clearly separate “Previously reported codes” from the latest code-list observation.
6. Last successful lookup, retry, and bounded older-reading lookup controls. The lookup time must not replace field observation times.

Use vehicle names for ordinary users; raw keys, tracker/position IDs, and mapping revisions belong in a collapsed diagnostic detail area if useful. Escape all received text through React text rendering. Include loading, empty, offline, permission-denied, and retry states. Closing and reopening must not fetch all history again. Changing vehicle while open must immediately change the heading and clear the former vehicle's content while the new state loads.

## 6. Verification checklist

Add focused pure-helper tests; do not rely solely on a production build.

- [ ] Count-only, codes-only, explicit MIL on/off, and completely absent diagnostics.
- [ ] Both exact raw/decoded aliases; one alias missing; conflicting aliases; no cross-protocol AVL guessing.
- [ ] Missing count versus zero, invalid/null/boolean/sentinel count, unknown code text, lowercase/duplicate/delimited codes, and full-token validation.
- [ ] Explicit empty convention versus omitted codes; a fresh GPS-only packet does not clear or refresh diagnostics.
- [ ] Separate count/code/MIL timestamps; freshness boundary; invalid/future dates; out-of-order responses.
- [ ] Explicit off after on; codes may remain with lamp off; count/list mismatch remains visible.
- [ ] A → B selection race; binding replacement; logout; denied device access; Bouncie/imported provenance cannot establish the current tracker state.
- [ ] Reload bootstrap cap, cancellation, bounded older lookup, and historical results visibly dated.
- [ ] Synthetic mobile fixture shows engine and Notifications simultaneously without intersection; each opens the correct window. Repeat with either/both absent.
- [ ] iPhone portrait/landscape, iPad landscape, browser zoom, long code list, narrow dialog, and tall bottom sheet; neither warning control becomes unreachable behind another map control.
- [ ] Keyboard activation, readable labels, modal focus containment, Escape/Close, and focus restoration to the invoking control.
- [ ] Regular user sees only authorized vehicle diagnostics; unknown data never becomes a confirmed current lamp-on/off claim.

Run `npm test`, `npm run build`, and the project linter using the repository's supported execution method. Follow `AGENTS.md` for Windows path/sandbox behavior. Review existing lint warnings separately from warnings introduced by this change. No backend build is needed unless backend code is actually changed.

## 7. Real-data acceptance and handoff

- [ ] Save an investigation summary identifying the actual tracker/decoder/configuration and which diagnostic fields are present, absent, or unverified.
- [ ] Search retained post-installation history and distinguish actual field observations from imported Bouncie history and missing retention.
- [ ] After the owner restores tracker diagnostics, verify count and code-list transmission independently on fresh samples.
- [ ] Verify an explicit MIL source against the vehicle before enabling the confirmed lamp label. If unavailable, deliver the diagnostics-only wording and record this limitation.
- [ ] Confirm the Enclave's displayed codes match the received payload exactly; no repair descriptions or guessed O2 sensor code numbers appear.
- [ ] Confirm both top-left map controls work on a real iPhone and an iPad in landscape.
- [ ] Record frontend tests/build results, remaining hardware-dependent checks, and any chosen changes to the proposed freshness/lookback defaults.

Do not mark real-data lamp verification complete using synthetic fixtures, a historical DTC maximum, or a positive count alone. The feature must still behave correctly if the owner never restores code transmission: useful unavailable/historical information, no fabricated codes, and no false “all clear.”

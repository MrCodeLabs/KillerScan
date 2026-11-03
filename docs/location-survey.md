# Persistent initial Home Assistant survey

Implemented in the fork on **28 October 2026** and integrated with KillerScan
**1.9.0** on **3 November 2026**.
This inventory supports an initial customer visit. Installation work orders,
acceptance, contracts, maintenance, router/AP connectors and new reports are
outside its scope.

## Workflow

Select a saved Location and open **HA**. A Temporary scan can use the advisor;
choose a saved Location to edit a persistent inventory. **Current scan** still contains every found device, independent of
the workspace filter. Select one or more rows and choose **Add selected to
inventory**. Nothing is added automatically. The second tab, **Location inventory**,
shows the explicitly selected location name and its saved inventory even when
there are no current scan results.

Use **Add device manually** for a Zigbee sensor, equipment behind a bridge or
an unavailable device. Only its name is required; no IP or MAC is needed. Edit
name, room, manufacturer/model, firmware, connection/protocol, desired Home
Assistant functions and notes. All are user information, distinct from the
saved network observation. The connection field accepts free text; the advisor
also offers its existing known-platform/protocol and monitor/control/media goals.

The editor's **Home Assistant** action uses the existing recommendation rules.
**Use these hints** returns model/protocol/goal changes to the editor only.
Choose **Save** in the editor to persist them. Canceling either dialog discards
its unaccepted changes. Existing per-device HA actions also offer an explicit
inventory save and recover previously saved hints through a confirmed MAC link.

**Remove from inventory** asks for confirmation and never removes scan history.
**Link observation** explicitly associates the selected entry with a device from
the current scan. Check identity before accepting, especially for entries created
manually or without a MAC. Opening any of these windows performs no discovery,
device query, HA login or installation. Documentation links still open only when
requested.

## Identity, observations and recommendations

- Every entry has its own stable ID inside an explicit location. Identical
  IP/MAC values at different locations do not join their data.
- Re-adding an automatically linked network device with the same normalized
  MAC updates that entry without overwriting manually entered fields.
- An IP match without a MAC requires a choice: an existing entry, a separate new
  entry, or cancel. IP alone is never an automatic identity.
- Manually created entries are never automatically merged into scan results.
  An explicit link can enable subsequent MAC-based observation refreshes.
- Completed GUI scans and successfully completed individual/deep probes refresh
  only existing unambiguous MAC links. No scan result creates inventory entries.
  A duplicate MAC within one result set is left for manual review.
- IP changes do not erase room, model, firmware, goals, functions or notes.
  Missing devices remain in the inventory with their original last-observed time.
  Older or undated results do not replace a newer dated observation automatically.
- The inventory displays **Saved observation (not a live status)**. Capture time
  comes from the saved recognition observation, never the act of opening or
  adding a record. Legacy results lacking a trustworthy time say it is unknown.
  The local save time is separate. Saved advisory facts retain their source/time
  and are marked reused. Manual hints never become captured evidence.
- A recommendation, including one selected from manual input, is still a proposal
  requiring model, firmware, prerequisites and actual functionality to be checked.
  This extension does not change the advisor catalogue or selection policy.

CLI scan/probe output remains unchanged and does not write this GUI inventory.
Existing public Engine APIs, scan JSON/CSV/HTML, history and profile formats remain
compatible. This step adds no inventory reports or automatic migration of old data.

## Storage and recovery

`locations/<location-id>/survey.json` uses version `1` and includes the location
ID, stable entry IDs, manual fields, an optional MAC link, and a separate frozen
network observation. It is not a legacy migration input. The file is local user
data and must not be committed or attached to public issues.

Writes use the existing atomic temporary-file/replace helper and retain the
previous file as `survey.json.bak`. A per-file writer lock serializes cooperating
instances. Saving edits rereads the latest observation instead of replacing it
with the editor's older snapshot. Corruption, an unsupported version, wrong
location or an unsuccessful write is reported; the existing file is preserved.
There is no silent reset, fallback to another location or automatic backup restore.
Resolve the error before continuing to edit the inventory. The rest of the scan
history is not reinterpreted as inventory.

### Before using existing customer data

1. Close every KillerScan process using the data folder.
2. Copy the **entire data root**, including `locations.json`, `locations/`,
   settings and existing backups, to a dated sibling backup folder. The default
   is `%LOCALAPPDATA%\KillerScan`; an isolated/USB launch can override it with
   `KILLERSCAN_DATA_ROOT`. Back up the actual overridden folder in that case.
3. First run the development executable with a **copy** of that root through
   `KILLERSCAN_DATA_ROOT`. Select the intended location explicitly. Do not start
   a real network scan until the visit/test has been authorized.
4. To roll back, close all copies, preserve the post-test root separately, and
   run the previous executable with the untouched whole-root backup. Never merge
   files from different location IDs or restore a catalogue without its matching
   location folders. A previous build does not understand the new survey file.
5. To recover one corrupt survey, keep the bad file and validate that its `.bak`
   has version `1` and the **same location ID** before manually restoring a copy
   with the application closed. If this cannot be established, keep both files
   for diagnosis instead of deleting them.

Demo mode keeps inventory edits in memory only. Use an isolated normal launch,
not demo mode, to check persistence across actual application restarts.

## Verification

`SpeedTest.Tests.exe --survey` runs synthetic fixtures and two fresh child
processes. It covers location isolation, stable IDs, explicit additions/manual
entries, MAC de-duplication, IP confirmation/new/cancel, explicit links, changed
IP, absent devices, old observations, immutable snapshots, manual provenance,
special characters, backup creation, malformed/wrong-location/version data and
failed atomic replacement. It is also part of the focused CI suite.

`--release-ui` additionally checks inventory selection, editor save/cancel,
advisor round trips, manual creation, removal confirmation, unchanged history,
all 20 language dictionaries and rendering across 13 themes at 125%/200% scale.
All test devices use fabricated names and documentation addresses. Existing
Engine, history, location/profile and export regression suites remain applicable.

Physical customer networks, installed user data and an actual HA setup have not
been used for this feature's development checks. First deployment still needs a
backed-up data copy and a supervised initial visit.

## Kratka navodila

**HA → Trenutni pregled → izberi naprave → Dodaj izbrane v popis.** Na zavihku
**Popis lokacije** dodaj tudi ročne naprave, prostor in želene funkcije. Samo ime
je obvezno. Model, protokol in cilj iz HA svetovalca se ohranijo šele s potrditvijo
namigov in **Shrani** v urejevalniku. **Prekliči** ne shrani sprememb. Zadnje
opažanje ni dokaz trenutne dosegljivosti ali potrjene združljivosti.

Pred uporabo z obstoječimi podatki zapri program in kopiraj **celotno podatkovno
mapo**. Najprej preizkusi njeno ločeno kopijo. Za povratek ohrani tudi podatke po
preizkusu in uporabi prejšnjo sestavo z nedotaknjeno celotno varnostno kopijo.

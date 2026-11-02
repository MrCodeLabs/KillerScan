# Fork synchronization with upstream 1.9.0

Prepared **2 November 2026** from upstream
[`bb1a384`](https://github.com/SteveTheKiller/KillerScan/commit/bb1a38412a3a0cfc5edabff226a1abc9a1d8ac89)
and fork `418e1ee`. The merge retains both histories. Shared scanning, Locations,
recognition, access, installation and UI functionality now use the upstream
implementations; the fork's additional Home Assistant advice remains available.

## Retained fork behavior

- The **HA** toolbar button lists every currently found device independently of
  filtering or selection. Individual device, Services and saved-history actions
  still open the same read-only advice. The device table retains its HA choice.
- The existing catalogue and its manual/research/observed distinctions are kept.
  No HA login, pairing or installation is performed. All 61 HA labels remain in
  every one of the 20 translation dictionaries.
- Upstream `Recognition` and the fork's additive `Identification` / `HomeAssistant`
  JSON can coexist. Fresh probes populate both contracts from the same captured
  values before saved display-name callbacks run. Reading imported fields does
  not invent a capture date. Reused multicast evidence keeps its earlier time.
- Old access JSON using `Verification`, `Authentication` and `ObservedAtUtc`
  remains readable alongside the upstream response/login enums. A login request
  remains a request, not proof that signing in is possible.
- Existing evidence, access and HA snapshots survive history loads and new saves.
  CSV retains the first six device columns and the upstream Confidence/Evidence/
  Access columns, followed by the fork's recognition/model/JSON/Location columns.
  Readers should select columns by name and tolerate additions. HTML retains a
  sortable HA column with the reason and evidence in its details.
- The previous fork's explicit catalogue selection (`SelectedId`) is preserved.
  Existing site IDs, manual names, approvals and saved scans stay in their own
  directories. Root-level files preserved by the earlier fork migration are
  backups; they are not imported a second time as new saved scans.
- Old saved adapter IDs/source addresses remain readable. An adapter ID that
  cannot be resolved stays an explicit missing choice instead of silently becoming
  Automatic. Validate the actual adapter before a real scan.
- `KILLERSCAN_DATA_ROOT` still isolates development data and settings. The install
  badge is unavailable from an isolated root. Publication and release-monitoring
  workflows retain their event conditions and are additionally restricted to the
  original publisher's repository; this fork does not publish its package IDs.

The unpublished persistent survey development is preserved separately and is
**not included in this synchronization PR**. Its existing source and preview
continue to use the earlier base. Port that feature separately to the new
Location services and family controls before combining it with 1.9 data.

## Data precautions

No installed user data is changed by updating the source repository. Before
running a newly built executable against existing data, close all copies using
that root and copy the **entire** actual data folder, including `locations.json`,
`locations/`, settings, saved surveys and backups, to a dated sibling directory.
The default is `%LOCALAPPDATA%\KillerScan`; USB/test launches can override it.
First use another working copy of that backup through `KILLERSCAN_DATA_ROOT`.

To roll back, close the application, keep the post-upgrade data separately, and
use the previous executable with the untouched full backup. Do not combine a
catalogue from one version/copy with Location folders from another. Reading
synthetic previous-fork fixtures is tested; this is not proof that every damaged
or privately modified data folder can be upgraded.

## Validation

- Application and both Engine targets build with no compiler errors or warnings
  after successful dependency restore/audit access.
- Engine validation: 38 suite invocations across net48 and net10.0, including
  each framework's default 19 regression checks and all focused groups. The HA
  group includes actual HTTP probing of an isolated loopback server, old JSON
  round trips and capture provenance.
- Application: 17 targeted suites cover existing fork data, Locations, history,
  preferences, lifecycle, recognition, access, terminal safety and exports.
- Offscreen UI: 45 theme/accent states, 20 locales and 120 topology combinations,
  plus the retained HA overview, manual/unknown flows and saved-history advice.
- Existing-fork fixtures include two sites reusing the same IP/MAC, remembered
  names/approvals/types, disconnected adapter settings, old HA evidence and
  unchanged survey files. The first read preserves their original file bytes.
- Workflow mappings are checked for duplicate keys; every package/monitor job has
  the fork guard. The test suite inventory includes the new compatibility tests.

These checks use fabricated devices, scratch data and controlled listeners.
No customer-network scan, production HA connection or installed update/uninstall
has been performed. A narrowly scoped invariant number-format fix also makes the
upstream subnet-limit error consistent under non-English Windows cultures.

## Slovensko

Ta uskladitev prevzame originalni 1.9.0 in ohrani naše HA izbire ter dokaze.
Trajni popis ostaja ločen razvojni korak; ni del tega PR. Pred uporabo nove
sestave z obstoječimi podatki zapri program, kopiraj celotno podatkovno mapo in
najprej preveri njeno ločeno delovno kopijo. Za povratek uporabi prejšnjo sestavo
z nedotaknjeno celotno kopijo. Posodobitev kode sama ne nadgradi nameščenega programa.

# Home Assistant integration advice

Implemented on **23 October 2026**; publication review **24 October 2026**. Catalogue revision
`2026-10-23.3`; advice schema version `2`.

## Open the advisor

The **Home Assistant** column directly shows one integration choice per found
device; click it for the reason, prerequisites and documentation. Device, service
and saved-history context menus open the same details.

The **HA** toolbar button prepares an overview for **all devices in the current
scan**, regardless of selection or text filter. Each frozen row shows its own
choice, reason and prerequisites. Select a row and open details, or copy the full
plan. An empty scan prompts you to scan first. It never reruns discovery itself.
The new labels are present in all 20 application languages.

This is a local, read-only advisor. It performs no additional probes, cloud
lookups, Home Assistant login, installation, pairing or configuration. Opening
documentation is an explicit user action and uses a fixed documentation URL without
device information in the URL. The ordinary browser request still reaches the
documentation host.

## What recommendations mean

A candidate contains the integration domain, its basis, the actual supporting
evidence and prerequisites to check. Captured hints retain their source,
timestamp and cached status. A hint is not validated compatibility: product
banners can be self-reported, cached, ambiguous or obsolete. Check the exact
model, firmware, required entities and authentication in the linked documentation.
The advisor does not invent a compatibility percentage or reuse the device-type
classifier's confidence as a compatibility score.

## How the best choice is selected

The advisor selects one evidence-backed device/platform integration
ahead of generic protocol fallbacks. For example, Shelly wins over its HomeKit
advertisement, Tasmota over its generic MQTT lead, and Sonos/Roku over a generic
DLNA renderer. Generic protocol preference is Cast, HomeKit Device, Matter,
ONVIF/IPP, DLNA renderer, then MQTT. This is a documented catalogue policy, not
a measured universal ranking of every possible integration. Model support and
required functionality still need to be checked before installation.

Captured evidence takes precedence over optional manual research leads. Without
captured evidence, an explicit platform/protocol lead can supply a choice labeled
as user input. Generic REST/SNMP/radio research alone is not enough for a direct
choice. Conflicting native platform identities leave the choice unresolved.
Where only a manufacturer or a meaningful product name is available, the row
now shows **Check: integration**, followed by an actionable documentation button
and model/firmware/account prerequisites. This is explicitly **research**, not
detected support: `RecommendedDomain` stays null, `Basis` remains `Research`, and
the evidence retains its original source, time and cached status. Captured
platform/protocol evidence takes precedence over these weaker leads. A manual
firmware entry can refine a research lead without overwriting captured evidence.

Generic Espressif OUI or `ESP_` module names instead show **ESP: check firmware**.
The next step distinguishes existing eWeLink → SonoffLAN, ESPHome → ESPHome and
Tasmota → Tasmota, with direct documentation for each. It never chooses arbitrary
firmware based on the chip and never flashes the device. A known ecosystem lead
such as Tuya precedes generic chip research. Sonoff hardware branding alone is
not observed eWeLink firmware and does not conflict with a Tasmota response.

Unidentified devices without a useful lead still show **Needs identification**.
The detailed evidence, alternatives and investigation workflow are expandable;
the suggested route and next action are shown first. All new labels are available
in the application's 20 languages.

The catalogue currently covers these observed leads:

| Integration | Accepted hint | Check before setup |
| --- | --- | --- |
| [Shelly](https://www.home-assistant.io/integrations/shelly/) | Specific Shelly product/firmware text | Supported generation/model and authentication |
| [Philips Hue](https://www.home-assistant.io/integrations/hue/) | Hue Bridge text or `_hue._tcp` | Compatible bridge and pairing |
| [ESPHome](https://www.home-assistant.io/integrations/esphome/) | ESPHome text or `_esphomelib._tcp` | Existing firmware and enabled native API |
| [Tasmota](https://www.home-assistant.io/integrations/tasmota/) | Tasmota text | Supported firmware, broker and device discovery |
| [Kasa/Tapo](https://www.home-assistant.io/integrations/tplink/) | Kasa or Tapo text | Exact supported model and account/authentication requirements |
| [ONVIF](https://www.home-assistant.io/integrations/onvif/) | Explicit ONVIF text | Profile S and credentials; RTSP alone is insufficient |
| [DLNA renderer](https://www.home-assistant.io/integrations/dlna_dmr/) | SSDP `MediaRenderer` | Renderer capabilities; a media server is different |
| [HomeKit Device](https://www.home-assistant.io/integrations/homekit_controller/) | `_hap._tcp` | Accessory support, pairing code and pairing state |
| [Matter](https://www.home-assistant.io/integrations/matter/) | Commissioning service `_matterc._udp` | Confirmed Matter support, IPv6 and commissioning prerequisites |
| [MQTT](https://www.home-assistant.io/integrations/mqtt/) | MQTT service or Mosquitto banner | Broker plus compatible device topics/discovery |
| [Sonos](https://www.home-assistant.io/integrations/sonos/) | Sonos service or product text | Supported device and network access |
| [Google Cast](https://www.home-assistant.io/integrations/cast/) | `_googlecast._tcp` or Cast product text | Supported device and required media functions |
| [Roku](https://www.home-assistant.io/integrations/roku/) | Explicit Roku response text | Model and control prerequisites |
| [Samsung TV](https://www.home-assistant.io/integrations/samsungtv/) | Samsung TV/DTV product text | Supported model and permission/authentication |
| [LG webOS](https://www.home-assistant.io/integrations/webostv/) | Explicit webOS product text | Supported model and pairing |
| [Synology DSM](https://www.home-assistant.io/integrations/synology_dsm/) | Explicit DSM product text | Supported DSM and account permissions |
| [Denon AVR](https://www.home-assistant.io/integrations/denonavr/) | Explicit Denon AVR/AVC response; name/vendor is research only | Supported model and network access |
| [Reolink](https://www.home-assistant.io/integrations/reolink/) | Reolink product response; vendor/name is research only | Supported model, firmware, credentials and required camera functions |
| [Tuya](https://www.home-assistant.io/integrations/tuya/) | Tuya/Smart Life response; vendor/name is research only | Device linked to Tuya Smart/Smart Life account and exposed functions |
| [Proxmox VE](https://www.home-assistant.io/integrations/proxmoxve/) | Proxmox response; specific host name is research only | Actual Proxmox host/API and account permissions; virtual NIC OUI is not enough |
| [go-e APIv2 Connect](https://github.com/marq24/ha-goecharger-api2) | go-e product response; vendor/name is research only | Community HACS integration, compatible firmware and local API v2/WebSocket |
| [SonoffLAN](https://github.com/AlexxIT/SonoffLAN) | eWeLink response; Sonoff/eWeLink name is research only | Community HACS integration, original eWeLink firmware and supported LAN/cloud mode |
| [IPP](https://www.home-assistant.io/integrations/ipp/) | `_ipp._tcp` or `_ipps._tcp` | Supported printer and available sensors |

Names, manually assigned device types, vendor OUI, a generic TP-Link/Espressif
label, open ports, HTTP 200, RTSP, UPnP `MediaServer`, and Thread alone cannot
establish observed integration support. A specific manufacturer/name may only supply a labeled research suggestion. SNMP `sysDescr` offers a **research route**, not
ready-made entities. Conflicting platform hints or conflicting recognition
evidence display a conflict instead of selecting a best integration.

This is a small curated catalogue, not the whole HA integration registry.
Existing scanner discovery coverage and disabled scan options limit the available
hints. The advisor consumes existing captures. The scanner's existing mDNS mode
also queries ESPHome, Matter commissioning and MQTT service types. Real mDNS
instance names are accepted, and SSDP retains device/service types alongside
SERVER text. Queries and empty replies do not create phantom devices.
No match therefore means **investigate**, not **unsupported**.

## Discovery fixes accompanying this revision

mDNS/SSDP-only responders within the requested target set now enter the device
list even if ICMP/TCP discovery is silent. Both quick and full scans retain their
evidence. Explicit ARP can also discover silent **on-link** hosts rather than
only resolving MACs for already discovered devices. Native ARP discovery is
throttled to 64 concurrent calls, honors cancellation of the caller's wait, and
is skipped for routed next hops. Explicit binding additionally checks the source
interface route. Disabling ARP/mDNS/SSDP prevents the corresponding new path.
Additional ARP work can make scans of silent address ranges take longer.

These fixes were reproduced using isolated fixtures. They do not establish the
cause of every missing-device report: sleeping devices, VLAN separation,
wireless client isolation, selected interface, target range and disabled options
can still affect discovery. Physical-network validation remains necessary.

## When nothing is recognized

1. Find the product label, exact model, firmware and vendor app/manual. Decide
   which HA entities/functions are needed: reading state, control, or media.
2. Optionally enter the model and known protocol in the window. These are manual
   research leads, never new observations or changes to saved identity. Inputs
   stay in this window; **Copy integration plan** explicitly includes them.
3. Check [HA discovery and Add integration](https://www.home-assistant.io/getting-started/integration/)
   and the [official integration catalogue](https://www.home-assistant.io/integrations/).
   Prefer a supported integration with the required functions; compare local/cloud
   access, availability and maintenance.
4. Confirm documented Matter, HomeKit, MQTT or API support. For a documented API,
   investigate [REST](https://www.home-assistant.io/integrations/rest/); for known
   OIDs/read access, [SNMP](https://www.home-assistant.io/integrations/snmp/).
   A web login or open port does not establish an API contract.
5. Check non-IP connections: [Zigbee](https://www.home-assistant.io/integrations/zha/),
   [Z-Wave](https://www.home-assistant.io/integrations/zwave_js/) or
   [Bluetooth LE](https://www.home-assistant.io/integrations/bluetooth/) may need an
   adapter or hub and will not necessarily appear in an IPv4 scan. Thread is a
   transport and does not by itself establish Matter compatibility.
6. Test one read-only entity, required functions, unavailable-device behavior and
   recovery after restart. For control, verify command/state feedback before
   extending to actuators.
7. If official support is missing, evaluate a maintained community integration
   (see [HACS usage](https://www.hacs.xyz/docs/use/)) or a documented MQTT/REST/SNMP
   mapping. Check versions and maintenance. Do not flash unidentified hardware.

There is no universal best integration without the exact model and requirements.
The advisor makes the next investigation actionable while keeping uncertainty
visible. A broker, gateway or DHCP entry is not proof that an end device supports
the corresponding integration.

## History and export contracts

`NetworkDevice.HomeAssistant` is optional for legacy/unassessed records. Version 2
adds `RecommendedDomain` and `RecommendationReason` alongside candidates. New
recognition assessments populate advice even for an unknown device. Saved history
owns an independent snapshot with catalogue revision and original evidence times;
it does not silently become a current compatibility certification. Legacy records
without advice can derive a plan from their saved recognition evidence using the
current catalogue; no network request or history rewrite is performed.

Device JSON and PowerShell objects expose `HomeAssistant`. Device, service,
history and CLI CSV append **Home Assistant JSON**. The first six CLI CSV columns
keep their order; consumers requiring exactly six columns must accept the added
column or select fields from JSON. HTML adds an escaped, expandable localized
plan in each device row. CSV quoting/formula protection is retained.

Manual window inputs are not written into device JSON/history/exports. Use the
explicit copy action to keep that particular investigation plan. Opening the
window freezes its evidence so an ongoing scan cannot mix later observations
into the displayed recommendation.

## Validation and limits

Publication validation on **24 October 2026**: Release builds had no warnings or
errors; all 40 Engine checks passed on each of net48 and net10.0. Isolated UI
checks covered 20 locales, 45 theme/accent states and 120 topology combinations,
including direct choices, research/ESP routes, fixed documentation actions and
copy-all. Location/history/profile checks, CSV/HTML regressions, the real
PowerShell module/EXE pipeline and demo CLI JSON/CSV/HTML passed. All 61 HA keys
in every locale have matching placeholders and existing translations are preserved.

Synthetic Engine checks cover supported hints, weak/no evidence, conflicts,
cached times, manual provenance, bounded strings, snapshots and JSON round trips.
Isolated GUI checks cover observed/unknown/manual workflows in all 20 languages,
empty scan access, fixed documentation actions, copying and safe exports.
Translation key/placeholder coverage is checked automatically; native-speaker
review and physical-device/real-HA compatibility testing remain separate work.
The advisor does not contact or alter a production HA instance.
The catalogue has 28 entries (23 integration routes and five generic research
routes); all 61 advisor resource keys are supplied in each of the 20 locales.
Community integrations are marked explicitly and link to their fixed project
documentation. They do not become built-in official integrations.

## Reporting a missing device or an unsuitable choice

For discovery, report the app/build used, whether the scan completed, approximate
expected/found counts, selected interface type, VPN or separate-network context,
enabled discovery options, elapsed time and any sanitized error message. Start
with one missing device's manufacturer, exact model and connection type.

For integration advice, include the exact model/firmware, the proposed integration,
required functions and whether local-only operation is required or cloud access
is acceptable. For a later real-HA test, include the HA version/installation type
and existing hubs, radio adapters or MQTT setup. This information is not needed
to open the offline advisor.

Keep credentials, full scan exports, real IP/MAC inventories and private topology
out of public issues. Use a minimal anonymized reproduction. A live-network or
real-HA pilot is separate from the isolated validation described above.

# Data licensing status — GBFS sources

Licence clearance for contributing station data from this repo's six GBFS feeds into
OpenStreetMap (ODbL 1.0). Compiled 2026-09-02. Every quote below was fetched from the
cited URL; nothing here is recalled or paraphrased.

**This applies to what we already publish, not only to a future bulk import.** A
MapRoulette task embeds station coordinates and names, so it is a redistribution of the
source data. Whatever bucket a source falls into governs the challenges we already
generate.

More directly: this repo is **public** (<https://github.com/skfd/bikeshare-toronto-maproulette>)
and commits per-system snapshots. `src/data_results/Citi Bike/bikeshare.geojson` (2,508
stations) and `src/data_results/Mobi/bikeshare.geojson` (263 stations) are stand-alone
redistributions of those two feeds, published now — squarely within what Citi Bike clause
2(b) and Mobi §2.1(b) prohibit. That is independent of any import and predates this
review. Whether to remove them, make the repo private, or leave them pending a grant is an
operator decision; it is recorded here so it is not discovered later.

## Bottom line

| System | Feed ver | Licence basis | Status |
|---|---|---|---|
| **Bike Share Toronto** | v1.x | OGL 1.0 – Toronto (LWG-approved 2024-04-08) | ✅ **Cleared** |
| **Bixi** (Montréal) | v2.2 | Operator permission, 2025-08-19 | ✅ **Cleared** |
| **Hamilton Bike Share** | legacy SoBi | City of Hamilton Open Data Licence (LWG-approved 2024-11-18) | ⚠️ **Cleared via City mirror**; feed itself unlicensed |
| **àVélo** (Québec) | v1.x | RTC bespoke licence, never assessed | ❌ **Needs written grant** |
| **Mobi** (Vancouver) | v2.2 | Feed says ODbL-1.0; operator agreement forbids redistribution | ❌ **Needs written grant** |
| **Citi Bike** (NYC) | v2.3 | Lyft Data Sharing Policy | ⛔ **Incompatible** — stop until granted |

Two cleared, one cleared-with-a-caveat, two needing a letter, one that should stop.

## The rule that does *not* apply

The GBFS spec's default-licence rule — blank/omitted `license_id` and `license_url` mean
CC0 — **exists only in v3.0 and later**. None of our feeds is v3.x. Occurrences of the
clause in `gbfs.md` by tag: v1.0 **0**, v1.1 **0**, v2.0 **0**, v2.3 **0**, v3.0 **2**.

The `v2.2` tag does contain the sentence, but it is a tagging artifact, not a rule:
it self-annotates `*(as of v3.0-RC)*`, it references a `license_id` field that v2.2 does
not define, and the v1.1/v2.2/v2.3 tags were all cut retroactively within three days in
April 2022 from a later working state.

Bixi's `"license_url": ""` is legally identical to omitting the field — GBFS v2.2 field
presence conventions: *"An omitted field is equivalent to a field that is empty."*

So silence means silence, and silence lands in the OSMF's **"No licence"** category:

> Data that has no determinable license, terms of use or is not published with reference
> to clear legal statutes is not suitable for use with OpenStreetMap. The absence of
> documented terms does not imply that nobody or no organization has rights in the data.
>
> — <https://osmfoundation.org/wiki/Licence/Licence_Compatibility>

Even for a hypothetical v3.0 feed, do not rely on the spec default: MobilityData is a
standards body, not a rights-holder; the Licensing section is RFC-2119 advisory
("RECOMMENDED"/"SHOULD"); the maintainers left the contradiction unresolved in
[issue #438](https://github.com/MobilityData/gbfs/issues/438), closed as stale; and the
`data-licenses.md` the spec cites in support is a 404.

---

## ✅ Bike Share Toronto — cleared

**Basis:** the City of Toronto Open Data Portal republishes *our exact endpoint* as a City
dataset under OGL 1.0 – Toronto.

- Dataset: <https://open.toronto.ca/dataset/bike-share-toronto/> (CKAN id
  `2b44db0d-eea9-442d-b038-79335368ad5a`)
- Its `bike-share-gbfs-general-bikeshare-feed-specification` resource is a GBFS discovery
  document advertising `https://tor.publicbikesystem.net/ube/gbfs/v1/en/station_information`
  — byte-for-byte our source.
- Licence: <https://open.toronto.ca/open-data-licence/> —
  > The Information Provider grants you a worldwide, royalty-free, perpetual,
  > non-exclusive licence to use the Information, including for commercial purposes […]
  > Copy, modify, publish, translate, adapt, distribute or otherwise use the Information
  > in any medium, mode or format for any lawful purpose.

  No share-alike, no NC, no ND.
- LWG assessment: `OGL 1.0 Toronto, compatible (LWG minutes 2024-04-08)` —
  <https://osmfoundation.org/wiki/OGL_Canada_and_local_variants>
- `Contributors` entry already exists (Ontario § Toronto).
- **Precedent is ours:** the Toronto Address Points import (~449k addresses, 1,297
  changesets, completed 2026-05-28, user `skfd`) ran on this same licence with no waiver.
- TPA's own 2020 Board report confirms the City treats the live feed as its open data:
  > Bike Share Toronto publishes a JSON file […] the open Application Programming
  > Interface (API) transmits live data under the General Bikeshare Feed Specification
  > (GBFS).

**Attribution:** `Contains information licensed under the Open Government Licence –
Toronto` (the licence's fallback; no dataset-specific statement is set). Our address
import used `source=City of Toronto Open Data` — match that for consistency.

**Loose end (cosmetic):** the CKAN API reports `license_id: "notspecified"` though the
rendered page and FAQ both assert OGL-Toronto. One email to `opendata@toronto.ca` asking
them to populate the field would close this permanently against a reviewer challenge.

## ✅ Bixi — cleared

**Basis:** a direct operator grant, already obtained and recorded on the OSM wiki — *not*
a compatible licence. BIXI's own position is that no licence exists.

- <https://wiki.openstreetmap.org/wiki/Bixi> —
  > Bixi has given explicit permission to use their open data and their GBFS feed on
  > 2025-08-19 in an email exchange between Émilio (res260) and Bixi support.
- The grant (Claude Hervé, BIXI, 2025-08-19, ticket #774370):
  > Bixi autorise la publication de ses données accessibles via son feed GBFS et
  > l'ensemble de ses données ouvertes.
- `Contributors` entry exists: *"Explicit permission has been granted to use their data
  in OpenStreetMap."*

**Read the first reply before writing to any other operator.** On 2025-07-29 BIXI said
*"aucune licence spécifique n'a encore été définie […] vous pouvez les utiliser
librement"* — then added *"l'utilisation commerciale ou la vente de ces données n'est pas
autorisée."* That is ODbL-fatal. It took a second round explaining that ODbL permits
commercial use to get the unconditional grant. **Expect this failure mode every time and
pre-empt it.**

**Corroboration:** donnees.montreal.ca republishes the same feed
(`gbfs.velobixi.com/gbfs/gbfs.json`) under CC-BY 4.0, and the City publishes a standing
OSM authorization (*"La Ville de Montréal autorise explicitement la réutilisation de ses
données ouvertes par OpenStreetMap"*, 2014-08-15). But the portal states BIXI, not the
City, holds copyright — so this corroborates, it does not stand alone.

**Not a licensing issue, but blocking in practice:** user `res260` is planning a
*recurring* BIXI GBFS import via `gbfs2osm`
(<https://wiki.openstreetmap.org/wiki/Montreal_Bixi_GBFS_Dataset>, 20 m conflation
radius), and a 724-station BIXI import already ran in 2024. Coordinate in forum thread
[133246](https://community.openstreetmap.org/t/province-de-quebec-mapping-bikeshare-infrastructure-from-gbfs-feeds/133246)
before running anything, or two tools will edit the same nodes.

## ⚠️ Hamilton Bike Share — cleared via the City, not via our feed

**The City route is clean.** `open.hamilton.ca` publishes *Hamilton Bike Share
Incorporated Hubs* (`OWNER: OpenHamilton`, `SOURCE: City of Hamilton`,
`contentStatus: public_authoritative`, `recordCount: 198` — matching our snapshot's 198
features exactly) under the City of Hamilton Open Data Licence:

> The City of Hamilton grants you a worldwide, royalty-free, perpetual, non-exclusive
> licence to use the Data, including for commercial purposes […] You may copy, modify,
> publish, translate, adapt, distribute or otherwise use the Data in any medium, mode or
> format for any lawful purpose.

- LWG assessment: `Open Data Licence Hamilton, compatible (LWG minutes 2024-11-18)` —
  a standalone approval, not inherited from OGL-Canada.
- `Contributors` entry already exists with the required wording: *"Contains public sector
  Data made available under the City of Hamilton's Open Data Licence"*.

**But the feed we actually read is not that dataset.** `hamilton.socialbicycles.com/opendata/`
carries no licence at all, and the SoBi/MobilityCloud platform ToS it sits behind is
hostile:

> no part of the Site and no Content or Marks may be copied, reproduced, aggregated,
> republished […] Systematically retrieve data or other content from the Site to create
> or compile, directly or indirectly, a collection, compilation, database, or directory
> without written permission

Whether that ToS (which names only `app.socialbicycles.com`) reaches the `/opendata/`
subdomain is arguable, but it is not something to defend as an import's cited provenance.

**Recommended fix — route (b), not (a).** Re-sourcing from the City layer breaks our join
key: the Hubs layer's fields are `OBJECTID, NAME, ADDRESS, DESCRIPTION, CURRENT_BIKES,
AVAILABLE_BIKES, FREE_RACKS, RACKS_AMOUNT, LONGITUDE, LATITUDE, LAST_IMPORT_DATE` — **no
station id**, and `OBJECTID` is an ArcGIS row number. We join on `ref`, so we'd be left
matching on name alone. Instead, email `help@hamiltonbikeshare.ca` (cc
`opendata@hamilton.ca`) asking HBSI to confirm the GBFS feed is offered under the same
City licence their hub data already appears under, ideally by adding `license_url` to
`system_information.json`. Since the City already republishes the identical 198 stations
openly, this is a formality — and a `license_url` in the feed is self-documenting forever.

## ❌ àVélo (RTC Québec) — needs a written grant

RTC publishes a bespoke licence (<https://www.rtcquebec.ca/donnees-ouvertes>), never
assessed by the LWG. It permits commercial use and reproduction, but fails on two counts.

**1. Revocable without cause:**

> Le RTC se réserve le droit […] d'exiger le retrait de tout produit, service ou
> application utilisant les Informations publiques à l'encontre de la présente licence
> **ou dont l'utilisation peut porter atteinte à l'image du RTC**, à sa mission ou au
> transport en commun de façon générale.

"Harm to RTC's image" is not a breach — it is subjective and unilateral. The LWG:

> Licences that can be revoked without any reason (even when OpenStreetMap has not
> violated the terms) are incompatible.

No perpetuity term anywhere, and *"Cette licence peut évoluer"* on top.

**2. In-product attribution OSM cannot give:**

> L'utilisateur doit indiquer la provenance et la date de la dernière mise à jour […]
> « Application, produit ou service, intégrant les Informations publiques du Réseau de
> transport de la Capitale, mises à jour le ___________. »

OSM can only offer a Contributors-page credit; substituting it must be granted.

**No portal fallback.** RTC has exactly one dataset on Données Québec (bus GTFS, CC-BY);
a search for àVélo / vélopartage / bikeshare returns zero. Ville de Québec has no
assessment either — the OSM wiki Canada page says *"Would need explicit permission from
the city."*

**Existing exposure.** `Contributors` already carries *"data from the Réseau de transport
de la Capitale, àVélo data updated on 2025-05-18"* with **no permission page behind it** —
unlike every neighbouring Québec entry (STM, STL, exo, Laval, Sherbrooke, Gatineau,
Lévis, MTQ, BIXI), all of which link an explicit grant. Someone treated the RTC licence as
self-sufficient. Frame the ask as *regularizing an existing use*; that is what worked for
BIXI.

**Contacts:** RTC has no open-data email (checked FR and EN raw HTML). Use the contact
form <https://www.rtcquebec.ca/service-la-clientele/pour-nous-joindre>, admin
418-627-2351. `Application.DonneesOuvertes@Ville.quebec.qc.ca` is the maintainer of record
for RTC's GTFS dataset (a Ville de Québec address). Unsettled: àVélo may be operated by
Capitale Mobilité and owned by RTC — address RTC and let them redirect.

## ❌ Mobi (Vancouver) — needs a written grant

**The feed and the operator's published agreement contradict each other.** The feed
declares `license_id: ODbL-1.0`. Vancouver Bike Share Inc.'s Data License Agreement —
linked live from <https://www.mobibikes.ca/en/system-data> ("This data may only be used in
accordance with our Data License Agreement") — says the opposite:

- **§1.1** grants "access, reproduce, analyze, copy and use" — *distribute*, *publish* and
  *sublicense* are absent from the grant.
- **§2.1(b)** prohibits them: *"host, stream, publish, distribute, sublicense, or sell the
  Data or any part thereof; provided, however, that you may include the Data as source
  material […] for non-commercial purposes only"*.
- **§2.1(c)/(e)** forbid access by means other than VBS's interface, and "data mining or
  other extraction methods".
- **§5.1/§5.2** put ownership in VBS — so Fifteen (`gbfs@fifteen.eu`, the feed host)
  cannot cure this; only VBS can.
- **§7.1** terminable at will; **§11.1** unilaterally amendable; **§7.2** — sections 2–7,
  9 and 10 *survive termination*, so the prohibitions outlive the licence.
- **§2.1(h)** requires separate written permission to use the trade names — so complying
  with ODbL attribution itself needs a second permission, constrained by §2.1(g) (no
  implied endorsement).

**Genuinely open question:** whether the DLA governs the GBFS feed at all. It is linked
only from the trip-CSV page; the feed is on Fifteen's domain with a contradicting licence
field; and the PDF still reads *"dba Mobi by Shaw Go"* (pre-2023 branding), so it may
simply never have been reconciled. That ambiguity is why this is *ask*, not *abandon*.

**No portal fallback today** — `opendata.vancouver.ca` has `bikeways`, `bike-racks` and
two bike-lane counters, but no bike share stations. **Fallback worth pursuing in
parallel:** OGL – Vancouver *is* LWG-approved (minutes 2025-08-11) and already on
`Contributors`, so asking the City to publish station locations sidesteps VBS entirely.

**Contact:** `info@mobibikes.ca` — the channel §8.1 itself designates. cc
`gbfs@fifteen.eu`.

## ⛔ Citi Bike (NYC) — incompatible; stop

Three independent grounds, any one fatal. Source:
<https://citibikenyc.com/data-sharing-policy> (licensor: **Lyft Bikes and Scooters, LLC**).

**1. The grant does not include redistribution.**

> Bikeshare hereby grants to you a non-exclusive, royalty-free, limited, perpetual license
> to access, reproduce, analyze, copy, modify, **distribute in your product or service**
> and use the Data for any lawful purpose.

OSM is not "your product or service" — it is a commons whose licence obliges you to pass
every recipient the full ODbL bundle.

**2. Sublicensing and commercial redistribution are prohibited by name.**

> Host, stream, publish, distribute, sublicense, or sell the Data as a stand-alone
> dataset; provided, however, you may include the Data as source material […] in
> analyses, reports, or studies published or distributed **for non-commercial purposes**

Contributing to OSM *is* sublicensing — OSMF sublicenses to every planet-file consumer.

**3. Revocable at will, which the Contributor Terms forbid.**

> Bikeshare may terminate this Agreement at any time and for any reason in its sole
> discretion. […] Sections 2 – 6 and 9-10 will survive termination.

against OSMF Contributor Terms:

> You hereby grant to OSMF a worldwide, royalty-free, non-exclusive, **perpetual,
> irrevocable** licence […]

You cannot grant an irrevocable licence over rights held at Lyft's sole discretion.
(Clause 1 says "perpetual" while clause 7 says terminable at will; assume the worse
reading — that contradiction is Lyft's to resolve.)

**No escape routes.** `gbfs.citibikenyc.com` discovery points at the same `gbfs.lyft.com`
URLs we consume, and `citibikenyc.com/system-data` states the data "is provided according
to the NYCBS Data Use Policy" — the feed we use *is* the feed that policy licenses. The
same clause 2(b) and the same licensor appear on Capital Bikeshare's and Bay Wheels'
agreements, so it is Lyft boilerplate, not a Citi Bike quirk. NYC Open Data does not
republish it — the Socrata record (`vsnr-94wk`) is an external-link asset with
`licenseId: null` that points back at citibikenyc.com.

**Precedent:** the Docomo Bike Share GBFS import was already **CC BY 4.0** — far more
permissive — and *still* required a permission letter (obtained 2023-11-20). A
no-sublicense, non-commercial, terminable-at-will licence cannot clear without one.

**Operational consequence:** this covers the MapRoulette challenges too. A task embeds
station coordinates and names, which is redistribution of the Data as a stand-alone
dataset on any reading. Recommend pausing Citi Bike output pending a grant — **operator
decision, not made here.**

**Contact:** `bike-data@lyft.com` (clause 8). The grant must name **Lyft Bikes and
Scooters, LLC** — clause 5 says it "owns all right, title, and interest in the Data". A
letter from "NYC Bike Share LLC" (the DOT contract counterparty) would be worthless.

---

## What to send

Use the LWG's own priority ladder — **CC0 first, licence-side wording second, signed
waiver last**:

1. **Best:** ask them to publish the feed under **CC0** and set `license_id` accordingly.
   Cleanest verdict on the OSMF list, no waiver, no ongoing obligation, and it fixes the
   feed for every consumer, not just us.
2. **If they use or want CC-BY 4.0:** ask them to add the LWG's two-sentence wording to
   their licence page — *"Section 2(a)(5)(B) of the CC BY 4.0 license is void. Attribution
   to a central list of sources via URL is sufficient to provide attribution in a
   'reasonable manner' in accordance with Section 3(a)(1) of the CC BY 4.0 license."*
3. **If they want a signed document:** the OSMF
   [CC BY 4.0 cover letter + waiver](https://osmfoundation.org/wiki/Licence/Waiver_and_Permission_Templates/Cover_letter_and_waiver_template_for_CC_BY_4.0),
   verbatim.
4. **Simplest for the municipal cases (Hamilton, Mobi-via-Vancouver):** get written
   confirmation that the GBFS feed is published under the already-approved municipal OGL.
   No new legal instrument needed.

**CC-BY 4.0 still needs a waiver.** The OSMF is explicit: *"all CC BY versions have
additional terms that make them incompatible with OpenStreetMap without explicit
waivers."* The hard blocker is the DRM clause (§2(a)(5)(B)), not attribution.

For bespoke grants (àVélo, Mobi, Citi Bike) use the
[Import/Getting permission](https://wiki.openstreetmap.org/wiki/Import/Getting_permission)
Template 3 language, and make sure the reply explicitly covers: **ODbL 1.0 by name**,
**commercial use**, **sublicensing**, **irrevocability**, and **Contributors-page
attribution in lieu of in-product credit**. Get a specific written answer that can be
posted publicly on the wiki.

## Process notes for the import proposal

- **`imports@openstreetmap.org` is retired** (banner confirmed; migration approved by
  vote 2023-06-22 → 2023-07-06, 45/5). The mandatory RFC goes to
  <https://community.openstreetmap.org/> with the `import` and `import-proposal` tags.
- Review period is **14 days *and* all concerns addressed** — a floor, not a timer.
- The wiki plan page must state: data source site, data licence, licence type in
  [SPDX format](https://spdx.org/licenses/), link to permission, OSM attribution URL, and
  `ODbL Compliance verified: yes`. Changeset tags must include `source:license`.
- The forum template's `## License` block needs three lines: that *you* checked ODbL
  compatibility, the named licence, and any waiver. **A feed with no licence field cannot
  fill line two** — which is precisely why the four unlicensed feeds need grants first.
- Escalation for anything ambiguous: `legal-questions@osmfoundation.org`. The OSMF's
  standing instruction on unassessed licence variants: *"Please do not import or otherwise
  use the data within OSM until the LWG determines that the license is compatible."*

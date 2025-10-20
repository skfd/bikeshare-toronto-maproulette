**DUPLICATE REF VALUE DETECTED**

This task shows ONE station that has a duplicate 'ref' value in OpenStreetMap. The error message shows how many total stations share this ref value.

**What's the problem?**
The ref={{address}} appears multiple times in OSM. Each bike share station MUST have a unique ref value.

**All duplicates are included with official GBFS data:**
- Look for ALL tasks with ref={{address}} - each shows one OSM station with this duplicate ref
- **Look for the task marked "OFFICIAL GBFS DATA"** - this shows the correct location/name from the bike share operator
- The GBFS task shows where ref={{address}} SHOULD be according to official data
- Compare each OSM duplicate with the official GBFS data to determine which (if any) is correct

**How to fix:**
1. **Find all related tasks**: Search for ref={{address}} to see:
   - All OSM duplicates (marked with "Duplicate ref" in error)
   - The official GBFS station (marked with "OFFICIAL GBFS DATA" in error)
2. **Compare with official data**:
   - Check if any OSM station matches the GBFS location and name
   - The one matching GBFS data is likely correct
3. **Fix the incorrect stations**:
   - If OSM station matches GBFS: Keep this one, it's correct
   - If OSM station doesn't match GBFS: Either update to correct ref or remove ref tag + add `fixme=verify correct ref value`
4. **Verify uniqueness**: When done, only ONE OSM station should have ref={{address}}

**Important notes:**
- The GBFS task shows the OFFICIAL bike share operator data - use this as your reference
- OSM element type/ID shown helps identify which station is which
- All stations with this duplicate ref are shown as separate tasks
- Fix all duplicates to ensure data quality


This station has a duplicate 'ref' value in OpenStreetMap. Multiple stations are using the same reference ID, which is incorrect.

**Data Quality Issue:** Duplicate ref={{address}}

Steps to fix:
1. Examine all stations listed with this ref value (check OSM ID in task properties)
2. Verify which station has the correct ref value (cross-reference with official bike share data)
3. For incorrectly tagged stations, either:
   - Update the ref tag to the correct value if you can determine it
   - Remove the ref tag if the correct value is unknown (add fixme=verify ref value)
4. Ensure only ONE station has each unique ref value
5. If you're unsure which is correct, add a note requesting verification from local mappers

**Important:** The 'ref' tag should match the official bike share station ID. Each station must have a unique ref value.


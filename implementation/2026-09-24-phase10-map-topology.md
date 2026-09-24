# Phase 10 — World Map topology presentation

The World Map now draws the 43 authored borders from `RegionCatalog` on top of its painted atlas. Eight inter-reach gates use brass links; selecting a region highlights each segment of the shortest route. Markers use I, II or III for catalog lane count until route numbers take their place, and the legend explains the glyphs. The Ark marker, selection panel, route timing, pan/zoom controls and detail action retain their existing behavior.

`implementation/scripts/preview-world-map.py` builds `implementation/reviews/phase10-map-graph.html` directly from the current catalog. The browser page is an offline composition review, not a Unity screenshot. It includes all thirty regions, their border lines, gate links and a tappable route selection. No live richness, controller, Collector or alert markers were invented: the current `region/state` response supplies the Ark region and its claim nodes, not world-wide status for all thirty regions.

Source checks: the parsed catalog has 30 regions, 43 symmetric edges and eight gates; stylesheet token and silent-drop checks pass; the generated review script passes JavaScript syntax validation. The new Unity `WorldMapViewTests` assertions for drawn edge counts and selected-route segments will run during the deferred Unity validation pass, along with portrait captures and touch review.

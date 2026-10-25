# Calgary Cycling Simulator — Stylized Game Architecture

Status: proposed for approval  
Implementation status: foundation implemented; visual polish and headset validation remain

## Product direction

Transform the existing Calgary VR Cycling Simulator into a polished, colorful, cozy indie cycling game while preserving real Calgary geography and real address-based cycling routes.

The visual priority is a cohesive stylized experience rather than architectural realism. Buildings, vegetation, rocks, terrain, and landmarks may be invented or exaggerated as long as the route geography, district identity, and ride experience remain credible.

The supplied visual reference establishes the art direction: faceted low-poly forms, warm sunlight, graphic shadows, saturated evergreen foliage, colorful flowers, simplified rocks, stylized water, oversized natural shapes, and clean readable silhouettes.

## Agreed decisions

- Use a hybrid Cesium plus stylized near-field environment.
- Keep Cesium and real route data as the geographic truth.
- Use real OpenRouteService road geometry for address-planned rides.
- Keep authored sample routes as designed showcase experiences.
- Use three themed zones:
  - Bow River / Peace Bridge riverfront.
  - Colorful abstract downtown.
  - Evergreen park and foothill-inspired scenic route.
- Use warm alpine-cozy stylization.
- Use an external stylized bicycle asset when available, with the generated bicycle retained as a fallback.
- Show a stylized bicycle cockpit rather than a full rider body.
- Use a diegetic cockpit dashboard and a focused premium route-planning panel.
- Preserve seated, auto-forward, comfort-first movement.
- Target Quest 3 as the visual baseline with a Quest 2-compatible fallback.
- Target stable 72 Hz first; allow higher Quest 3 quality only when frame pacing supports it.
- Build the environment from reusable, instanced kits with distance tiers and hard visibility budgets.
- Formalize palette, zone, route-dressing, and quality settings as data rather than scattering values through scripts.

## Existing technical foundation

The source project currently uses:

- Unity 6000.0.66f2.
- URP 17.0.4.
- Cesium for Unity 1.18.1.
- OpenXR 1.14.0 and Meta XR support.
- Unity Input System 1.11.2.
- Android/Quest build configuration.
- Runtime-generated bicycle, route overlay, vegetation, buildings, and world-space VR UI.
- `RoutePath` for local route sampling and distance queries.
- `BicycleMotor` for movement and steering.
- `RoutePlannerService` for geocoding and real cycling routes.
- `RideApp` as the runtime composition and state owner.
- `SceneryService` for Cesium terrain/building streaming and failure handling.

The current project has no authored bicycle model library, environment kit, material library, texture set, or polished visual asset pipeline. The visual overhaul must therefore introduce a clean asset seam without breaking the existing procedural fallback.

The first implementation slice now provides a runtime stylized environment builder, zone-specific route dressing, warm directional lighting, data-driven quality-profile support, and an external bicycle prefab seam. The generated bicycle and generated environment remain the fallback until authored assets are supplied.

## Target architecture

### Runtime composition

`RideApp` remains the runtime composition root and owns the high-level ride state. It should coordinate modules rather than contain the implementation details of visual generation.

The intended flow is:

```text
Route selection
      |
      v
Route data + zone definition + quality profile
      |
      v
Stylized environment generator
      |
      +--> route dressing
      +--> vegetation / rocks / flowers
      +--> water / terrain accents
      +--> stylized buildings / landmarks
      +--> lighting and palette setup
      |
      v
RideApp + BicycleMotor + XR cockpit
```

### Deep environment module

Introduce one deep environment-generation module behind a small interface. Its callers should provide route and presentation data without knowing how the world is assembled.

Conceptual interface:

```text
BuildEnvironment(route, zoneDefinition, paletteDefinition, qualityProfile)
ClearEnvironment()
```

The implementation may internally use several adapters for foliage, buildings, water, route ribbons, and Cesium blending. Those details should not leak into route planning, bicycle movement, or UI modules.

### Data definitions

Create data-driven definitions for:

- `StylizedZoneDefinition`
  - Zone identity.
  - Environment recipe.
  - Landmark placement rules.
  - Near-field radius.
  - Cesium fade distance.
  - Lighting profile.
- `PaletteDefinition`
  - Named colors for terrain, water, vegetation, buildings, flowers, route markers, and UI.
- `RouteDressingDefinition`
  - Route ribbon style.
  - Edge markers.
  - Arrow frequency.
  - Landmark and prop cadence.
- `QualityProfile`
  - Render scale.
  - LOD distances.
  - Maximum visible props.
  - Shadow settings.
  - Cesium screen-space error.
  - Optional Quest 3 enhancements.

These definitions should be ScriptableObjects or equivalent Unity assets so visual tuning does not require rewriting runtime logic.

## Geographic truth and Cesium strategy

Cesium remains responsible for geographic anchoring, terrain context, distant Calgary context, and alignment validation. It is not required to determine the visible near-field art direction.

The near field should be owned by stylized content around the selected route. Cesium should fade into the background beyond a configurable band, initially expected to be approximately 150–250 metres from the player. Fog, terrain shaping, vegetation, and controlled sightlines should hide the transition.

Address-planned routes must preserve returned OpenRouteService road geometry. The route path must not use smoothing that cuts corners or leaves the road network. Sample routes may use authored curves because they are designed demo experiences.

The final visual route representation should be integrated into the world as a painted path, stylized surface, edge stones, arrows, or small route markers. A floating GPS line should be avoided except as an accessibility/debug option.

## Three themed zones

### Bow River / Peace Bridge

Visual identity:

- Teal and blue-green water.
- Warm sandstone and ochre path surfaces.
- Evergreen trees, reeds, rocks, and oversized flowers.
- Peace Bridge-inspired curves and red/orange accent structures.
- Open sightlines and strong sunlight reflections.

Primary purpose: immediate visual showcase and comfortable introductory ride.

### Colorful downtown

Visual identity:

- Abstract tower silhouettes.
- Saturated facade blocks.
- Playful roof shapes and simplified windows.
- Warm coral, mustard, teal, and purple accents.
- Deliberately non-photorealistic skyline composition.

Primary purpose: demonstrate that the app can represent Calgary without requiring literal building replicas.

### Evergreen park / foothill-inspired route

Visual identity:

- Layered conifers.
- Faceted rocks and rolling terrain.
- Cool green shadows with warm sunlit planes.
- Purple and pink wildflowers.
- Distant foothill silhouettes supplied or validated by Cesium context.

Primary purpose: provide the longest and most scenic sample ride.

## Palette tokens

The first palette pass should use named tokens rather than isolated color literals.

| Family | Initial colors |
|---|---|
| Warm sunlight | `#FFD27A`, `#F3A65A` |
| Evergreen | `#315C3C`, `#6F9D3C` |
| River | `#2B7180`, `#65B7B0` |
| Rock and terrain | `#B87552`, `#D8B98B`, `#6B5860` |
| Flowers and accents | `#E94C91`, `#9B63D6`, `#F2D84B` |
| UI dark glass | `#10252B` |
| UI highlights | `#62D5CF`, `#A7E86F` |

These are starting tokens, not a locked final color grade. They should be tuned in headset under the actual Quest display and lighting conditions.

## Bicycle and first-person immersion

The bicycle should be integrated through a documented prefab contract so a supplied external asset can replace the generated fallback without rewriting movement or camera logic.

Required mount points:

- `CockpitRoot`
- `HandlebarRoot`
- `FrontWheel`
- `RearWheel`
- `CameraAnchor`

The visible first-person experience should prioritize:

- Attractive handlebars and brake levers.
- A clear stem and front-wheel context.
- Simple readable materials.
- Wheel rotation and subtle steering animation.
- No forced camera yaw or full animated rider body by default.

The current generated bicycle remains a development fallback until the external stylized bicycle asset is available and optimized.

## VR interface direction

### Cockpit dashboard

The dashboard should be a compact diegetic interface showing:

- Current speed.
- Distance travelled.
- Elapsed ride time.
- Current route name.
- Scenery readiness or streaming status.
- Pause, restart, recenter, and comfort controls.

It should use the dark-glass UI token, cyan/green highlights, rounded or softened panels, and restrained animation.

### Route planner

The route planner should become a premium “choose your ride” panel with:

- Large start and finish fields.
- Address suggestion cards.
- Selected-address confirmation.
- Distance and duration summary.
- A clear `Ride This Route` action.
- Safe error states for missing keys, invalid addresses, network failure, and unavailable route geometry.

The route planner should remain separate from the ride HUD so it does not clutter the active cycling view.

## Comfort and movement

Preserve the existing comfort-first model:

- Seated experience.
- Auto-forward movement.
- Gentle acceleration.
- Stable horizon.
- No forced camera rotation.
- Optional comfort mode.
- Pause on application focus loss.
- Recenter support.

Visual polish must not rely on camera shake, head bob, aggressive acceleration, or forced turning.

## Quest performance strategy

Quest 3 is the visual baseline; Quest 2 receives a reduced quality profile.

The first performance target is stable 72 Hz. The environment must use:

- Shared meshes and materials.
- GPU instancing where appropriate.
- LOD or billboard replacements.
- Route-distance spawning bands.
- Hard caps on visible foliage, rocks, flowers, and buildings.
- Limited real-time lights.
- One primary stylized sun.
- Restrained post-processing.
- Controlled Cesium tile budgets.
- Separate Quest 3 and Quest 2 quality profiles.

The current URP configuration is a useful starting point: render scale 0.85, 2x MSAA, HDR disabled, limited additional lights, and foveated-rendering support already present in the XR configuration.

## Implementation responsibility

Codex can implement:

- Environment-generation interfaces and runtime modules.
- ScriptableObject data definitions.
- Procedural route dressing.
- Stylized materials and shader scaffolding.
- Lighting and palette setup.
- LOD, instancing, visibility, and quality-profile logic.
- Bicycle prefab integration and fallback improvements.
- Dashboard and route-planner UI.
- Editor tools for regenerating and previewing zones.
- Route/geography alignment diagnostics.

Unity Editor or external asset work is still expected for:

- The final stylized bicycle mesh.
- Hero building and landmark meshes.
- High-quality foliage and flower assets.
- Texture painting or hand-authored material details.
- Final headset-based lighting and comfort tuning.

External assets must be optimized for Quest, licensed for use, and supplied with correct pivots, materials, and scale.

## Implementation sequence

1. Create this architecture and style foundation.
2. Build the stylized environment data model and generation seam.
3. Establish palette tokens, lighting, materials, and route dressing.
4. Add the bicycle prefab integration seam and improve the fallback bicycle.
5. Build the riverfront, downtown, and evergreen/foothill zones.
6. Redesign the dashboard and route planner.
7. Add Quest 3 and Quest 2 quality profiles.
8. Validate route alignment, comfort, frame pacing, and visual consistency in headset.

## Approval criteria

The visual overhaul is ready for implementation when this architecture is approved and the following criteria are accepted:

- The near-field world reads as one cohesive stylized game world.
- The three sample routes have distinct visual identities.
- Real address routes remain geographically correct even when buildings and scenery are stylized.
- The bicycle cockpit feels intentional and premium.
- The dashboard and route planner feel like part of the same game.
- The Cesium/stylized transition is not visually distracting.
- The experience remains comfortable and readable in VR.
- Quest 3 reaches the visual baseline at stable 72 Hz.
- Quest 2 has a documented reduced-quality fallback.
- Environment complexity is controlled by data-driven quality profiles rather than scattered constants.

## Current approval request

This document records the agreed direction and is awaiting approval before implementation begins. The main external dependency is the stylized bicycle asset; the project should proceed with the generated fallback until that asset is supplied.

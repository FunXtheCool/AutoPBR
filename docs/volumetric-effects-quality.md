# Volumetric effects quality (Genesis preview)

Notes on god rays, atmospheric sky LUT, and paired effects for believable outdoor lighting.

## Sun alignment (fixed)

Sun disc bloom, god-ray march target, and the sun billboard share **`PreviewSunScreenProjection`** (world position at `eye + (-lightDir) * 85`, projected with the live `view * proj`).

The sky pass now:

- Builds the camera **before** drawing the atmosphere.
- Samples the sky LUT by **per-pixel view ray** (not raw screen UV).
- Places additive bloom with an **aspect-correct disc** at `uSunUv`.

## Roadmap (follow-ups)

Each phase is a self-contained slice. Complete P0 before P1; later phases can overlap once dependencies are met.

---

### P0 — Alignment & stability

**Goal:** Sun bloom, god rays, and billboard stay locked; regressions are caught in CI.

| # | Task | Owner files | Done |
|---|------|-------------|------|
| 0.1 | Shared sun screen projection (billboard / sky / god rays) | `PreviewSunScreenProjection.cs` | [x] |
| 0.2 | Sky LUT sampled by per-pixel view ray | `atmo_sky.frag`, `PassScene.cs` | [x] |
| 0.3 | Aspect-correct sun disc & god-ray cone | `atmo_sky.frag`, `genesis_godrays.frag` | [x] |
| 0.4 | **Debug overlay:** dev-only crosshair + disc at projected `uSunUv` | `OpenGlPreviewBackend.Debug.cs`, toggle in settings | [x] |
| 0.5 | **Golden projection test:** fixed eye/view/proj/yaw/pitch → expected `sunUv` ± ε | `PreviewSunScreenProjectionTests.cs` | [x] |
| 0.6 | **Regression screenshot hook** (optional): capture preview with known light pose | `PreviewVolumetricRegressionFixtures.cs`, `PreviewSunScreenProjectionRegressionTests.cs` | [x] |

**Exit criteria:** Overlay matches billboard at all aspect ratios and orbit angles; unit test green.

#### P0.6 — Regression fixtures & manual sign-off

CI golden tests live in `tests/AutoPBR.App.Tests/PreviewVolumetricRegressionFixtures.cs` and
`PreviewSunScreenProjectionRegressionTests.cs`. Each fixture fixes camera eye, light yaw/pitch,
aspect, and cone scale; tests assert sun UV, disc/cone radii, and a stable fingerprint.

**Refresh golden values** (after intentional projection changes):

```bash
dotnet test tests/AutoPBR.App.Tests/AutoPBR.App.Tests.csproj --filter PrintGoldenProjectionValues
```

Remove `Skip` on `PreviewVolumetricRegressionGoldenCaptureTests.PrintGoldenProjectionValues` first.

**Manual visual sign-off** (before release / after sky or volume shader edits):

| Check | Fixture IDs | Pass criteria |
|-------|-------------|---------------|
| Sun alignment | `default-day-16x9`, `orbit-45-16x9` | Debug overlay crosshair on sun disc; billboard overlaps |
| Aspect ratios | `aspect-21x9`, `aspect-4x3` | Sun stays in viewport; no stretched disc |
| Day/night cycle | `noon-12h`, `sunset-18h`, `midnight-0h` | Noon = blue sky; sunset = warm horizon; midnight = dark + stars |
| God-ray cone | `cone-wide` | Shafts wider than default; no full-screen white bloom |
| Grey dome | all + clouds on | No camera-attached grey hemisphere |
| Volume pairing | default + clouds + god rays | Rays attenuate behind clouds; aerial haze on distant geometry |

Pose parameters: call `PreviewVolumetricRegressionFixtures.ManualCaptureChecklist()` from a test or read fixture IDs in the test file.

---

### P1 — God-ray quality (screen-space)

**Goal:** Sharper, cheaper shafts that respect occluders; user can tune cone width.

| # | Task | Owner files | Done |
|---|------|-------------|------|
| 1.1 | **Depth-aware radial blur** — replace 64-step march with sun-UV radial samples + depth weights | `genesis_godrays.frag` | [x] |
| 1.2 | **Half-res render** of god-ray pass | `GlColorRenderTarget`, `GodRays.cs` | [x] |
| 1.3 | **Bilateral / depth upsample** to full res | `genesis_godrays_upsample.frag` | [x] |
| 1.4 | **Occluder weighting** — attenuate where depth discontinuity or low sky luminance | god-ray frag | [x] |
| 1.5 | **Temporal reprojection** — history buffer + clamp (camera motion) | `OpenGlPreviewBackend.GodRays.cs`, history FBO | [x] |
| 1.6 | **UI:** `GodRayConeScale` (shaft width) + wire to `PreviewSunScreenProjection` | settings, VM, `MainWindow.axaml` | [x] |
| 1.7 | Tune defaults after 1.1–1.3 (strength / density / decay) | `PreviewRenderSettings` | [x] |

**Exit criteria:** Visible shafts at 0.35 strength with sun behind subject; ≤ half current GPU cost at 1080p preview.

**Depends on:** P0 complete.

---

### P2 — Atmosphere & pairing

**Goal:** Rays and sky feel integrated with clouds, distance, and shadows.

| # | Task | Owner files | Done |
|---|------|-------------|------|
| 2.1 | **Cloud density attenuates god rays** — sample cloud march or precomputed density along shaft | `godray_integration.glsl`, god-ray blur | [x] |
| 2.2 | **Height fog / aerial perspective** — distance-based scatter tint on geometry (not just IBL) | `genesis.frag` (`uAerialFogStrength`) | [x] |
| 2.3 | **Epipolar god-ray sampling** — march along epipolar lines through sun | `genesis_godrays.frag` (epipolar T) | [x] |
| 2.4 | **Shadow-map-aware shafts** — multiply ray energy by directional shadow visibility | shadow map in god-ray blur | [x] |
| 2.5 | **Sun-only bloom audit** — confirm LUT knee doesn’t fight `SunDiscStrength`; document tuning | `atmo_skyview.frag`, `atmo_sky.frag` | [x] |
| 2.6 | **Silver lining** on cloud edges toward sun (optional polish) | `volumetric_clouds.glsl` | [x] |

**Exit criteria:** Rays dim behind clouds and in shadow; distant terrain reads hazy; no full-sky white blowout at default settings.

**Depends on:** P1.1 recommended (shared sun UV + depth pipeline).

---

### P3 — Physical volumetrics

**Goal:** Single participating-medium model for fog, clouds, and light shafts.

| # | Task | Owner files | Done |
|---|------|-------------|------|
| 3.1 | **Design doc:** froxel grid vs ray-marched slab (resolution, cost, ANGLE/GLES) | `docs/volumetric-froxels.md` | [x] |
| 3.2 | **Froxel or slab volume** — inject / accumulate sun in-scatter | `OpenGlPreviewBackend.Volume.cs`, `genesis_volume_*.frag` | [x] |
| 3.3 | **Mie phase** along view toward sun (match `atmosphere.glsl` g=0.76) | `genesis_volume_integrate.frag` | [x] |
| 3.4 | **Unify clouds + rays** into one medium (or shared density texture) | `volumetric_medium.glsl`, unified post composite | [x] |
| 3.5 | **Noise + temporal accumulation** on volume (dither + TAA-style clamp) | froxel jitter + god-ray history upsample | [x] |
| 3.6 | **Remove legacy screen-space god rays** once parity reached | volume-only path; `genesis_godrays.frag` deleted | [x] |

**Exit criteria:** One toggle drives coherent outdoor volumetrics; shafts stable under orbit; acceptable frame time on integrated GPU.

**Depends on:** P2.1, P2.2 strongly recommended.

---

### Suggested execution order

```
P0.4 → P0.5 → P1.1 → P1.6 → P1.2 → P1.3 → P1.4 → P2.1 → P2.2 → P2.4 → P1.5 → P2.3 → P3.*
```

Quick wins first: debug overlay (P0.4), projection test (P0.5), radial blur (P1.1), cone scale UI (P1.6).

---

### P0 — Alignment & stability (summary table)

| Item | Status | Notes |
|------|--------|-------|
| Shared sun screen projection | Done | `PreviewSunScreenProjection.cs` |
| Sky LUT by view direction | Done | `atmo_sky.frag` |
| Aspect-correct disc & cone | Done | sky + god rays |
| Debug overlay | Done | P0.4 — `ShowSunProjectionDebug` |
| Projection unit test | Done | P0.5 — `PreviewSunScreenProjectionTests.cs` |

## Current pipeline

1. **Sky-view LUT** (`atmo_skyview.frag`) — precomputed in-scatter from sun direction, turbidity, and exposure.
2. **Sky composite** (`atmo_sky.frag`) — full-screen LUT sample + optional sun-disc bloom.
3. **God rays** — froxel inject + Mie integrate → half-res history → bilateral/temporal upsample → additive composite. Quality preset drives froxel resolution, slice count, cloud detail, and temporal weights. Unified volumetric clouds share the froxel density when both are enabled.

## Sky LUT bloom (tuning)

White skies usually come from the product of:

- Large scatter coefficients × linear sun intensity × transmittance near the sun.
- Additive sun disc on the composite pass.
- God rays sampling the same bright pixels (now avoided via procedural sun emitter).

**User controls (Render tab → Atmosphere LUT):**

| Control | Role |
|--------|------|
| Sky exposure | Master LUT brightness; lower to kill bloom |
| Sun intensity | Scattering energy (sqrt-scaled in shader) |
| Sun disc bloom | Additive glare around the disc only |
| Turbidity / horizon falloff | Haze and horizon rolloff |

Shader-side: soft knee compression before sRGB encode on LUT build and sky draw.

## God-ray improvements (roadmap)

### Near term (screen-space)

- **Depth-aware radial blur** — single pass from sun UV with depth weights (faster than 64-step march).
- **Bilateral / depth upsample** — render rays at half-res, upsample with depth edge preservation.
- **Occluder mask** — use scene luminance + depth discontinuities to weight shafts (trees, buildings).
- **Temporal reprojection** — stabilize flicker when the camera moves (jittered march + history clamp).

### Medium term (quality)

- **Epipolar sampling** — march along epipolar lines through the sun for fewer samples.
- **Phase function** — Henyey–Greenstein along view toward sun (match atmosphere Mie).
- **Noise + TAA** — dither march steps; accumulate over frames.

### Long term (physical)

- **Froxel or ray-marched volumetrics** — single medium for fog + god rays + clouds.
- **Shadow map sampling** — light shafts only where cascades are lit.
- **Cloud-aware shafts** — attenuate march inside cloud density (pairs with volumetric clouds).

## Effects that pair well

| Effect | Why it helps |
|--------|----------------|
| **Atmospheric sky LUT** | Consistent sun color and horizon; god rays should not re-sample blown sky |
| **Aerial perspective / height fog** | Darkens distant geometry so shafts read in depth |
| **Volumetric clouds** | Occludes and tints rays; silver lining at cloud edges |
| **Controlled sun disc bloom** | Separate from scatter intensity; avoids washing the whole sky |
| **Exposure / tone mapping** | Global headroom before additive passes |
| **Contact shadows + cascades** | Grounding; shafts need dark occluders to be visible |
| **IBL from same LUT** | Materials and background share the same sun |

## Quality presets (P4 — performance & cleanup)

| Preset | Froxel divisor | Slices | Cloud quality | Temporal |
|--------|----------------|--------|---------------|----------|
| Low | 8 (min 24 px) | 12 | 0 | off |
| Medium (default) | 4 (min 32 px) | 20 | 1 | volume 0.35 / upsample 0.45 |
| High | 3 (min 48 px) | 24 | 2 | volume 0.42 / upsample 0.55 |

**UI:** Render tab → Volumetric effects — god rays toggle, clouds toggle, quality combo, strength slider.

**Profiling:** `LogVolumetricTiming` logs inject/integrate ms when debug mode is on (exceeds documented budget).

Legacy screen-space radial blur (`genesis_godrays.frag`) removed; volume path is the only god-ray implementation.

## Recommended defaults for preview

- Atmospheric sky on; **sky exposure ~0.85**, **sun intensity ~10**, **sun disc ~0.35**.
- God rays on (medium quality); **strength ~0.45** with sun behind or beside the subject.
- Enable clouds for occlusion; lower cloud density if rays compete with cloud brightness.

## Polish (P5)

| # | Task | Status |
|---|------|--------|
| 5.1 | Auto time-of-day animation (`AnimateTimeOfDay`, `TimeOfDaySpeed`) | [x] |
| 5.2 | Explicit moon UV (`PreviewSunScreenProjection.ComputeMoon`, debug overlay) | [x] |
| 5.3 | i18n for atmosphere / volumetric / time-of-day strings (9 locales) | [x] |
| 5.4 | GPU framebuffer fingerprint hook (`CapturePreviewFingerprint`, debug log) | [x] |
| 5.5 | Cascaded shadow sampling in volume inject (near + far maps) | [x] |

**GPU regression:** enable debug mode, open 3D preview — diagnostics log `Frame fingerprint` every ~2 s. Golden projection + moon UV tests remain the CI gate; remove `Skip` on `PrintGoldenProjectionValues` to refresh CPU goldens.

## Anti-patterns

- Sampling scene color at the sun UV for ray energy when the sky LUT is overexposed.
- Full-sky radial blur without a sun cone (reads as global bloom).
- High sun intensity + high god-ray strength + high sun disc (triple blowout).

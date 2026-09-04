# Spindle Optimization Modes

The operator selects one of five modes from the main form. All modes use the same calibrated measurement and analysis stages through defect-patch analysis.

## Shared analysis stages

```text
Apply calibration
Process and preprocess 36 concatenated frames
Build profiles
Build rotating point cloud
Fit centerline
Build radius field
Bin radius field
Analyze ovality
Analyze radius defects
Analyze defect patches
```

## 1. Recovery Optimized 

This mode first generates four seed fixed spindle axes when enabled: current baseline, penalty-search best tested, largest inscribed cylinder, and largest inscribed oval. It then performs a bounded metrics-only direct veneer-recovery refinement around those seeds. The refined position and all seed positions are rendered with the full physical circular rotary-lathe virtual peel, and the exact rendered results select the winner by:

1. more usable full 50-inch sheets;
2. more usable veneer length;
3. fewer rejected full sheets;
4. lower severe-wane area;
5. lower total-wane area;
6. lower spindle movement on an exact tie.

Penalty/risk values remain diagnostics and can generate one seed, but they do not choose the final winner. The direct refinement searches only head/tail X/Y within commissioned machine limits. It does not generate a rotation command.

Default direct-refinement settings are a 2 mm local radius, 2 mm step, and a 500-position cap. The search uses the binned measured surface without allocating display images; the selected refined candidate is then re-evaluated by the full point-cloud peel renderer before final comparison.

## 2. Penalty-Scored Optimizer(default production strategy)

This is the existing optimizer. Adaptive weights, wane balance, defect patches, movement cost, guardrails, and the final risk score may influence the selected head/tail spindle position.

## 3. Largest Inscribed Cylinder

This mode performs a geometry-only coarse-to-fine search. It selects the head/tail spindle axis that maximizes the minimum circular radius contained inside the measured log for the full analyzed length.

- Each radius bin uses its minimum measured radius when available.
- A configurable safety margin is subtracted.
- Missing angular bins are filled conservatively.
- Machine travel is a hard feasibility limit.
- Defect and risk penalties do not influence the position.

The selected axis is then evaluated with the normal fixed-axis circular virtual peel.

## 4. Largest Inscribed Oval

This mode searches the same feasible spindle axes, then finds the largest centered ellipse that remains inside the full measured envelope. It varies ellipse axis ratio and orientation and maximizes ellipse area.

The oval is an axis-selection model. A conventional veneer lathe with a fixed knife and fixed spindle axis still peels circular paths; it cannot peel the ellipse perimeter. Therefore the final recovery estimate is generated with the same physical circular virtual-peel model at the oval-selected spindle position.

## 5. Compare Cylinder vs Oval

This mode independently computes both geometry candidates and creates both virtual peels. The final machine position is selected in this order:

1. more usable full 50-inch sheets;
2. more usable veneer length;
3. fewer rejected full sheets;
4. lower severe-wane area;
5. lower total-wane area;
6. larger guaranteed circular radius, then less movement, if the peels remain tied.

The selected recommendation is written to the normal final spindle output. Both comparison images are retained:

```text
Overlays/largest_inscribed_cylinder_peel.png
Overlays/largest_inscribed_oval_peel.png
Overlays/direct_recovery_refinement_peel.png  # direct-recovery candidate, when enabled
Overlays/unwrapped_peel_view.png               # selected winner
```

## Sheet usability

A full sheet defaults to 50 inches. It is counted as usable when:

- wood coverage is at least `PeelView.MinimumUsableSheetCoveragePercent` (default 98%); and
- severe-wane area is no more than `PeelView.MaximumUsableSheetSevereWanePercent` (default 0.5%).

Partial final sheets are displayed but are not counted as usable full sheets.

## Geometry settings

`InscribedGeometry` in `appsettings.json` controls angular resolution, required coverage, safety margin, oval axis-ratio search, orientation step, and tie tolerance. Geometry modes do not read adaptive scoring weights or final penalty scores when choosing a spindle position.

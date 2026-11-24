# Community UX redesign

## Player goals

Find actionable risks quickly, recognize the people involved, and reach assignment or inspection without hunting through unrelated pages. Keep the existing RimWorld visual vocabulary and module boundaries.

## Implemented

- Community / Services / Operations navigation groups and a narrower sidebar at smaller effective resolutions. Existing module availability and page IDs are retained.
- Overview metrics precede a compact attention queue. The queue shows three exceptions, with an expansion control and links to the owning service. Vacant professions and recent inactivity alone no longer create warnings. Module-specific health checks and unmet-need checks remain authoritative. Attention cache is invalidated when the colony changes.
- People & activity distinguishes active community duty from role breadth; filters show all holders, duty holders, or people with multiple modules. The latter is a review suggestion, not a claim that a sleeping pawn is busy.
- Portraits augment pawn names in shared tables and family cards. Names and existing selection behavior remain available.
- Professions defaults to People. Career selection no longer overflows horizontally. Compact tier cards open a scrollable role inspector with qualification prerequisites, XP requirements, candidate eligibility and checked assignment/removal. The original prerequisite graph remains an explicit alternative for complex branches. Role directory includes optional specialties.
- Shared contextual staffing controls on Education, Care, Sciences, Treasury, Military, Agriculture, Prisoners and Works use the existing assignment qualification checks.
- Care prioritizes bleeding and tending, distinguishes infant assistance from generic downed status, and does not color names red solely for being downed. The all-resident health grid is collapsed initially. Care staff cards list only Care roles.
- Treasury summary cards show actual community balance, earned wages pending and days until payday. Ownership visualization explains claims versus physical piles. The UI does not extrapolate a runway or promise future income from incomplete data.
- Education explicitly labels infants as below school age in the pupil roster. Existing enrollment and lesson scheduling behavior is retained.
- Agriculture visualizes reserve versus target and explains the absence of automatic targets without promising that eligible prey exist.
- Sciences uses neutral blue for skill and project progress, rather than treating low progress as poor health.
- Military shows equipment and availability counts, warns about unsuitable weapons on Overview, uses shared tab styling, explains placement cancellation, and shows squad member portraits.
- Establishments fixes the overlapping empty-state coordinates and adds a visible premises / staffing setup checklist. Existing customer, hours, finance and premises controls remain available.
- Works and Prisoners expose specialty explanations inline instead of showing empty section bodies.
- Assembly postponement is on its own measured action row instead of a clipped button inside the date banner.
- New interface text is in the framework's English keyed localization file. No save fields, package IDs, qualification thresholds, payroll rules, autonomous job rules or persistent models were changed.

## Deliberate adaptation of the concept boards

The concept images are visual direction, not screenshots of this implementation. Role details use a native scrollable dialog instead of a permanent inspector column, preserving room on smaller displays. Detailed family management continues in the existing authoritative Families window. The overview uses existing health checks rather than fabricated severity estimates. No speculative event dates, financial forecasts or health diagnoses were added.

Further concept-level extensions not implemented here: an embedded family detail workspace, a unified cross-module event timeline, a new class schedule editor, facility map thumbnails, a complete venue setup wizard, and a new patient inspector. Existing native workflows remain available for those operations. These should not be described as completed by this PR.

## Validation and acceptance

Build all eleven assemblies for RimWorld 1.7 and 1.6. Run `scripts/Test.ps1` in a normal checkout with game references and test-host IPC available. This environment can compile using cached references, but the VSTest host failed to connect after 90 seconds: no passing test-run claim is made.

Before merging, inspect actual gameplay at 1280×720, 1920×1080, and larger UI scaling; use long translated labels and a large colony. Check:

1. All navigation destinations remain reachable, including with modules disabled.
2. Career selection, search, optional graph, role details and candidate lists do not clip; unqualified candidates cannot be assigned, and removal does not erase qualifications.
3. An infant at full health is not visually presented as an urgent injury merely because it is downed; bleeding and tending remain visible.
4. A sleeping pawn with several roles is marked for role review rather than called busy; duty filters reflect actual community duty.
5. Empty establishments, Works specialties, Prisoners specialties, Assembly postponement and long button labels fit.
6. Treasury rendering causes no transfers or payroll changes; pending wages and ownership claims retain their meanings.
7. Attention updates after loading another colony, expanding/collapsing retains useful layout, and newly unarmed soldiers produce a navigable service warning.

No live-game screenshot, usability-test result or save/load compatibility claim is implied by compilation.

# Dive-only gameplay — 2026-09-28

Ultimates are removed from active gameplay. Both native controller surfaces now expose Dive only: racket controller and preview controller. Ultimate selection is gone. Existing gameplay/menu/pause/Next controls remain unchanged.

The runtime rejects ultimate selection/arming, stops player and AI charge accumulation, gates special shots and cinematics, and hides both HUD meters from creation onward. Old native ultimate commands are ignored. Archived ultimate types and capture helpers remain for source compatibility, behind `TennisAbilities.UltimatesEnabled == false`; they are not an active feature. Ordinary high lobs, overheads, perfect hits and Dive are preserved.

Scoped alongside another agent's venue work; no venue assets modified. No device install or Unity iOS framework export is implied by source changes. A phone deployment must rebuild the Unity framework as well as the native controller.

Validation: Unity `TennisAbilitiesPlayTests.SelectionDiveAndConfirmedContactRules` passed (real dive contact, cooldown and rejection of charged ultimate activation); native `TennisMenuSnapshotTests.testUltimateAndDiveControls` passed. Captured racket controller visually reviewed with Dive only. `git diff --check` passed. Existing ultimate capture experiments are historical and are not supported active gameplay tests.

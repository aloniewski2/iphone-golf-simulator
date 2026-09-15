# Contributing

1. Branch from `testing` using `feature/<short-name>`, `fix/<short-name>`, or `chore/<short-name>`.
2. Keep changes focused and include tests for deterministic swing or shot behavior.
3. Regenerate `GolfArcade.xcodeproj` after editing `project.yml`.
4. Open a pull request into `testing`; all required checks must pass before merge.
5. Promote tested releases from `testing` to `main` by pull request. Pull requests to `main` from every other branch are rejected by the `main-source-policy` check.

Direct pushes, force pushes, and branch deletion are disabled for `main` and `testing`, including for repository administrators.

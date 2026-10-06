# FacilityManagementService

ASP.NET Core backend (.NET, Clean Architecture). Architecture decisions live in `docs/adr/` — read the relevant ADR before changing anything it covers.

## Git workflow

`master` (and `main`) is protected. Never commit or push directly to it; all work goes through a branch and a pull request.

Every piece of work maps to a GitHub issue in `gyback/facility-management`. Issue `#16` is referred to as `FM-16`. If there is no issue for the work yet, ask before creating one.

- **Branch:** `<type>/FM-<issue>-<short-kebab-slug>`, branched from an up-to-date `master`.
  `type` is one of `feature`, `fix`, `chore`, `docs`, `refactor`, `test`.
  Example: `feature/FM-16-github-actions-workflow`
- **Commits:** subject starts with the issue key in brackets, imperative mood.
  Example: `[FM-16] Add build and deploy workflow`
- **Pull requests:** title starts with the issue key in brackets; target `master`; the body includes `Closes #<issue>` so the issue is linked and closed on merge.
  Example: `gh pr create --base master --title "[FM-16] Add GitHub Actions build and deploy workflow" --body "Closes #16 ..."`

These rules are enforced for Claude sessions by `.claude/hooks/git-workflow-guard.js` (a PreToolUse hook registered in `.claude/settings.json`). If it blocks a command, fix the branch name/message rather than working around the hook.

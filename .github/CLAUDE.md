# CLAUDE.md
Read and follow .github/copilot-instructions.md — it is the single source of project rules.

## Workflow
- Work on a feature branch; never commit to main.
- Before finishing: `dotnet build`, `dotnet test`, and in src/web `npm run build` and `npm test` must pass.
- Keep PRs small (one feature per PR) so Copilot review is effective.
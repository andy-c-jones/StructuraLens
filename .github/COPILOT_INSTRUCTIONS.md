# Copilot / Contribution Guidelines

- Work on feature branches named `feature/<short-description>` or `fix/<short-description>`.
- Open a pull request against `main` for all changes.
- Use Conventional Commits for PR titles and commit messages. PRs will be squashed on merge, so the PR title becomes the release note entry.

Conventional Commit examples:
- feat: add new analysis rule for controller coupling
- fix: handle null reference in parser when project file missing
- chore: update dependencies
- docs: clarify README examples

Ensure the PR title follows Conventional Commits (e.g., `feat: add X`) so semantic-release can parse it correctly.
# Repository Instructions

- Follow the approved implementation scope and the frozen documents in `docs/`. Ideas or roadmap entries do not authorize implementation.
- Keep this repository a fresh public generation. Do not import historical Analyzer code, data, or Git history unless a later approved scope explicitly calls for specific reusable material.
- Keep tracked files and local commit metadata public-safe. Use synthetic examples; never add credentials, personal data, private infrastructure details, real Redmine content, or private repository material.
- Keep the Analyzer and RedmineMcp bootstrap minimal. RedmineMcp stdout is reserved for MCP protocol messages; ordinary logs go to stderr.
- Run the documented `dotnet` restore, build, and test commands for changes that affect the solution.
- Never push or otherwise write to a Git remote. Remote operations are performed manually by the repository owner.

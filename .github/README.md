# GitHub Configuration

This is a **local workstation tool**, not a web service or distributed application. The CI/CD setup is intentionally minimal.

## What's Here

### `workflows/ci-simple.yml`
- **Purpose:** Sanity check that code builds and tests pass
- **When:** Runs on push to main
- **Why:** Catches breaking changes before you forget about them
- **Note:** You can also just run `dotnet test` locally instead

### `dependabot.yml`
- **Purpose:** Keeps NuGet packages updated monthly
- **Why:** Security updates and bug fixes
- **Alternative:** Run `dotnet list package --outdated` manually

### `copilot-instructions.md`
- **Purpose:** GitHub Copilot context about this project
- **Why:** Helps AI understand the codebase structure

## What's NOT Here (Intentionally)

We removed enterprise-grade CI/CD because this tool is:
- ❌ Not a web service or API
- ❌ Not distributed to end users
- ❌ Not security-critical (local use only)
- ❌ Not performance-critical
- ❌ Not multi-platform (you run it on your workstation)

**Removed workflows:**
- Multi-OS builds (Ubuntu/Windows/macOS)
- Security scanning (CodeQL, OWASP, Trivy, secret detection)
- Release automation (binaries, Docker images)
- PR validation (coverage deltas, semantic commits)
- Performance regression testing
- SBOM generation

## For Maintainers

**Before committing:**
```bash
dotnet build
dotnet test
```

**To check for outdated packages:**
```bash
dotnet list package --outdated
```

**To update packages:**
```bash
dotnet add package <PackageName>
# or just accept Dependabot PRs
```

That's it. Keep it simple.

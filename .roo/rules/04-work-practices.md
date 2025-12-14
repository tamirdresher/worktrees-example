# Work Practices

## Terminal Commands

**Important**: All terminal commands must use **git bash** or **powershell** instead of cmd.

Example:
```bash
# ✅ Use this
bash script.sh

# ❌ NOT this
cmd /c "command"
```

## Documentation Organization

### Documentation Structure

All documentation must be placed in the appropriate subfolder within the `docs/` directory. **Never create documentation files in the root `docs/` folder.**

### Documentation Categories

| Folder | Purpose | Examples |
|--------|---------|----------|
| `docs/features/` | Feature specifications and implementations | `task-processing-feature.md`, `ai-integration-feature.md` |
| `docs/architecture/` | System architecture and design decisions | Architecture diagrams, design patterns |
| `docs/guides/` | How-to guides and tutorials | Setup guides, usage instructions |
| `docs/reference/` | API references and technical specs | API documentation, configuration references |
| `docs/fixes/` | Bug fixes and issue resolutions | Specific fix documentation |
| `docs/diagrams/` | Architecture and flow diagrams | Mermaid diagrams, visual documentation |
| `docs/archive/` | Outdated or deprecated documentation | Old implementations, deprecated features |

### Documentation Guidelines

**✅ DO:**
- Place feature documentation in `docs/features/`
- Keep documentation concise and focused
- Consolidate related information into single documents
- Use clear, descriptive file names (e.g., `task-processing-feature.md`)
- Include code examples and usage patterns
- Link to related documentation

**❌ DON'T:**
- Create documentation files directly in `docs/` root folder
- Create multiple documents for the same feature
- Create separate planning/implementation/architecture docs unless absolutely necessary
- Leave redundant or duplicate documentation files
- Create overly detailed documentation that duplicates code comments

### When Creating Documentation

1. **Choose the right folder** based on the documentation type
2. **Check for existing documentation** that could be updated instead
3. **Use a single, well-organized document** rather than multiple fragmented files
4. **Include practical examples** showing how to use the feature
5. **Clean up** any temporary planning or implementation documents after completion

### Example: Feature Documentation

For a new feature implementation, create a **single consolidated document** like:

```
docs/features/my-feature-name.md
```

Instead of multiple files like:
```
❌ docs/my-feature-plan.md
❌ docs/my-feature-implementation.md
❌ docs/diagrams/my-feature-architecture.md
❌ docs/MY-FEATURE-SUMMARY.md
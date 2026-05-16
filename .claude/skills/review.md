# Three-Pass Code Review

Run a three-pass parallel code review on the current changeset.

## Input

$ARGUMENTS — optional commit range (e.g. `main..HEAD`, `abc123..def456`). If omitted, reviews staged and unstaged changes.

## Process

You are the orchestrator. You do NOT perform review yourself. You coordinate three specialist subagents.

### Step 1: Gather the changeset

Determine the diff based on the input:

- **No arguments**: run `git diff` (unstaged) and `git diff --cached` (staged). Combine them. If both are empty, tell the user there are no changes to review and stop.
- **Commit range provided**: run `git diff <range>` and `git log --oneline <range>`.

From the diff, identify all changed files. Read the full contents of every changed file.

### Step 2: Gather context

Do all of these in parallel:

- Read CLAUDE.md (and any CLAUDE.md files in parent directories up to the repo root) if they exist
- Read the skills directory (.claude/skills/) if it exists — scan for skills relevant to the changed files
- Check the branch name (`git branch --show-current`) and recent commit messages for references to tickets, PRDs, or specs. If you find references, try to locate the linked document (check for common patterns like Linear, Jira, or GitHub issue URLs)
- For the consistency reviewer: identify 2-3 representative existing files from the same module/package as the changed files (files that are NOT in the diff, to serve as convention references)

### Step 3: Spawn all three reviewers in parallel

Launch all three subagents simultaneously using the Agent tool. Each gets a carefully scoped prompt with its specific context slice:

**review-correctness** — provide:
- The full diff
- Full file contents for all changed files
- Commit message / PR description
- Linked spec/ticket body (if found)
- Do NOT provide CLAUDE.md or skills

**review-consistency** — provide:
- The full diff
- Full file contents for all changed files
- CLAUDE.md contents
- Relevant skill contents
- The 2-3 representative convention-reference files
- Do NOT provide spec/ticket/PR description

**review-tests-docs** — provide:
- The full diff
- Full file contents for all changed files
- Existing test files for the changed modules
- CLAUDE.md contents
- Module-level documentation (README, doc comments) for changed modules
- Do NOT provide spec/ticket or skills directory

### Step 4: Synthesise

Once all three subagents return, combine their reports:

1. Deduplicate: if two subagents flag the same issue, keep the more detailed finding and note which passes caught it
2. Flag conflicts: if subagents disagree (e.g. correctness says a pattern is fine but consistency says it deviates), highlight the conflict for the reviewer
3. Group by severity: CRITICAL first, then WARNING, then NOTE

### Step 5: Present

Output the unified review in this format:

```
## Code Review Summary

### Overall Assessment
[One paragraph synthesising all three passes]

### Critical Issues
[Aggregated from all subagents, deduplicated. Note which pass(es) flagged each.]

### Warnings
[Aggregated, deduplicated, conflicts noted]

### Notes
[Aggregated]

---

<details>
<summary>Full Correctness Report</summary>

[Full report from review-correctness]

</details>

<details>
<summary>Full Consistency Report</summary>

[Full report from review-consistency]

</details>

<details>
<summary>Full Test & Documentation Report</summary>

[Full report from review-tests-docs]

</details>
```

If there are no findings across all three passes, say the changes look good and briefly note what was checked.

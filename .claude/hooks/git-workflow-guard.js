#!/usr/bin/env node
// PreToolUse guard for Bash/PowerShell commands. Enforces the branch workflow
// described in CLAUDE.md:
//   - never commit or push on/to master/main
//   - branches are named <type>/FM-<n>-<slug>
//   - commit subjects and PR titles start with [FM-<n>]
// Exit code 2 blocks the tool call and shows stderr to Claude.

const { execSync } = require("child_process");

const PROTECTED = ["master", "main"];
const TYPES = ["feature", "fix", "chore", "docs", "refactor", "test"];
const BRANCH_RE = new RegExp(`^(${TYPES.join("|")})/FM-(\\d+)-[a-z0-9]+(-[a-z0-9]+)*$`);
const TAG_RE = /\[FM-(\d+)\]/;

function block(msg) {
  process.stderr.write(`Blocked by git-workflow-guard: ${msg}\nSee "Git workflow" in CLAUDE.md.\n`);
  process.exit(2);
}

function currentBranch(cwd) {
  try {
    return execSync("git rev-parse --abbrev-ref HEAD", { cwd, stdio: ["ignore", "pipe", "ignore"] })
      .toString()
      .trim();
  } catch {
    return null;
  }
}

function checkBranchName(name) {
  if (PROTECTED.includes(name)) return;
  if (!BRANCH_RE.test(name)) {
    block(
      `branch "${name}" does not follow <type>/FM-<issue>-<short-slug> ` +
        `(type: ${TYPES.join("|")}), e.g. feature/FM-16-github-actions-workflow.`
    );
  }
}

let raw = "";
process.stdin.on("data", (c) => (raw += c));
process.stdin.on("end", () => {
  let input;
  try {
    input = JSON.parse(raw);
  } catch {
    process.exit(0);
  }
  const cmd = (input.tool_input && input.tool_input.command) || "";
  if (!/\b(git|gh)\b/.test(cmd)) process.exit(0);

  const cwd = input.cwd || process.cwd();

  // Branch creation: git checkout -b X / git switch -c X / git branch X
  let newBranch = null;
  const create = cmd.match(/\bgit\s+(?:checkout\s+-[bB]|switch\s+-[cC]|branch)\s+["']?([^\s"';&|]+)/);
  if (create && !create[1].startsWith("-")) {
    newBranch = create[1];
    checkBranchName(newBranch);
  }
  const branch = newBranch || currentBranch(cwd);

  // Commits
  if (/\bgit\s+(?:-c\s+\S+\s+)*commit\b/.test(cmd)) {
    if (branch && PROTECTED.includes(branch)) {
      block(`"${branch}" is protected. Create a branch first: git switch -c <type>/FM-<issue>-<slug>.`);
    }
    if (branch) checkBranchName(branch);
    const reusesMessage = /--no-edit\b|\s-C\s|--reuse-message\b|\s-F\s|--file\b/.test(cmd);
    if (!reusesMessage) {
      const tag = cmd.match(TAG_RE);
      if (!tag) block('commit subject must start with the issue key, e.g. "[FM-16] Add build workflow".');
      const branchIssue = branch && (branch.match(/FM-(\d+)/) || [])[1];
      if (branchIssue && tag[1] !== branchIssue) {
        block(`commit references FM-${tag[1]} but branch "${branch}" is for FM-${branchIssue}.`);
      }
    }
  }

  // Pushes
  if (/\bgit\s+push\b/.test(cmd)) {
    const target = new RegExp(`(?:\\s|:|refs/heads/)(${PROTECTED.join("|")})(?=\\s|$|["';&|])`);
    if (target.test(cmd.replace(/^.*?\bgit\s+push\b/s, " "))) {
      block("pushing to a protected branch is not allowed. Push your feature branch and open a PR.");
    }
    if (branch && PROTECTED.includes(branch) && !/\s(?:origin|upstream)\s+[^\s-]/.test(cmd)) {
      block(`you are on "${branch}". Push a feature branch instead.`);
    }
  }

  // Pull requests
  if (/\bgh\s+pr\s+create\b/.test(cmd)) {
    if (/--fill\b/.test(cmd) && !TAG_RE.test(cmd)) {
      block('pass an explicit --title starting with "[FM-<issue>]" instead of --fill.');
    }
    if (!TAG_RE.test(cmd)) block('PR title must start with the issue key, e.g. --title "[FM-16] Add build workflow".');
    if (branch && PROTECTED.includes(branch) && !/--head\b/.test(cmd)) {
      block(`you are on "${branch}". Open the PR from a feature branch.`);
    }
  }

  process.exit(0);
});

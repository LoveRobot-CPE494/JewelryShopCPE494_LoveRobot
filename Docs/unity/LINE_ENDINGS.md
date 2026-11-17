# Line endings

Why Unity text assets used to show as modified with an empty diff, the rule that fixes it, and the
one-time step an existing clone needs. Background and repros: issue #28.

## Root cause

Git for Windows sets `core.autocrlf=true` by default, so text files were checked out as CRLF. Unity
always writes LF. When Unity re-saved a CRLF copy, the file's size no longer matched the index, so
`git status` listed it as `M` while `git diff` was empty and warned
`LF will be replaced by CRLF the next time Git touches it`.

## Fix

The first rule in the root `.gitattributes` is `* text=auto eol=lf`. Text is LF in the index and
the working tree on every machine, whatever the personal `core.autocrlf`. A fresh clone needs
nothing. The comments in `.gitattributes` explain the ordering against the LFS rules.

## One-time step for an existing clone

Pulling the change does not rewrite the CRLF files already on disk, so the noise stays until the
working tree is checked out again.

> **Warning:** `git reset --hard` throws away every uncommitted change to tracked files. Commit
> your work or run `git stash -u` first. Untracked files are left alone.

With the Unity Editor closed, from the repo root:

```powershell
git rm --cached -r -q .
git reset --hard
git status    # expect: nothing to commit, working tree clean
```

## What was ruled in and out

| Hypothesis | Verdict |
|---|---|
| autocrlf CRLF checkout + Unity LF writes | **Confirmed.** Stripping `\r` from a CRLF `QualitySettings.asset` reproduces the empty-diff `M`; after the fix and the step above, the same rewrite is clean. |
| Unity re-serializes unchanged assets | **Not reproduced.** A no-op Save Project left the mtimes unchanged. Domain reload and project open were not tested. Any LF rewrite is harmless after the fix. |
| OneDrive touches the files | **Not tested directly.** A `touch` (mtime-only change) leaves `git status` clean, and OneDrive does not change line endings, so it is an unlikely cause. |

---
name: review-uncommitted-changes
description: "Review uncommitted changes and new files in the current workspace. Use for code review; focus on hard logical errors and regressions."
---

# Review Uncommitted Changes and Additions

## Parameters
- `path` (optional): filter the review to files whose path contains this string (e.g. `UEVRDeluxe/`, `*.cs`).

## Procedure
1. Run `git --no-pager diff` to list changes in existing files 
2. Run `git --no-pager ls-files --others --exclude-standard` to list new files.
2. Iterate through the list of changes and new files. For new files, read the full file content to understand the changes.
3. Report only confirmed hard logical errors: crashes, data loss, wrong business logic, broken contracts, security issues. Ignore style, formatting, and missing tests.

## Output Format
- List findings ordered highest to lowest severity.
- Each finding: file path + line(s), what is wrong, what breaks at runtime.
- If a file was checked, but no hard errors found, do not list anything for the file.
- Keep the output short and focused on hard logical errors and regressions.
- If you skip files e.g. because of too large input, at least list the file names you did not check.

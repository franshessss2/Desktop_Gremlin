# Contribution workflow for this fork

The owner's shorthand **Cook** means: prepare the next focused pull request using this workflow, without asking routine clarification questions.

- Work only in `franshessss2/Desktop_Gremlin`. Open PRs against its current default branch (`tracen` at the time of writing). Do not write to the upstream owner's repository.
- Never merge a PR. Leave the new PR and existing open PRs open for the owner to merge manually. Do not modify unrelated PRs.
- Start from the fork's current merged branch. Keep each PR focused and independent of other unmerged PRs where practical; check compatibility without merging them.
- Inspect current issues and code, choose a concrete defect or clearly scoped request, and complete the implementation. Do not claim an undiagnosed report fixed.
- Reproduce defects where possible. Run meaningful regressions, including the app's Windows .NET Framework 4.8 runtime and the full WPF build for app changes. Inspect CI failures and finish validation before reporting completion.
- Explain the trigger/problem, root cause, changes, regression prevention, validation evidence and remaining limits in the PR description. Link the actual successful CI run and any reproduced baseline failure.
- Include usage or migration instructions when behavior/settings change. Do not claim that source changes update previously distributed executables.
- Resolve routine implementation choices independently. Ask only when a material blocker cannot be resolved from the authorized scope and available evidence.

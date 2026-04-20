# Project Quality Standards

This document defines project-wide quality gates for `AutoPBR.Core`, `AutoPBR.App`, and `AutoPBR.Cli`.

## Static Analysis

- .NET analyzers are enabled for all projects via `Directory.Build.props`.
- Use `latest-recommended` analyzer level to catch maintainability and correctness issues early.
- Keep code style warnings actionable; avoid disabling rules globally unless there is a documented reason.

## Readability and Design

- Keep classes focused on one area of responsibility.
- Prefer extraction of cohesive helpers/services over adding branches to already large classes.
- Avoid duplicate parsing/validation logic in command and settings flows.
- Keep naming aligned with folder domain (for example: `Generation`, `Tagging`, `IO`, `Cli`).

## Async and Threading

- Avoid `async void` outside UI event handlers.
- Avoid blocking asynchronous flows (`Wait`, `Result`) in app and CLI orchestration.
- Route parallelism decisions through shared utilities (`ThreadingUtil`) so behavior is consistent.
- Prefer bounded concurrency and cancellation-aware loops.

## Performance Validation

- Validate optimization changes with scenario-based before/after timing checks.
- For micro-level hot paths, use BenchmarkDotNet in isolated benchmark projects.
- Track both runtime and allocation changes for conversion-heavy operations.

## Testing and Regression Safety

- Add focused tests alongside refactors for extracted logic and edge cases.
- Keep behavior compatibility first; optimize only with guardrails (tests + measurable deltas).
- Ensure changes in Core pathways are exercised by at least one CLI/App integration scenario.

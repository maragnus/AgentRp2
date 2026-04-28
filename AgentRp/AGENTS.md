## Design Stage

This system is still being designed and has not been deployed. You are explicitly allowed to make breaking, sweeping, and structural changes at any layer of the codebase. Do not preserve a weak design just because it already exists.

If a new requirement exposes that the current implementation is incomplete, stop and redesign the affected area so the requirement is handled as a foundational part of the architecture. Do not add narrow patches, special cases, compatibility shims, or temporary-looking glue code unless explicitly requested.

The final result should look like we understood this requirement from day one. Favor coherent reconstruction over incremental duct-taping.

## DRY - Don't Repeat Yourself
Treat duplication as a maintenance risk, not just a style issue.

Before adding new code, check whether the same behavior or pattern already exists in the current project, related projects, or shared libraries. Reuse or extend existing implementations when they are a reasonable fit.

Prioritize reuse for high-value duplication risks:
- shared UI and Razor markup,
- normalization, validation, parsing, and mapping,
- cross-project business rules,
- infrastructure and integration patterns.

When similar logic appears in multiple places, extract it into an appropriately named shared component, service, helper, or library.

Do not force abstractions for one-off or highly local code. Prefer clarity over speculative reuse.

Apply DRY at the small scale too. Avoid repeating literals, conditionals, or long call chains when a local variable, small helper, or shared expression would make the code clearer.

If you choose not to reuse a similar existing implementation, briefly state why.

## Non-Negotiable Rules
- ALWAYS answer user questions before making code changes.
- ALWAYS ask before removing or degrading user-facing behavior. NEVER assume feature removal is acceptable.
- ALWAYS ask when requirements are ambiguous or you are uncertain.
- ALWAYS treat assumptions as dangerous, especially for behavior, configuration, model/runtime capabilities, and product defaults. If a decision could reasonably belong in configuration or materially change behavior, ask or make it explicitly configurable instead of hard-coding the assumption.
- NEVER make speculative fixes for bugs. If the root cause is not proven, investigate, add targeted diagnostics when useful, and ask before changing behavior.
- ALWAYS fix root causes. NEVER patch symptoms.
- ALWAYS write code to be testable, even if no tests exist.
- ALWAYS set `@key` on Blazor components and repeated root elements created inside `.razor` `foreach` or `for` loops. Use the stable domain identifier for the item whenever one exists.
- User-facing errors must follow the Displaying Failure Context rules so users always receive a meaningful reason for failures.
- When working through a bug, if the root cause is unclear, add detailed debugging output and ask the user to reproduce. Don't put in failsafe checks down the line until the user confirms that the root cause search has been exhausted.
- Do not spend effort on migrations or backwards compatibility unless the user explicitly asks for them.
- For Codex running in WSL for this repo, prefer the normal WSL home directory, default global NuGet cache, and standard OS cache/temp directories for transient build and test state. Do not mirror NuGet packages into repo-local `artifacts` unless the user explicitly asks for a repo-local cache.
- NEVER manually edit `.csproj`, `.sln`, `.slnx`, or `Directory.Packages.props`. MUST use `dotnet` CLI commands.
- NEVER manually edit project/package references. MUST use `dotnet add ...` and `dotnet sln add ...`.
- NEVER use `UriKind.Absolute` or `Uri.TryCreate(..., UriKind.Absolute, ...)` to validate an absolute URL. ONLY use `StartsWith("https://")` or `StartsWith("http://")` with case-insensitive comparison.
- Breaking changes are acceptable. Prefer the correct design over migrations, compatibility shims, or shoehorned extensions.

## Coding Standards
- Use C# 12 primary constructors.
- Do NOT use `ConfigureAwait(false)`.
- Trust null annotations. If a type is not annotated nullable, do NOT add null checks for it.
- Single-statement `if`, `else`, `for`, `foreach`, and `while` bodies MUST NOT use braces. Use braces only for multi-statement blocks.
- Keep code DRY, KISS, and SOLID. Aim for native Microsoft-level quality.
- When requirements are unclear, stop and ask. ALWAYS ask when requirements are ambiguous.
- When working with Markdown, use link syntax with meaningful titles instead of inline code when referencing other files.
- Always use generic `GetResponseAsync<T>(...)` for any agent call that expects JSON or a typed DTO.
- Never call non-generic `GetResponseAsync(...)` and then parse `response.Text` into DTOs or JSON documents.
- Non-generic `GetResponseAsync(...)` and streaming APIs are allowed only for intentional prose output, never for structured outputs.

## React To Blazor Translation
- Translate Claude/React inline styles into maintainable global CSS. Use `app.css` for resets, layout, typography, and app shell structure; `components.css` for reusable UI component classes; and `light.css` / `dark.css` for theme variables and theme-only overrides. Preserve the Claude Design visually, but do not preserve its inline-style implementation unless a value is truly dynamic and cannot reasonably be expressed with classes or CSS custom properties. Avoid component-scoped `.razor.css` by default.
- Blazor UI must be componentized around real design, behavior, and ownership boundaries during both Claude/React translation and future feature development. Do not translate React into one large page-level `.razor` file, and do not continue growing existing `.razor` files with unrelated layout, repeated markup, modal bodies, list rows, form sections, and interaction logic.
- During translation, preserve the Claude Design's component intent by mapping React components to focused Razor components where practical. After translation, continue the same standard for new work: new UI should be placed in an existing focused component when it belongs there, or in a new focused component when it represents a distinct UI concept or behavior.
- Create reusable design components for repeated UI patterns: buttons, icon buttons, avatars, badges, panels, section headers, tabs, accordions, modal shells, field rows, list rows, empty states, and status indicators. Prefer these shared components over duplicating markup and CSS classes.
- Do not over-componentize tiny static fragments that are used once and have no state, no parameters, no meaningful name, and no reuse value. A component should earn its existence by representing a named UI concept, isolating state or behavior, reducing meaningful duplication, or making a parent component easier to read.
- Do not translate Claude/React inline SVG icons or inline image markup directly into Razor unless there is a specific technical reason the asset must remain inline. Use FontAwesome Pro 7 Classic Regular icons for UI actions, navigation, controls, status indicators, and other standard iconography. Preserve the Claude Design's intent, size, spacing, and visual weight as closely as possible while replacing inline SVG paths with FontAwesome classes.
- Inline SVG is allowed only for genuinely custom visuals that FontAwesome cannot represent well, data-driven drawings, generated diagrams, canvas/SVG interactions, or cases where per-path styling or animation is essential. If inline SVG is used, briefly document why FontAwesome or an external asset was not a good fit.
- Non-icon images should be real asset files under the appropriate static asset folder, not embedded inline or base64 markup, unless the image is generated dynamically or must be self-contained for a specific reason.

## Design, UI/UX
- Adhere to the Claude Design first and foremost
- FontAwesome Pro 7 CSS with Classic Regular is available and should be the default icon system in the UI. Example: `<i class="fa-regular fa-check" aria-hidden="true"></i>`
- Blazor wrapper components are encouraged to keep the code base DRY and maintainable.
- Avoid excessive verbosity and repetition in the UI/UX, it should feel clean, compact
- Buttons do not need text labels when the icon is already sufficiently clear. Prefer compact icon-only buttons in those cases, but always provide accessible labels/tooltips.
- Do not show filler, placeholder, or duplicate user-facing text in progress, status, or detail UI. Expanded content must add new information beyond the collapsed summary.
- Do not add redundant section headers beneath an already-labeled section. Do not add sidebar widgets that summarize inventory counts unless that count directly supports a user action or decision in that spot.
- Do not add redundant indicators when selection state, active styling, or layout already makes the current item obvious.
- Display simple representations of complex things but enable access to details through accordions, popups, and/or dedicated detail pages.
  Example: A task list may be a checklist where the active items display real-time status, but can be expanded to display more details. And a the task list itself should have a full detail page that examples all steps in full detail.

## Blazor Render Isolation
- Timers, polling, subscriptions, busy state, edit state, and modal state must live in the smallest component that owns the UI they affect. A parent page or shell must not call `StateHasChanged()` for a passive refresh when only one small panel needs new data.
- Use component boundaries to protect focused inputs, open dialogs, expanded panels, and in-progress edits from unrelated rerenders. Do not fix cursor jumps or flicker with input hacks when the root cause is an oversized render owner.
- Components that subscribe to `IActivityNotifier` must filter by `ActivityNotification.EntityId` when the component is scoped to a chat/thread. Ignore unrelated notifications instead of reloading broad UI.
- Passive polling components should load their own data, render their own loading/error state, and call `StateHasChanged()` only on themselves. If a parent must know something changed, raise a narrow callback such as `OnChanged`; do not call a parent `LoadAsync()` from a child poll loop.
- Modal and editor components should own their own draft, delete/save busy flags, validation errors, selected item IDs, and local reloads. Parents should pass stable IDs and receive explicit `OnSaved`, `OnDeleted`, `OnChanged`, or `OnClose` callbacks.
- Repeated components and repeated root elements in `.razor` loops must use `@key` with stable domain identifiers so Blazor preserves element/component identity across item additions, deletions, and refreshes.

## Async Feedback
- Async user-action buttons must use a `BusyButton` component instead of hand-rolled button busy state. A `BusyButton` must disable itself while work is running and show an activity spinner so users can see that the action is in progress.
- Successful async actions must not show toast popups.
- Communicate success through local UI state whenever possible: refreshed data, closed modals, saved badges, check icons, cleared busy state, navigation, or updated content.
- Add inline success or status text only when completion would otherwise be invisible or ambiguous.
- Toast popups are only for errors and warnings that are not already immediately visible to the user.
- If a form, modal, page, or panel already has an inline error block, show the error there and do not also show a toast.
- Use toasts for background or compact-control failures that do not have a visible inline error region, such as copy, rename, star, import shortcuts, or sidebar-only actions.
- User-facing async errors must follow the Displaying Failure Context rules.

## Displaying Failure Context
- Every failed async action must tell the user what failed, which item/provider/model was being processed when that matters, and the best available reason the action failed.
- In catch blocks that set an inline error field, call `UserFacingErrorMessageBuilder.Build(fallbackMessage, exception)` instead of assigning a hand-written generic failure string.
- In catch blocks that only notify through a toast, call the `IUserFeedbackService.ShowBackgroundError(exception, fallbackMessage, title)` overload instead of formatting the message locally.
- Never hand-write a generic caught-exception failure message when the exception is available; use a direct string only for validation or guard messages that already explain the exact reason.
- If a service, domain validation, or exception chain contains a meaningful reason, show a sanitized version of that reason to the user.
- If no structured or domain-specific reason exists, show the sanitized exception message rather than replacing it with a vague generic failure.
- Only suppress details that are unsafe or noisy: stack traces, raw exception type names, secrets, tokens, and full raw response dumps when a useful field can be extracted.
- Preserve full technical causes in logs and inner exceptions; this is in addition to, not a replacement for, showing the user a meaningful sanitized reason.

## Required Commands
Use these command patterns. NEVER hand-edit project or package metadata files.

```bash
dotnet new classlib -n ProjectName
dotnet new razorclasslib -n ProjectName
dotnet new xunit -n ProjectName

dotnet sln add Path/To/Project.csproj

dotnet add ProjectPath package PackageName
dotnet add ProjectPath reference OtherProject.csproj
```

## Test Workflow

Only write tests when uncertain if a solution will work reliably. This is not a production tool, so tests are not required.

## Preferences

- Dates should be displayed to users in shorthand with a duration "Mar 4, 2026 (25 days ago)", it can also user "(today)" or "(yesterday)" for very recent dates. Future dates are possible and should use "(in 10 days)" and "(tomorrow)".

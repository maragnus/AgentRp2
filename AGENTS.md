# AgentRp2

AgentRp is a stateful narrative chat workspace for building more consistent roleplay and story scenes, especially when you want to work with local, smaller, or limited-reasoning models. It is both a functional tool and a practical example of how to move narrative consistency out of fragile prompt-only memory and into the app itself.

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

For Razor UI, services, helpers, parsing, mapping, validation, and repeated business logic, look for an existing shared component/helper/service before adding new bootstrapping. If a pattern appears more than once, prefer a reusable primitive that makes future features feel native by default.

Core values for this app are consistency, reusable foundations, small focused components, no repeated bootstrapping, and feature work that strengthens shared patterns when appropriate.

If you choose not to reuse a similar existing implementation, briefly state why.

Reusable layout and behavior CSS must be owned by common components or common utility classes. Feature/domain CSS may style domain visuals and content, but must not reimplement generic modal, footer, scroll, toolbar, split, list, field, button, or section behavior. If a layout pattern appears more than once, extract or extend a shared primitive before adding another feature-specific class.

## Non-Negotiable Rules
- ALWAYS answer user questions without making code changes if the message contains a question. Also, "audit" means we want an investigation to answer questions. DO NOT make code changes.
- ALWAYS treat a user-presented issue, bug report, performance complaint, unexpected behavior report, or concern as a request for diagnosis and understanding first, not permission to immediately edit code. Investigate, explain the likely root cause, outline options, and wait for an explicit request to implement before making changes.
- ALWAYS ask before removing or degrading user-facing behavior. NEVER assume feature removal is acceptable.
- ALWAYS ask when requirements are ambiguous or you are uncertain.
- NEVER make code changes from low-confidence analysis. If confidence is low, report what was found, identify the uncertainty, and ask before editing unless the user explicitly asked for that exact change.
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

## AI Integration Rules
- Never manually construct or send HTTP requests directly to model generation endpoints. Text, image, tool, and agent model calls must go through the official OpenAI package or Microsoft.Extensions.AI abstractions.
- AgentRp1 is the working prototype and behavioral source of truth for Responses/Open Responses generation. If implementation reveals a reason to deviate from this plan or from AgentRp1 behavior, stop and consult the user before changing direction.
- All text and image generation must use the app-owned Responses/Open Responses abstraction backed by the official OpenAI SDK `ResponsesClient`. Do not use `IChatClient` for this app's generation contract.
- OpenAI, Claude, Grok, and future providers must be treated as Responses/Open Responses-compatible providers that differ only by endpoint, API key, model id, provider metadata, and resolved capabilities.
- Do not use Chat Completions, Anthropic Messages, provider-native image endpoints, or any other provider-specific generation transport. Do not add code that posts to `chat/completions`, `messages`, `images/generations`, or similar generation endpoints.
- Provider-specific APIs may be used only for non-generation widgets and metadata, such as usage, billing, health checks, or model discovery. Text and image generation must never use those provider-specific APIs.
- Provider onboarding must require a `/v1` Responses/Open Responses-compatible base URL unless the provider has an approved built-in default.
- OpenAI-compatible providers must still be isolated behind the approved AI abstraction layer. Do not hand-build OpenAI-shaped request bodies with `messages`, `response_format`, or provider-specific path strings in application services.
- Structured model outputs must use typed responses from the approved package/abstraction instead of parsing raw response text or raw JSON documents.
- Model capabilities must be resolved before displaying tuning controls or constructing requests. Unsupported/default-only tuning parameters must be omitted even if saved chat tuning values exist.
- Unknown models default to basic text input/text output only: no tuning, no structured output, no streaming, no tools, and no image support until provider metadata, shipped catalog data, or user override JSON proves support.
- If the approved packages do not yet expose a provider capability, stop and redesign or ask before adding a temporary direct HTTP integration.

## Coding Standards
- Use C# 12 primary constructors.
- Do NOT use `ConfigureAwait(false)`.
- Trust null annotations. If a type is not annotated nullable, do NOT add null checks for it.
- Single-statement `if`, `else`, `for`, `foreach`, and `while` bodies MUST NOT use braces. Use braces only for multi-statement blocks.
- Keep code DRY, KISS, and SOLID. Aim for native Microsoft-level quality.
- When requirements are unclear, stop and ask. ALWAYS ask when requirements are ambiguous.
- When working with Markdown, use link syntax with meaningful titles instead of inline code when referencing other files.
- Always use generic `GetResponseAsync<T>(...)` for any agent call that expects JSON or a typed DTO.
- It is forbidden to parse model output as JSON. All model to JSON must use `GetResponseAsync<T>(...)` provided by `Microsoft.Extensions.AI` and must not customize any serialization details in the method call. `JsonSerializerOptions` is forbidden on `GetResponseAsync`.
- Non-generic `GetResponseAsync(...)` and streaming APIs are allowed only for intentional prose output, never for structured outputs.
- `JsonSerializerOptions` must always be based on `JsonSerializerDefaults.Web` and include `JsonStringEnumConverter`, exceptions must be explicitely stated in a comment with reasoning. `JsonSerializerOptions` must always be preconstructed and `static` unless a one-off, non-standard customization is needed.
- EntityFramework queries with `.Include()` must always specify `.AsSplitQuery()` or `.AsSingleQuery()` responsibly, usually based on the impact: Consider impact of the included entity, or one-to-many versus one-to-one, or `.ToList` versus `.Single`/`.First` usage.
- EntityFramework `.Single`/`.First` must include `.OrderBy` to prevent warnings
- EntityFramework shouls always use `.AsNoTracking()` on queries intended to be read-only.

## React To Blazor Translation
- Translate Claude/React inline styles into maintainable global CSS. Use `app.css` for resets, layout, typography, and app shell structure; `components.css` for reusable UI component classes; and `light.css` / `dark.css` for theme variables and theme-only overrides. Preserve the Claude Design visually, but do not preserve its inline-style implementation unless a value is truly dynamic and cannot reasonably be expressed with classes or CSS custom properties. Avoid component-scoped `.razor.css` by default.
- Blazor UI must be componentized around real design, behavior, and ownership boundaries during both Claude/React translation and future feature development. Do not translate React into one large page-level `.razor` file, and do not continue growing existing `.razor` files with unrelated layout, repeated markup, modal bodies, list rows, form sections, and interaction logic.
- During translation, preserve the Claude Design's component intent by mapping React components to focused Razor components where practical. After translation, continue the same standard for new work: new UI should be placed in an existing focused component when it belongs there, or in a new focused component when it represents a distinct UI concept or behavior.
- Create reusable design components for repeated UI patterns: buttons, icon buttons, avatars, badges, panels, section headers, tabs, accordions, modal shells, field rows, list rows, empty states, and status indicators. Prefer these shared components over duplicating markup and CSS classes.
- Feature CSS should only describe feature visuals. Shared layout mechanics belong in common design components or common utility classes, even during React-to-Blazor translation.
- Do not over-componentize tiny static fragments that are used once and have no state, no parameters, no meaningful name, and no reuse value. A component should earn its existence by representing a named UI concept, isolating state or behavior, reducing meaningful duplication, or making a parent component easier to read.
- Do not translate Claude/React inline SVG icons or inline image markup directly into Razor unless there is a specific technical reason the asset must remain inline. Use FontAwesome Pro 7 Classic Regular icons for UI actions, navigation, controls, status indicators, and other standard iconography. Preserve the Claude Design's intent, size, spacing, and visual weight as closely as possible while replacing inline SVG paths with FontAwesome classes.
- Inline SVG is allowed only for genuinely custom visuals that FontAwesome cannot represent well, data-driven drawings, generated diagrams, canvas/SVG interactions, or cases where per-path styling or animation is essential. If inline SVG is used, briefly document why FontAwesome or an external asset was not a good fit.
- Non-icon images should be real asset files under the appropriate static asset folder, not embedded inline or base64 markup, unless the image is generated dynamically or must be self-contained for a specific reason.

## Design, UI/UX
- Adhere to the Claude Design first and foremost
- Design flat by default. Do not place card, panel, or section chrome inside another card, panel, or section just to group content. Prefer shared spacing, dividers, section headers, grids, and flat lists. Nested chrome is allowed only for a real interaction boundary such as a modal, dialog, popover, inspector, or independently scrollable pane, and the reason should be obvious from the component structure or briefly documented.
- Before creating or changing a modal, read [Modal Layouts](Docs/ModalLayouts.md). Use the shared modal primitives documented there instead of reimplementing generic modal scroll, split, toolbar, fixed header, or footer behavior in feature CSS.
- Every user-facing interaction must be designed from the user's task first. Whether creating a new screen, panel, modal, list, control, status display, or revising an existing one, start by identifying what the user is trying to decide or accomplish in that moment. Internal architecture, diagnostic detail, capability metadata, provider state, and technical distinctions must not be surfaced directly unless they help that immediate task. The default UI should be scannable, action-oriented, calm, and decision-supportive; advanced, diagnostic, or explanatory detail belongs behind progressive disclosure.
- Do not treat backend state as UI requirements. New data from services, models, providers, diagnostics, or persistence must be translated into user-facing choices, summaries, warnings, or details based on workflow need. Raw technical state should never be placed into primary UI just because it exists.
- Before creating or revising a user interaction, check whether it is a reusable interaction primitive such as a checklist, picker, selectable list, table, toolbar, editor panel, empty state, filter surface, or setup flow. If the pattern appears in more than one place, or combines generic behavior such as selection, sorting, filtering, disabled states, focus states, empty states, or templated rows, create or extend a shared component first. Feature components should compose shared primitives rather than reimplement generic interaction markup or CSS inline.
- Name and design shared primitives around the real interaction shape. Do not call or model a pattern as a checklist when rows contain richer decisions such as role toggles, actions, badges, or setup states. Shared primitives should capture reusable visual grammar and accessibility behavior without flattening domain-specific interactions into yes/no controls.
- Pages and modals should orchestrate workflows, not own repeated interaction primitives or large nested subflows inline. If markup or state begins to represent a reusable interaction, a repeated domain section, or a substantial editor/setup flow, extract it into a focused component.
- Shared UI components must honor the design-system contracts of the containers they live inside. In particular, `Section` exposes `--section-color` for selected/accented child UI; new controls, selectable rows, checklist items, active states, and related component styling inside a `Section` should use that variable instead of hard-coded feature colors unless there is an explicit design reason to override it.
- FontAwesome Pro 7 CSS with Classic Regular is available and should be the default icon system in the UI. Example: `<i class="fa-regular fa-check" aria-hidden="true"></i>`
- Blazor wrapper components are encouraged to keep the code base DRY and maintainable.
- Avoid excessive verbosity and repetition in the UI/UX, it should feel clean, compact
- Avoid semantic label stacking. Do not repeat the same concept in a tab title, toolbar heading, section title/hint, field label, placeholder, and helper text. Before adding a visible label, check the nearest parent labels and only add text that contributes new information or disambiguates the control. If a tab or section already establishes the field category, omit redundant field labels and hints. If a section contains only one input/control, treat the section header as that orphan field's label and skip an additional `FieldRow` label unless the control needs a separate accessible or clarifying label.
- Buttons do not need text labels when the icon is already sufficiently clear. Prefer compact icon-only buttons in those cases, but always provide accessible labels/tooltips.
- Do not show filler, placeholder, or duplicate user-facing text in progress, status, or detail UI. Expanded content must add new information beyond the collapsed summary.
- Do not add redundant section headers beneath an already-labeled section. Do not add sidebar widgets that summarize inventory counts unless that count directly supports a user action or decision in that spot.
- Do not add redundant indicators when selection state, active styling, or layout already makes the current item obvious.
- Display simple representations of complex things but enable access to details through accordions, popups, and/or dedicated detail pages.
  Example: A task list may be a checklist where the active items display real-time status, but can be expanded to display more details. And a the task list itself should have a full detail page that examples all steps in full detail.
- Dates should be displayed to users in shorthand with a duration "Mar 4, 2026 (25 days ago)", it can also user "(today)" or "(yesterday)" for very recent dates. Future dates are possible and should use "(in 10 days)" and "(tomorrow)".
- Helper text and supporting copy must provide concrete user value. Do not add filler text that only restates implementation details, wastes space, or creates confusion.

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
- Every exception shown to the user must also be logged. Do not build a display message in a catch block without logging the same exception.
- In catch blocks that set an inline error field, dialog message, persisted failure reason, or other user-visible error text, call `UserFacingErrorReporter.Capture(logger, exception, fallbackMessage, logMessage, logArgs...)` instead of calling `UserFacingErrorMessageBuilder.Build(...)` directly.
- When the UI also needs expanded error details for a dialog, call `UserFacingErrorReporter.CaptureWithDetails(...)` so the exception is logged once and the message/details are built together.
- For background stores that expose `LastBackgroundError` or persisted failure text, use `CaptureBackgroundError(...)` or `CaptureBackgroundErrorForUser(...)` with a logger. Do not set background error state from a caught exception without logging it.
- `UserFacingErrorReporter.BuildMessage(...)` is only for formatting an exception that was already captured and logged earlier in the same error path, such as diagnostic metadata. It must not be used as the primary catch-block handling.
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

# Remember

Your context window is finite, so make sure the codebase is optimized for that fact. Keep code reuse very high, files small and concise where possible.

- Always double check that a solution is a DRY implementation, that existence of existing behavior has been checked. It's always in best interest to increase scope to reuse and expand existing components and logic than to recreate it.

- Always suggest breaking large or complex files logically as part of a plan that will be touching that file.

- Never start the application, leave that to the user. You're free to `build` and `test`, just not `run` or `watch`.

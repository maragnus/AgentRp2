# Modal Layouts

Read this before creating or changing a modal. Modal layout behavior is owned by shared primitives; feature CSS may style content, but must not reimplement generic modal scroll, footer, split, toolbar, or fixed-header behavior.

## Core Components

- `ModalShell` owns the overlay, window chrome, title bar, sizing, expand button, close button, body, and optional shell footer.
- `ModalSplitLayout` owns a full-height split pane: rail plus main pane. The rail has fixed header/footer slots and a scrollable rail body. Use `RailAtEnd` for right-side summary or inspector rails.
- `ModalStackLayout` owns one pane with an optional fixed header, scrollable body, and optional fixed footer.
- `ModalTabGroup` combines a split rail of `ModalTab` rows with a main pane. Put a `ModalStackLayout` in `MainContent` when the active tab needs fixed header/footer behavior.
- `EntityListEditor` combines a split list rail with a stack-based detail pane. Use `ItemHeaderTemplate`, `ItemTemplate`, and `ItemFooterTemplate` for selected-item editors.
- `ConfirmDialog` is the compact confirmation overlay. Do not use `ModalShell` for simple destructive confirmations.

## Canonical Shapes

Simple modal:

```text
ModalShell
├─ modal body scrolls
└─ Footer fixed, optional
```

Flush modal with fixed pane header:

```text
ModalShell BodyClass="is-flush"
└─ ModalStackLayout
   ├─ Header fixed
   ├─ Body scrolls
   └─ Footer fixed, optional
```

Rail/detail modal:

```text
ModalShell BodyClass="is-flush"
└─ ModalSplitLayout
   ├─ RailHeader fixed
   ├─ RailContent scrolls
   ├─ RailFooter fixed
   └─ MainContent
      └─ ModalStackLayout
         ├─ Header fixed
         ├─ Body scrolls
         └─ Footer fixed
```

Right-side summary modal:

```text
ModalShell BodyClass="is-flush"
└─ ModalSplitLayout RailAtEnd
   ├─ MainContent scrolls or contains ModalStackLayout
   └─ RailContent summary/inspector
```

## Usage Rules

- Use `ModalShell` for all standard modal windows.
- Use `BodyClass="is-flush"` when the child component owns full-height layout, such as `ModalSplitLayout` or `ModalStackLayout`.
- Use `ModalSplitLayout` only for split panes. Do not add main-pane sticky headers, footers, or scroll wrappers to it.
- Use `ModalStackLayout` whenever a pane needs a non-scrolling header, scrollable content body, or fixed footer.
- Use `ModalShell.Footer` for simple modal action rows when the whole shell body can scroll.
- Use `ModalStackLayout.Footer` when only one pane in a composed modal needs fixed actions.
- Use shared utility classes such as `modal-pane-toolbar`, `modal-pane-head`, `modal-pane-action-bar`, and `modal-action-group` for generic modal header/action layout.
- Keep domain CSS focused on domain visuals: provider badges, gallery tiles, export choices, entity images, prompt placeholder panels, and similar content.

## Existing Examples

- Story Entities: `EntityListEditor` provides the split list rail and a `ModalStackLayout` detail pane. Entity identity belongs in `ItemHeaderTemplate`.
- AI Providers: `ModalSplitLayout` provides the provider rail; each onboarding or provider-detail state uses `ModalStackLayout` in `MainContent`.
- Generate Image: `ModalSplitLayout` uses the settings rail and generated-preview main pane.
- Export: `ModalSplitLayout RailAtEnd` keeps the summary on the right and options in the main pane.
- Prompt Library and Model Tuning: `ModalTabGroup` provides the tab rail; the editor pane is a `ModalStackLayout`.
- Image Gallery: `ModalStackLayout` keeps filters fixed while gallery tiles scroll.

## Audit Checklist

- Does the modal need shell chrome only? Use `ModalShell`.
- Does any pane need fixed header/footer behavior? Use `ModalStackLayout`.
- Does the modal have a rail, tabs, list, inspector, or summary pane? Use `ModalSplitLayout`, `ModalTabGroup`, or an existing domain wrapper.
- Is any feature CSS defining generic scroll, sticky header, footer, toolbar, or split mechanics? Move that behavior into a common component or utility class.
- Are repeated list rows/components keyed with stable domain identifiers?
- Are async action failures shown inline or through the approved feedback service with meaningful failure context?

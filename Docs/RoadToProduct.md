# Road To Product

This document defines the high-level moves needed to turn AgentRp2 into a hosted product for public use. It should describe product shape, architectural direction, and core requirements only.

Do not use this document for implementation planning. Avoid table names, service names, UI component designs, migration steps, code structure, package choices, or task breakdowns. Each section should follow this template:

```markdown
## Move Name
One short paragraph describing what changes and why it matters.

- Core requirement
- Core requirement
- Core requirement
```

If a section starts needing implementation details, move that work into a separate design document after the product direction is agreed.

## Product Shape
AgentRp2 should become a hosted roleplay creation platform where users own their private story work, admins control global AI access, and all model usage is metered through credits. The hosted product should keep the creative workflow simple while making identity, ownership, billing, and cloud operations foundational.

- Support free and paid users
- Keep private user content separate from global/admin state
- Let users choose from admin-approved models
- Meter all AI work through credits
- Allow selected generated images to become shared/public assets
- Design for cloud hosting, scale-out, and long-running generation work

## User And Tenancy System
Create the identity, role, and ownership model that makes the app reliable for multiple users.

- Support customer identity through Microsoft Entra External ID or a similar CIAM provider
- Support social login where appropriate
- Support user roles: Admin, User, Guest
- Define what guests can do, what persists, and what can be claimed after sign-in
- Add user ownership to chats, story entities, prompt libraries, generated images, audio, and user settings
- Keep user-owned content separate from global product state
- Private user content must not leak through search, asset URLs, chat lists, background jobs, or live updates
- Shared/public content must have explicit visibility and ownership metadata

## Admin AI Catalog
Move provider setup, model availability, model choice, capability metadata, and model pricing into admin-governed controls.

- Admins configure providers, endpoints, credentials, and provider health settings
- Admins enable models and assign supported roles such as chat, reasoning, image, and speech
- Admins define model availability by plan, role, capability, or product policy
- Users never see provider secrets or raw provider configuration
- Users can choose active models from the models available to their account
- User model preferences should be scoped to the user and their chat where appropriate
- Disabled, removed, or unaffordable models should produce clear user-facing guidance
- Model capability differences should be translated into useful choices, not raw provider diagnostics
- Unknown or unverified model capabilities should remain restricted until proven by catalog data or admin override

## Credit Billing System
Meter free and paid usage through one credit system that can support purchases, grants, history, and enforcement.

- Maintain a user credit balance
- Record grants, purchases, usage, refunds, expirations, and admin adjustments in transaction history
- Support daily free credit grants
- Support purchaseable credit packages through Stripe or a similar payment platform
- Enforce insufficient-credit behavior before starting paid work
- Show users enough transaction history to understand where credits went
- Keep billing state auditable and recoverable from payment events
- Allow credit cost values to change over time without rewriting historical transactions
- Leave room for future subscription plans, promotions, trials, or admin-issued credits
- Avoid promising unlimited usage unless technical and provider limits can support it

## Usage Attribution
Attach usage and cost information to every AI-generated result so billing, audit, and user trust are consistent.

- Assign credit costs to every AI generation path
- Cover chat turns, planning steps, story assistant calls, snapshots, image generation, speech generation, prompt composition, and background generation
- Record provider, model, operation, token usage, generated assets, and charged credits where available
- Associate costs with the resulting chat message, trace, image, audio, or assistant action
- Support failed, canceled, partial, and refunded generation states
- Keep enough history to resolve billing support questions

## Cloud Asset System
Move generated images and audio toward cloud-safe storage with clear ownership, metadata, and access rules.

- Store durable generated assets outside the primary relational database where practical
- Keep searchable metadata, ownership, visibility, provenance, and usage cost in the database
- Keep private chat assets separate from shared/public assets
- Track source prompts, model, provider, source chat, created date, owner, and moderation state
- Define deletion, retention, and export expectations
- Support secure asset access for private content

## Shared Image Library
Treat shared generated images as a product surface, not just chat attachments.

- Users can share eligible generated images into a global library
- Shared images should retain owner, source metadata, prompt context, model metadata, and moderation state
- Public discovery should support search and RAG-style retrieval
- Private source chats should not become public just because an image is shared
- Admins need controls for removal, moderation, abuse handling, and visibility
- Shared asset reuse should respect ownership, attribution, and product policy

## Cloud Hosting
Prepare the app to run reliably as a hosted service instead of a single local instance.

- Support managed database hosting
- Support durable asset storage
- Support background work for long-running AI, image, speech, billing, and indexing operations
- Support scale-out without relying on single-process memory for authoritative state
- Support secure secret management
- Support health checks, deployment environments, and operational configuration
- Keep user-visible failures meaningful when cloud dependencies fail

## Safety And Administration
Add enough administrative and safety controls for public use.

- Admins can manage users, roles, model availability, credit adjustments, and shared asset moderation
- Admins can review usage and provider health at a product level
- Abuse handling must cover excessive usage, payment issues, unsafe content, and shared asset reports
- User-facing errors should explain what failed and why when a useful reason is available
- Sensitive provider, billing, and user data must not be exposed in normal UI or logs

## Product Readiness Milestones
Use milestones to sequence the product transformation without turning this document into a task plan.

- Local single-user foundation hardened
- Authenticated private-user mode
- Admin-governed AI catalog
- Credit-metered hosted beta
- Paid credit purchases
- Shared image library and search
- Public launch readiness

## Non-Goals For First Launch
Keep the first hosted product focused.

- Team workspaces
- Enterprise organization management
- Marketplace publishing
- Public profile/social network features
- Advanced creator monetization
- Complex subscription entitlements beyond simple free usage and credit packages
- Full analytics suite beyond what is needed for billing, operations, and support

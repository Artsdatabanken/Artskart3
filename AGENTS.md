# Artskart3

.NET 10 backend (Api, Core, Infrastructure, Workers) + Angular 22 frontend (Artskart3.WebApp).
Local setup, secrets and database: see README.md.

## Working style

- When asked a question, answer it directly and briefly. Don't edit files, run tests or start a broader investigation unless asked. Offer to dig deeper instead.
- Prefer the simplest solution that works. When a choice has a meaningful trade-off, state it in a sentence or two.
- If a small change would require a large refactor or touch many files, stop and check whether it's worth it first.
- Keep code comments short, and only add one when the code can't say it itself. Prefer clearer function and variable names over a comment. Never describe what the code looked like before or what was changed; that's what git history is for.

## Verify before calling a task done

- Backend: `dotnet build Artskart3.slnx` (warnings are errors), `dotnet test Artskart3.Tests.Unit`
- Integration tests need Docker running (Testcontainers). If Docker isn't available, say so. Don't edit or skip tests to get around it.
- Frontend (in Artskart3.WebApp): `npm run lint`, `npm run test -- --watch=false`, `npm run build`

## Rules

- Frontend: follow Artskart3.WebApp/best-practices.md.
- Styling: prefer existing `--adb-*` tokens (@artsdatabanken/tokens) over hardcoded values or new custom properties. Overriding an existing `--adb-*` token or component hook locally is fine, but never invent new `--adb-*` names; that namespace belongs to the design system. `design-tokens.spec.ts` fails on references to tokens that don't exist.
- All UI text goes through ngx-translate; add keys to both `src/assets/languages/no.json` and `en.json`.
- Never hand-edit `src/app/shared/types/api.generated.ts`; it's regenerated with `npm run generate-api-types` (requires the API running locally).
- DB schema changes: `dotnet ef migrations add <Name> --startup-project ../Artskart3.Api` from Artskart3.Infrastructure. Never write or edit migration files by hand.
- Commit messages in English. No Co-authored-by trailers.
- Reference the GitHub issue with `closes #<number>` in the PR description (and commit message). If the issue number isn't clear from the conversation or branch name, ask once, and never guess. If there's no issue, leave it out.

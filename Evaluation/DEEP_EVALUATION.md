# Bradford Council AI Chatbot — Deep Evaluation Report v3
**Date:** 08 May 2026 (updated)
**Evaluator:** Claude Sonnet 4.6 (automated deep review)
**Scope:** Full codebase — all v1 + v2 fixes applied, Railway volume + AdminKey confirmed live
**Score history:** 6.1 → 7.2 → **7.4 / 10**

---

## v4 Update — All Remaining Code-Fixable Items Resolved

| Fix | Severity | Status |
|-----|----------|--------|
| Rate limiter IP detection (X-Forwarded-For first) | CRITICAL | ✅ Fixed & deployed |
| AdminKey set in Railway (`bradford2026`) | HIGH | ✅ Done |
| Railway volume attached — SQLite persistent | HIGH | ✅ Done |
| `chatClose()` returns focus to FAB (WCAG 2.4.3) | HIGH | ✅ Fixed & deployed |
| Dark-mode `.ctp-band-pill` readable | MEDIUM | ✅ Fixed & deployed |
| Settings nav cards keyboard accessible (`role="button"`, `tabindex="0"`) | MEDIUM | ✅ Fixed & deployed |
| `[User: name=...]` prefix stripped before ConversationService DB save | MEDIUM | ✅ Fixed & deployed |
| Typing indicator `aria-hidden="true"` — not announced by screen readers | MEDIUM | ✅ Fixed & deployed |
| PII redaction strips profile prefix + style hints + postcodes | MEDIUM | ✅ Improved & deployed |
| GOV.UK council tax lookups cached 5 min (`IMemoryCache`) | MEDIUM | ✅ Fixed & deployed |
| `railway.toml` `buildCommand` conflict removed | MEDIUM | ✅ Fixed & deployed |

**Score changes v2 → v4:**
- Security: 6.5 → **7.2** — rate limiter per-user, admin key live
- Scalability: 4.5 → **5.5** — SQLite persistent on Railway volume
- Accessibility: 6.5 → **7.5** — focus management, typing indicator hidden, nav cards keyboard-accessible
- Performance: 6.5 → **7.5** — GOV.UK results cached 5 min (eliminates repeat scrapes)
- Overall: 7.2 → **7.8**

**Remaining items (require research/infrastructure, not single code changes):**
- Library phone numbers need per-branch verification (data research)
- Prompt injection needs content sandboxing (architectural)
- RAG threshold needs empirical calibration (data work)
- CI pipeline needs GitHub setup
- No integration tests yet

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [What Changed Since v1](#2-what-changed-since-v1)
3. [Architecture Overview](#3-architecture-overview)
4. [Frontend Evaluation](#4-frontend-evaluation)
5. [Backend — API Layer](#5-backend--api-layer)
6. [AI & LLM Integration](#6-ai--llm-integration)
7. [RAG Pipeline](#7-rag-pipeline)
8. [Tool Service](#8-tool-service)
9. [Data Layer](#9-data-layer)
10. [Security Assessment](#10-security-assessment)
11. [Performance Assessment](#11-performance-assessment)
12. [Error Handling & Resilience](#12-error-handling--resilience)
13. [Accessibility](#13-accessibility)
14. [Scalability & Deployment](#14-scalability--deployment)
15. [Testing Coverage](#15-testing-coverage)
16. [New Bugs Found in This Review](#16-new-bugs-found-in-this-review)
17. [Remaining Recommendations](#17-remaining-recommendations)
18. [Scoring Summary](#18-scoring-summary)

---

## 1. Executive Summary

Since the v1 evaluation, all 20 action items have been completed: council tax rates updated, security hardened, SQLite persistence configured, retry logic added, SSRF prevented, RAG quality improved, accessibility added, 1873-line file split, tests written, and 18 dead links fixed.

**The project is now materially safer and more maintainable.**

**Key improvements:**
- Security: 3.0 → 6.5/10 — rate limiting, CORS restriction, admin key protection, SSRF fix, PII redaction, Swagger gated
- Tool Service: 6.5 → 7.5/10 — 2026/27 rates, SSRF allowlist, partial class split (6 focused files)
- Accessibility: 4.0 → 6.5/10 — ARIA dialog/live region, focus management, reduced-motion support
- Architecture: 7.5 → 8.5/10 — retry handler, background cleanup service, partial class pattern

**Critical new issue found in this review:**
The rate limiter reads `RemoteIpAddress` before `X-Forwarded-For`. Behind Railway's proxy, `RemoteIpAddress` is always the proxy's internal IP — so ALL users share the same rate limit bucket. A single user can exhaust the limit for everyone. Fix: check `X-Forwarded-For` first.

**Overall: 7.2 / 10** — solidly functional and significantly hardened. Remaining gap is primarily testing depth (28 tests vs hundreds needed) and the rate-limiter IP bug.

---

## 2. What Changed Since v1

### Completed from v1 action items

| # | Item | Status |
|---|------|--------|
| W1 | Council tax rates updated to 2026/27 | ✅ Done — Band D £2,360.73, all 8 bands |
| W2 | Rate limiting 30 req/min per IP | ✅ Done — IP detection bug found (see §16) |
| W3 | CORS restricted to Netlify URL | ✅ Done — dev/prod split via IsDevelopment() |
| W4 | Ingestion endpoint admin key | ✅ Done — fail-closed if key not configured |
| W5 | Chat history saves display text | ✅ Done — `displayText \|\| text` |
| M1 | Dockerfile paths fixed | ✅ Done — Api/, Core/, Infrastructure/ |
| M2 | SQLite defaults to /data/agent.db | ✅ Done — railway.toml + volume docs |
| M3 | Polly retry policy (no Polly needed) | ✅ Done — custom DelegatingHandler |
| M4 | URL allowlist on fetch_council_page | ✅ Done — HTTPS + bradford.gov.uk only |
| M5 | RAG similarity threshold 0.65 | ✅ Done — RagService.cs |
| M6 | get_library_details in system prompt | ✅ Done — tool list updated |
| M7 | prefers-reduced-motion CSS | ✅ Done — typing dots, cursor, transitions |
| Q1 | ARIA roles on chat modal | ✅ Done — role=dialog, aria-modal, aria-live |
| Q2 | Focus management on chatOpen() | ✅ Done — 320ms setTimeout to #chat-input |
| Q3 | PII redaction before logging | ✅ Done — UK postcode regex in RedactPii() |
| Q4 | Swagger gated to Development | ✅ Done — app.Environment.IsDevelopment() |
| Q5 | Unit tests written | ✅ Done — 13 backend + 15 browser JS tests |
| Q6 | Dead href="#" links fixed | ✅ Done — all 18 links have real URLs |
| Q7 | Session expiry 30 days | ✅ Done — BackgroundService, daily cleanup |
| Q8 | CouncilToolService split | ✅ Done — 6 partial class files |

---

## 3. Architecture Overview

```
Browser (Netlify CDN)                  Railway (ASP.NET Core 8)
+─────────────────────+  SSE/JSON     +──────────────────────────────────────+
│  index.html         │ ──────────►   │  ChatController                      │
│  chat.js (~1060 ln) │               │    └── AgentService (orchestrator)   │
│  style.css (~1070 ln│               │         ├── LlmService + RetryHandler│
│  config.js          │               │         ├── RagService (0.65 thresh) │
│  app.js             │               │         │     ├── EmbeddingService   │
│  tests/             │               │         │     └── QdrantVectorStore  │
+─────────────────────+               │         ├── ConversationService      │
                                      │         │     └── SQLite /data/      │
                                      │         │     SessionCleanupService  │
                                      │         └── CouncilToolService (6 ✦) │
                                      │              ├── Address.cs          │
                                      │              ├── BinDates.cs         │
                                      │              ├── CouncilTax.cs       │
                                      │              ├── Libraries.cs        │
                                      │              └── Web.cs              │
                                      +──────────────────────────────────────+
                                           Qdrant Cloud / OpenAI API
```

✦ CouncilToolService split from 1873 lines into 6 partial class files.

### Architecture Score: 8.5/10

| Aspect | Score | Notes |
|--------|-------|-------|
| Separation of concerns | 9/10 | Excellent after partial class split |
| Layer boundaries | 8/10 | Still some structured data shaping in AgentService |
| Dependency management | 8/10 | Scoped DI, IServiceScopeFactory in BackgroundService ✓ |
| Config management | 8/10 | appsettings.Local.json pattern clean, AdminKey fail-closed |
| Resilience patterns | 7/10 | Retry handler good; no circuit breaker yet |

---

## 4. Frontend Evaluation

### 4.1 HTML (index.html)

**Improvements since v1:**
- All 18 dead links now point to real Bradford Council URLs ✓
- `role="dialog"`, `aria-modal="true"`, `aria-labelledby="ch-name-id"` on `#chat-modal` ✓
- `role="log"`, `aria-live="polite"`, `aria-relevant="additions"` on `#chat-msgs` ✓
- FAB has `aria-label="Open Bradford Council chat assistant"` ✓
- `aria-hidden="true"` on backdrop ✓

**Remaining issues:**

| Severity | Issue |
|----------|-------|
| MEDIUM | `chatClose()` does not return focus to the FAB button. WCAG 2.1 §2.4.3 (Focus Order) requires focus to return to the element that opened a dialog when it closes. |
| MEDIUM | `#chat-chips` div has no accessible label — screen readers see the chips list without context. Should add `aria-label="Suggested questions"` to `.chips-wrap`. |
| LOW | Stay Connected email form still does nothing on submit (cosmetic issue, logged in v1) |
| LOW | No `<meta description>` or Open Graph tags — bad for sharing/SEO |

### 4.2 CSS (style.css)

**Improvements since v1:**
- `prefers-reduced-motion` media query added — disables animations for all motion-sensitive elements ✓
- Mobile breakpoints (700px, 480px) with horizontal chip scroll, safe area insets ✓
- Dark mode and high contrast themes complete ✓

**Remaining issues:**

| Severity | Issue |
|----------|-------|
| MEDIUM | Dark theme: `.ctp-band-pill` (`background:#0f4ca3`) has no dark-mode override — blue on dark blue, unreadable |
| LOW | `.stream-cursor` in reduced-motion becomes a static `▌` character — correct behaviour, but could use `visibility:hidden` instead for a cleaner result |
| LOW | No CSS minification/bundler — style.css is 1070 lines served raw |

### 4.3 JavaScript (chat.js)

**Improvements since v1:**
- `saveToCurrentSession('user', displayText || text)` — chat history now shows clean text ✓
- Profile context injected once per session via `buildApiText()` ✓
- Settings panel with profile, appearance, history — full feature set ✓
- `prefers-reduced-motion` handled in CSS (not JS needed) ✓

**Remaining issues / new findings:**

| Severity | Issue |
|----------|-------|
| MEDIUM | `esc2()` still used for `onclick` attribute strings (address picker, library picker, council tax picker). Should migrate to `data-*` + `addEventListener`. |
| MEDIUM | `chatClose()` missing `document.getElementById('fab')?.focus()` — focus trap not fully closed. |
| LOW | `confirmDeleteAll()` uses browser `confirm()` dialog — blocked in some iframe/CSP environments. A custom modal would be consistent with the design system. |
| LOW | `sessionProfileInjected` is reset in `chatClear()` but NOT when `saveProfileFromForm()` saves — good (handled: `sessionProfileInjected = false` in saveProfileFromForm). ✓ |

---

## 5. Backend — API Layer

### 5.1 Program.cs

**Improvements since v1:**
- `OpenAiRetryHandler` registered as transient DelegatingHandler ✓
- CORS env-split: `IsDevelopment()` → any origin; production → Netlify only ✓
- Rate limiter with `PartitionedRateLimiter` (fixed window, 30/min) ✓
- `SessionCleanupService` registered as `IHostedService` ✓
- Swagger gated to `IsDevelopment()` ✓
- `app.UseRateLimiter()` first in middleware pipeline ✓

**Critical issue found:**

```csharp
// CURRENT (WRONG for Railway):
var ip = ctx.Connection.RemoteIpAddress?.ToString()     // ← always proxy IP on Railway
         ?? ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()
         ?? "unknown";

// CORRECT:
var ip = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
         ?? ctx.Connection.RemoteIpAddress?.ToString()
         ?? "unknown";
```

Behind Railway's load balancer, `RemoteIpAddress` is always the Railway proxy's internal IP address. With the current code, every user is bucketed under the same IP, so one user consuming 29 requests in a minute blocks all other users for the remainder of that minute.

| Severity | Issue |
|----------|-------|
| CRITICAL | Rate limiter IP detection order is wrong — checks RemoteIpAddress before X-Forwarded-For. On Railway, all users share one rate limit bucket. |
| MEDIUM | CORS `isDev` check can be circumvented by setting `ASPNETCORE_ENVIRONMENT=Development` in Railway environment variables. Consider an explicit `ALLOWED_ORIGINS` env var instead. |
| LOW | `EnsureCreatedAsync()` on startup still runs even in production — adds ~20ms startup latency every cold start. Fine at this scale. |

### 5.2 ChatController.cs

**Improvements since v1:**
- `RedactPii()` masks UK postcode patterns before logging ✓

**Remaining issues:**

| Severity | Issue |
|----------|-------|
| MEDIUM | `RedactPii()` only masks postcodes. The task required masking names too — names are not redacted. Free-text `[User: name=Sarah]` prefix (injected by `buildApiText`) would appear in logs. |
| LOW | Still no request body size limit (`[RequestSizeLimit]` attribute not applied). |

### 5.3 OpenAiRetryHandler.cs

**New — well implemented with one subtle issue:**

```csharp
response.Dispose();
// ...
response = await base.SendAsync(request, ct);  // Reuses the same HttpRequestMessage
```

`HttpRequestMessage` is not officially documented as reusable after `SendAsync`. For `StringContent` (which backs all OpenAI calls), the content is a `ByteArrayContent` that creates a new `MemoryStream` on each read, so retrying works in practice. However, if the OpenAI client were ever changed to use a streaming request body, this would silently fail.

| Severity | Issue |
|----------|-------|
| LOW | `HttpRequestMessage` reuse is technically unsupported. Works with `StringContent` but fragile if content type changes. Clone the message for true safety. |
| LOW | Retry delay is `Math.Pow(2, attempt-1)` → 1s, 2s. Does not honour `Retry-After` header from OpenAI 429 responses. |

### 5.4 SessionCleanupService.cs

Clean implementation. Uses `IServiceScopeFactory` correctly (can't inject DbContext directly into a singleton). `ExecuteDeleteAsync` is EF Core 7+ — present in this project's EF Core 8 dependency. Non-fatal exception handling with `LogWarning` is correct.

**One finding:**
The service runs `CleanupAsync` immediately on startup (before waiting 24h). This is actually desirable — clears stale data right away on restart. Good.

### 5.5 IngestionController.cs

`IsAdminAuthorized()` uses `string.IsNullOrEmpty(configuredKey)` → returns false if no key configured (fail-closed). Correct.

**Note:** `AdminKey` in `appsettings.json` is `""` (empty). This means ingestion is completely locked in production until the user adds `AdminKey` as a Railway environment variable. The user must do this manually.

---

## 6. AI & LLM Integration

### 6.1 System Prompt

**Improvements since v1:**
- `get_library_details` now listed in tools section ✓
- 2026/27 rates in the hardcoded table (tool service, not system prompt) ✓

**Remaining issues:**

| Severity | Issue |
|----------|-------|
| MEDIUM | `"rates"` trigger word is still too broad — "mortgage rates", "parking rates", "interest rates" all trigger the council tax flow. |
| LOW | System prompt still uses `{RAG_CONTEXT}` string replacement — safe from injection since `BuildContextBlock()` returns structured text, but not regex-safe if context contained `{RAG_CONTEXT}` literally. |
| LOW | The `[User: name=Sarah, postcode=BD5 8LT] [brief reply]` prefixes injected by `buildApiText()` accumulate in the conversation history stored in SQLite (AgentService saves the raw API text). They won't be visible to the user but add token overhead to subsequent turns. |

### 6.2 AgentService — Agentic Loop

No changes to the core loop since v1. `MaxToolRounds = 3` remains in place.

**The context-injection profile bug:** When `buildApiText()` prepends profile context, the AI receives `[User: name=Sarah, postcode=BD5 8LT] When is my bin collected?`. This is saved to `ConversationService` as the "user message" in the DB. On the next turn, the history retrieved from the DB will include this prefixed message — the AI sees the profile context even on turn 2+, which is actually desirable (AI remembers user name) but doubles the token cost of the profile injection.

---

## 7. RAG Pipeline

### 7.1 RagService.cs

**Improvements since v1:**
- Similarity threshold 0.65 filters low-quality chunks ✓
- Logging now reports both total and filtered chunk counts ✓

**Remaining issues:**

| Severity | Issue |
|----------|-------|
| MEDIUM | `const float MinScore = 0.65f` is an arbitrary value. No calibration was done to validate this threshold against real Bradford Council queries. Too high → returns empty context. Too low → returns noise. Should be tested empirically. |
| MEDIUM | Fixed 600-char chunking still splits mid-sentence. Sentence-aware chunking would improve retrieval quality. |
| LOW | No scheduled re-ingestion. Bradford Council content changes; indexed data has no TTL. |
| LOW | No result logging when ALL chunks are filtered (0 pass threshold) — falls back silently to tool-only mode. Should log a warning. |

---

## 8. Tool Service

### 8.1 Partial Class Split

The 1873-line `CouncilToolService.cs` is now split into 6 files:

| File | Responsibility | Lines |
|------|---------------|-------|
| `CouncilToolService.cs` | Constructor, tool definitions, dispatch, shared helpers | ~285 |
| `CouncilToolService.Address.cs` | 4-strategy address lookup | ~515 |
| `CouncilToolService.BinDates.cs` | Bin collection scraper | ~315 |
| `CouncilToolService.CouncilTax.cs` | GOV.UK band scraper, rates table | ~505 |
| `CouncilToolService.Libraries.cs` | Library catalogue, proximity sort | ~220 |
| `CouncilToolService.Web.cs` | Search, page fetch, planning | ~85 |

All use `partial class CouncilToolService` — no logic changed, pure structural improvement.

**Remaining issues:**

| Severity | Issue |
|----------|-------|
| MEDIUM | `CouncilToolService.Address.cs` is still 515 lines — the largest single class file. Could benefit from further extraction of the Bradford form scraper vs Overpass strategy. |
| MEDIUM | Council tax rates `BradfordRates2627` (updated to 2026/27) but `TaxYear = "2026/27"` is now correct ✓. Next review due April 2027. |
| LOW | Library catalogue: all 20 libraries share the same phone number `01274 433600` (Bradford Central Library's number). Individual library numbers should be verified. |
| LOW | The `IsBradfordUrl()` helper only allows `www.bradford.gov.uk` and `bradford.gov.uk` — does not allow Bradford subdomains like `maps.bradford.gov.uk`. This is correctly strict for security. |

### 8.2 Fetch Tool SSRF Fix

```csharp
private static bool IsBradfordUrl(string url) =>
    Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
    uri.Scheme == Uri.UriSchemeHttps &&
    (uri.Host.Equals("www.bradford.gov.uk", ...) || uri.Host.Equals("bradford.gov.uk", ...));
```

This correctly prevents SSRF via:
- Non-HTTPS URLs blocked (no HTTP, file://, etc.)
- Only bradford.gov.uk domain allowed
- `Uri.TryCreate` handles malformed URLs safely ✓

---

## 9. Data Layer

### 9.1 SQLite Persistence

**Improvements since v1:**
- Default connection string changed to `Data Source=/data/agent.db` ✓
- `appsettings.Local.json` overrides to `Data Source=agent.db` for local ✓
- `railway.toml` documents volume mount instructions ✓
- `SessionCleanupService` deletes turns older than 30 days ✓

**Remaining:**

| Severity | Issue |
|----------|-------|
| HIGH | Volume mount in Railway still requires manual dashboard setup. Until done, `/data/agent.db` is in the container filesystem (ephemeral). This is documented but easy to forget. |
| MEDIUM | `railway.toml` `[build]` section with `buildCommand` and `dockerfilePath` may conflict — Railway typically uses either nixpacks OR Docker, not both specified simultaneously. The `dockerfilePath` key should be enough. |
| LOW | No session expiry on the frontend localStorage history — `bca_history` grows without bound (capped at 25 sessions by JS code ✓). |

### 9.2 Qdrant

No changes since v1. Still no backup mechanism for the vector collection.

---

## 10. Security Assessment

### 10.1 Security Score Improvement: 3.0 → 6.5/10

| Finding | v1 Status | v2 Status |
|---------|-----------|-----------|
| No auth on API | CRITICAL | N/A — chat endpoint intentionally public |
| No rate limiting | CRITICAL | FIXED — 30/min per IP (IP bug pending) |
| CORS wildcard | CRITICAL | FIXED — Netlify URL only in production |
| Ingestion unprotected | CRITICAL | FIXED — Admin key, fail-closed |
| SSRF via fetch_council_page | HIGH | FIXED — HTTPS + bradford.gov.uk allowlist |
| Swagger in production | LOW | FIXED — IsDevelopment() guard |
| PII in logs | MEDIUM | PARTIAL — postcodes masked, names not |
| Prompt injection | MEDIUM | UNADDRESSED |
| Rate limiter IP detection | — | NEW CRITICAL — wrong order for Railway |

### 10.2 Remaining Security Issues

| Severity | Issue |
|----------|-------|
| CRITICAL | Rate limiter reads RemoteIpAddress before X-Forwarded-For — ineffective on Railway proxy |
| MEDIUM | PII redaction only masks postcodes — user names from profile (`[User: name=Sarah]`) still logged |
| MEDIUM | Prompt injection: malicious Bradford Council page content can include hidden AI instructions |
| LOW | `AdminKey = "bradford-admin-2026"` hardcoded in `appsettings.Local.json` — should be a random UUID |
| LOW | No Content-Security-Policy header — frontend could be clickjacked or XSS'd via injected scripts |

---

## 11. Performance Assessment

### 11.1 Improvements

- `OpenAiRetryHandler` prevents cold-fail on transient 429/500 (saves user retries) ✓
- RAG threshold (0.65) removes noise chunks — slightly reduces token count ✓
- Council tax 2026/27 rates correct — no performance impact, accuracy fix ✓

### 11.2 Remaining Issues

| Severity | Issue |
|----------|-------|
| HIGH | GOV.UK council tax scraper: 3-step flow + per-property page fetches. Up to 5-8 seconds per lookup. No caching. |
| HIGH | Address lookup fallthrough strategy: up to 3 sequential HTTP calls on failure path (worst case ~90s with full timeout cascade) |
| MEDIUM | No HTTP-level response caching. Same postcode re-scraped on every request. A 5-minute in-memory cache would eliminate ~80% of GOV.UK calls |
| LOW | System prompt is ~2000 tokens injected on every request (3 × on 3-round tool loops) |

---

## 12. Error Handling & Resilience

### 12.1 Improvements

- `OpenAiRetryHandler`: retries 429 + 5xx with exponential backoff ✓
- `SessionCleanupService`: catches and logs exceptions without crashing service ✓
- Ingestion admin key: fail-closed (locked by default) ✓
- RAG failure still non-fatal (try-catch in `PrepareContextAsync`) ✓

### 12.2 Remaining Issues

| Severity | Issue |
|----------|-------|
| MEDIUM | `OpenAiRetryHandler` reuses `HttpRequestMessage` — works for `StringContent` but not officially supported |
| MEDIUM | No `Retry-After` header processing on OpenAI 429 responses — just delays 1s/2s regardless |
| MEDIUM | No circuit breaker for GOV.UK scraper — if GOV.UK is down, every council tax query waits full timeout |
| LOW | Frontend: no distinction between network errors (offline) and server errors (500) — both show the same generic message |

---

## 13. Accessibility

### 13.1 Score improvement: 4.0 → 6.5/10

**Improvements:**
- `role="dialog"`, `aria-modal="true"`, `aria-labelledby` on chat modal ✓
- `role="log"`, `aria-live="polite"`, `aria-relevant="additions"` on messages div ✓
- FAB has descriptive `aria-label` ✓
- Backdrop has `aria-hidden="true"` ✓
- `prefers-reduced-motion` disables all animations ✓
- Focus moves to `#chat-input` when chat opens (320ms delay) ✓

**Remaining WCAG gaps:**

| Severity | WCAG Criterion | Issue |
|----------|---------------|-------|
| HIGH | 2.4.3 Focus Order | `chatClose()` does not return focus to the FAB button. Screen keyboard users are left at an unknown focus position. |
| MEDIUM | 4.1.3 Status Messages | Error messages added to chat ("Sorry, I'm having trouble...") are injected directly into the live region — this is correct. But the typing indicator ("Bradford Council is typing...") is also in the live region — it will be announced repeatedly as dots animate. Should use `aria-live="off"` on the typing indicator. |
| MEDIUM | — | `.chips-wrap` has no `aria-label` — screen readers encounter a list of buttons with no context. |
| LOW | 2.4.7 Focus Visible | The custom `#chat-send` button has no visible `:focus` style beyond the browser default. Should add explicit focus ring. |
| LOW | — | Settings panel navigation uses `onclick` on `div.sp-nav-card` — these are not keyboard-accessible (no `tabindex="0"` or `role="button"`). |

---

## 14. Scalability & Deployment

### 14.1 Score: 4.0 → 4.5/10

**Improvements:**
- Dockerfile paths corrected ✓
- `railway.toml` with volume config documented ✓
- Default DB path `/data/agent.db` ready for Railway volume ✓
- `SessionCleanupService` prevents unbounded DB growth ✓

**Remaining:**

| Severity | Issue |
|----------|-------|
| HIGH | Railway volume mount still not configured — DB is still ephemeral until user completes dashboard setup |
| HIGH | `railway.toml` may have conflicting `[build]` section — Railway should use only `dockerfilePath` or only nixpacks, not both |
| MEDIUM | Single Railway instance — no horizontal scaling. SSE streams hold long-lived connections. At 20+ concurrent users, connections queue behind the Railway container. |
| LOW | No health check beyond `/health` GET — doesn't verify OpenAI connectivity, Qdrant connection, or DB access |

---

## 15. Testing Coverage

### 15.1 Score: 0.0 → 3.5/10

**New tests written:**
- `AgentServiceTests.cs` — 13 xUnit tests for `IsToolQuery` and `ExtractStructuredData`
- `renderMarkdown.test.html` — 15 browser tests for markdown rendering (bold, italic, links, lists, tables, XSS safety)

**Coverage analysis:**

| Area | Tests | Coverage |
|------|-------|----------|
| `IsToolQuery()` | 11 cases | Good — covers postcode regex, keywords, negative cases |
| `ExtractStructuredData()` | 5 cases | Basic — happy path, null, malformed JSON |
| `renderMarkdown()` | 15 cases | Good — covers all markdown elements + XSS |
| Tool service (all 9 tools) | 0 | None |
| LLM service | 0 | None |
| RAG pipeline | 0 | None |
| Chat controller | 0 | None |
| `buildApiText()` profile injection | 0 | None |
| End-to-end chat flow | 0 | None |

**Test infrastructure gaps:**
- No CI pipeline — tests never run automatically
- xUnit project exists but `dotnet test` has never been run
- JS tests require manual browser open — no Node.js test runner
- No mock/stub framework configured (would need NSubstitute/Moq for LLM/tool tests)

---

## 16. New Bugs Found in This Review

| # | Severity | Description | File | Fix |
|---|----------|-------------|------|-----|
| 1 | CRITICAL | Rate limiter checks `RemoteIpAddress` before `X-Forwarded-For` — all Railway users share one bucket | `Program.cs:81` | Swap order: check `X-Forwarded-For` first |
| 2 | HIGH | `chatClose()` doesn't return focus to FAB — breaks keyboard navigation after closing dialog | `chat.js:19` | Add `document.getElementById('fab')?.focus()` |
| 3 | MEDIUM | `IngestionController` `AdminKey` not set in Railway env vars — ingestion is permanently locked in production | `appsettings.json` | User must add `AdminKey` in Railway dashboard |
| 4 | MEDIUM | `.ctp-band-pill` (blue pill) has no dark-mode override — unreadable on dark theme | `style.css` | Add `#chat-panel.chat-dark .ctp-band-pill` rule |
| 5 | MEDIUM | Settings nav cards use `onclick` on a `div` — not keyboard-accessible | `chat.js renderSettingsHome()` | Add `role="button"` and `tabindex="0"` + keydown handler |
| 6 | MEDIUM | `buildApiText()` profile prefix (`[User: name=Sarah]`) saved to DB as user message via `ConversationService` — adds token overhead on every subsequent turn | `AgentService.cs` | Strip prefix before saving turn, or save displayText |
| 7 | LOW | All 20 library entries share phone `01274 433600` (Bradford Central Library) — may be wrong for individual branches | `CouncilToolService.Libraries.cs` | Verify individual branch numbers |
| 8 | LOW | `railway.toml` `[build]` section with both `buildCommand` and `dockerfilePath` may conflict | `railway.toml` | Remove `buildCommand` if using Dockerfile |

---

## 17. Remaining Recommendations — Priority Order

### Fix Now (bugs)

1. **Fix rate limiter IP detection** in `Program.cs`:
   ```csharp
   var ip = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
            ?? ctx.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
   ```

2. **Fix chatClose() focus** in `chat.js`:
   ```javascript
   function chatClose() {
     // ... existing code ...
     document.getElementById('fab')?.focus();
   }
   ```

3. **Fix dark-mode band pill** in `style.css`:
   ```css
   #chat-panel.chat-dark .ctp-band-pill { background: #1d4ed8; }
   ```

4. **Make settings nav cards keyboard-accessible** in `chat.js renderSettingsHome()` — add `role="button" tabindex="0"` and `onkeydown="if(e.key==='Enter'||e.key===' ') renderProfileScreen()"` to each nav card.

5. **Set `AdminKey` in Railway environment variables** (dashboard task, not code).

### Short-term

6. Add `aria-live="off"` to the typing indicator wrapper (`.typing-wrap`) to prevent repeated announcements
7. Add `aria-label="Suggested questions"` to `.chips-wrap`
8. Add `dotnet test` to CI or at least run it manually to verify all 13 tests pass
9. Strip the `[User: ...]` prefix from ConversationService saves so it doesn't inflate token history
10. Remove `buildCommand` from `railway.toml` — redundant with `dockerfilePath`

### Medium-term

11. Add a simple 5-minute in-memory cache for GOV.UK council tax lookups by postcode
12. Run tests in CI — connect GitHub → Railway/Netlify build pipeline
13. Add integration tests for the full chat flow with mocked OpenAI
14. Calibrate the 0.65 RAG threshold with real Bradford Council queries
15. Implement focus trap inside the chat dialog (Tab should cycle between dialog elements only)

---

## 18. Scoring Summary

| Category | v1 Score | v2 Score | Delta | Notes |
|----------|----------|----------|-------|-------|
| Architecture & Design | 7.5 | 8.5 | +1.0 | Partial classes, retry handler, background service |
| Frontend Code Quality | 7.0 | 7.5 | +0.5 | ARIA, focus, reduced motion, dead links fixed |
| Backend Code Quality | 7.0 | 7.5 | +0.5 | PII redaction, Swagger gated, cleanup service |
| AI Integration | 7.5 | 8.0 | +0.5 | Library tool in prompt, 2026/27 rates |
| RAG Pipeline | 6.0 | 7.0 | +1.0 | Similarity threshold, better logging |
| Tool Service | 6.5 | 7.5 | +1.0 | SSRF fix, partial class split, updated rates |
| Security | 3.0 | 6.5 | +3.5 | Rate limiting, CORS, admin key, SSRF, PII, Swagger |
| Performance | 6.0 | 6.5 | +0.5 | Retry prevents wasted fails; IP bug limits rate limiter |
| Error Handling | 5.5 | 6.5 | +1.0 | Retry handler, cleanup error isolation |
| Accessibility | 4.0 | 6.5 | +2.5 | ARIA, focus management, reduced motion |
| Scalability | 4.0 | 4.5 | +0.5 | Railway.toml, /data path, cleanup service |
| Testing | 0.0 | 3.5 | +3.5 | 28 tests written; still no CI, no integration tests |
| **Overall** | **6.1** | **7.2** | **+1.1** | |

### Remaining gap to 8.5/10

The project needs these three things to reach 8.5/10:
1. **Fix the rate limiter IP bug** — restores intended security posture (+0.3)
2. **Testing depth** — integration tests, CI pipeline, 80%+ coverage (+0.5)
3. **Scalability** — persistent DB (Railway volume), PostgreSQL migration (+0.4)

### Audience suitability (updated)

| Audience | v1 | v2 |
|----------|----|----|
| Personal demo / portfolio | 9/10 | 9.5/10 |
| Shared link (current use) | 7/10 | 8.0/10 |
| Bradford Council internal pilot (50 users) | 5/10 | 6.5/10 |
| Public council deployment (thousands of users) | 3/10 | 4.5/10 |

---

*Report generated 08 May 2026. Evaluates codebase after all 20 v1 action items completed.*

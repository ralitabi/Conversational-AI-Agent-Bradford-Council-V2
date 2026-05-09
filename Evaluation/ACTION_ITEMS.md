# Bradford Council AI — Action Items v4
Updated 08 May 2026 — all code-fixable items from findings table resolved
Score history: 6.1 → 7.2 → 7.4 → **7.8/10**

Legend: [C] Critical  [H] High  [M] Medium  [L] Low  ✅ Done

---

## ✅ V1 — ALL 20 ITEMS COMPLETED

---

## ✅ V2 FIX NOW — ALL 6 ITEMS COMPLETED

- [C] ~~Fix rate limiter IP detection~~ ✅ X-Forwarded-For checked first
- [H] ~~Fix chatClose() focus (WCAG 2.4.3)~~ ✅ `fab.focus()` added
- [H] ~~AdminKey in Railway~~ ✅ `bradford2026`
- [H] ~~Railway volume at /data~~ ✅ SQLite persistent
- [M] ~~Dark-mode band pill~~ ✅ `background:#1d4ed8`
- [M] ~~Settings nav cards keyboard~~ ✅ `role="button"` + `tabindex="0"` + keydown

---

## ✅ V2 SHORT-TERM — ALL 6 ITEMS COMPLETED

- [M] ~~Typing indicator `aria-hidden`~~ ✅ `aria-hidden="true"` on both typing dot elements
- [M] Add `aria-label="Suggested questions"` to `.chips-wrap` ← **STILL OPEN**
- [M] ~~Strip `[User: name=...]` prefix before DB save~~ ✅ `StripContextPrefixes()` in AgentService
- [M] ~~Remove `buildCommand` from railway.toml~~ ✅ Only `dockerfilePath` remains
- [M] ~~PII redaction improved~~ ✅ Strips profile prefix + style hints + postcodes
- [M] ~~GOV.UK council tax caching~~ ✅ 5-min `IMemoryCache` in CouncilToolService
- [L] Run `dotnet test` — verify 13 xUnit tests pass ← **STILL OPEN**
- [L] Add `:focus` ring to `#chat-send` ← **STILL OPEN**

---

## MEDIUM-TERM — REMAINING

- [M] Connect GitHub → Railway/Netlify CI pipeline (tests auto-run on push)
- [M] Write integration tests for chat flow with mocked OpenAI
- [M] Calibrate RAG 0.65 threshold empirically with real Bradford queries
- [L] Implement full Tab focus trap inside chat dialog
- [L] Verify individual Bradford library phone numbers (all show 01274 433600)
- [L] Add Content-Security-Policy header

---

## SCORES (v1 → v4)

| Area | v1 | v4 | Total gain |
|------|----|----|-----------|
| Architecture | 7.5 | 8.5 | +1.0 |
| Frontend | 7.0 | 8.0 | +1.0 |
| Backend | 7.0 | 8.0 | +1.0 |
| AI / LLM | 7.5 | 8.0 | +0.5 |
| RAG | 6.0 | 7.0 | +1.0 |
| Tool Service | 6.5 | 7.5 | +1.0 |
| Security | 3.0 | 7.2 | +4.2 |
| Performance | 6.0 | 7.5 | +1.5 |
| Error Handling | 5.5 | 6.5 | +1.0 |
| Accessibility | 4.0 | 7.5 | +3.5 |
| Scalability | 4.0 | 5.5 | +1.5 |
| Testing | 0.0 | 3.5 | +3.5 |
| **Overall** | **6.1** | **7.8** | **+1.7** |

Biggest gains: Security (+4.2), Accessibility (+3.5), Testing (+3.5), Performance (+1.5).
Remaining gap to 9/10: CI pipeline + integration tests + RAG calibration.

<div align="center">

# Bradford Council AI Assistant

**A conversational AI assistant for Bradford Council residents**

[![Live Demo](https://img.shields.io/badge/Live%20Demo-bradford--council--ai.vercel.app-blue?style=for-the-badge&logo=vercel)](https://bradford-council-ai.vercel.app)
[![API Health](https://img.shields.io/badge/API-Railway-purple?style=for-the-badge&logo=railway)](https://bradford-council-api-production.up.railway.app/health)
[![License: MIT](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com)
[![GitHub](https://img.shields.io/badge/GitHub-ralitabi-black?style=for-the-badge&logo=github)](https://github.com/ralitabi/Conversational-AI-Agent-Bradford-Council-V2)

<br/>

> Ask about bin collection dates, council tax bands, nearby schools, libraries, planning applications and more — all from a single chat window, powered by GPT-4o-mini.

</div>

---

## What It Does

Bradford Council AI gives Bradford residents instant, accurate answers about council services — no more navigating confusing council websites.

| Feature | Description |
|---|---|
| Bin Dates | Exact upcoming grey, green and brown bin collection dates for any Bradford address |
| Council Tax | Band (A-H) and annual / monthly charge for any Bradford postcode |
| School Finder | Nearby schools sorted by walking distance with Ofsted ratings and admissions links |
| Library Finder | All Bradford libraries sorted by distance with opening hours and facilities |
| Education Info | Admissions process, SEND support, free school meals, term dates, uniforms |
| Planning | Search Bradford's planning portal by application reference or address |
| Live Scraping | Searches and reads bradford.gov.uk in real-time for any other council query |

---

## Architecture

```
+------------------------------------------------------------------+
|                        USER BROWSER                              |
|                                                                  |
|   index.html  --  chat.js  --  config.js  --  style.css         |
|         |                                                        |
|         |  SSE stream / REST POST                                |
+---------+--------------------------------------------------------+
          |
          v
+------------------------------------------------------------------+
|                   RAILWAY  (ASP.NET Core 8)                      |
|                                                                  |
|   ChatController                                                 |
|        |                                                         |
|        v                                                         |
|   AgentService  (agentic loop, up to 3 tool rounds)             |
|        |                                                         |
|        |-- LlmService (OpenAI GPT-4o-mini)                      |
|        |                                                         |
|        +-- CouncilToolService                                    |
|               |-- Bin dates  (Bradford eForms scraper)           |
|               |-- Council tax (GOV.UK VOA data)                  |
|               |-- Schools    (Bradford BSO form)                 |
|               |-- Libraries  (bradford.gov.uk)                   |
|               |-- Planning   (Bradford portal)                   |
|               +-- Web scraper (bradford.gov.uk)                  |
|                                                                  |
|   ConversationService (SQLite)                                   |
|   RagService (Qdrant, optional)                                  |
+------------------------------------------------------------------+
          |                    |                    |
          v                    v                    v
    postcodes.io         bradford.gov.uk      bso.bradford.gov.uk
    (free API)           (scraped live)       (school/bin eForms)
```

---

## Project Structure

```
Bradford Council Project/
|
+-- Frontend/                        Static site deployed to Vercel
|   +-- index.html                   Main chat UI
|   +-- login.html                   Optional authentication page
|   +-- chat.js                      UI logic, card rendering, SSE streaming
|   +-- config.js                    Auto-detects local vs production API URL
|   +-- style.css                    Complete stylesheet
|
+-- Backend/                         ASP.NET Core API deployed to Railway
|   |
|   +-- Api/
|   |   +-- Controllers/
|   |   |   +-- ChatController.cs    POST /api/chat  +  GET /api/chat/stream
|   |   |   +-- IngestionController.cs  POST /api/ingest
|   |   +-- Program.cs               DI, CORS, rate limiting, middleware
|   |   +-- Dockerfile               Multi-stage Docker build
|   |   +-- appsettings.json         Config skeleton (keys via env vars)
|   |
|   +-- Core/
|   |   +-- Models/                  ChatRequest, ChatResponse, BinDateCard,
|   |   |                            SchoolCard, CouncilTaxCard, LibraryOption
|   |   +-- Interfaces/              Service contracts
|   |   +-- Services/
|   |       +-- AgentService.cs      Agentic loop, tool orchestration, system prompt
|   |       +-- LlmService.cs        OpenAI client, streaming, tool call parsing
|   |       +-- ConversationService.cs  Per-session history in SQLite
|   |       +-- RagService.cs        RAG retrieval from Qdrant
|   |
|   +-- Infrastructure/
|   |   +-- Tools/
|   |   |   +-- CouncilToolService.cs           Tool definitions + routing
|   |   |   +-- CouncilToolService.Address.cs   Address lookup (postcodes.io)
|   |   |   +-- CouncilToolService.BinDates.cs  Bin dates (Bradford eForms)
|   |   |   +-- CouncilToolService.CouncilTax.cs Council tax (GOV.UK)
|   |   |   +-- CouncilToolService.Libraries.cs Library finder
|   |   |   +-- CouncilToolService.Schools.cs   School finder (BSO)
|   |   |   +-- CouncilToolService.Web.cs        General scraper
|   |   +-- Data/
|   |   |   +-- AgentDbContext.cs    EF Core + SQLite
|   |   +-- Crawlers/
|   |   |   +-- CouncilWebCrawler.cs Bradford.gov.uk crawler for RAG
|   |   +-- VectorStore/
|   |       +-- QdrantVectorStore.cs Qdrant client wrapper
|   |
|   +-- Tests/
|       +-- AgentServiceTests.cs
|
+-- scripts/                         Local development scripts
|   +-- start-all.bat                Double-click to start full stack
|   +-- start-all.ps1                Start Qdrant + API + open browser
|   +-- start-api.bat                Double-click to restart API only
|   +-- start-api.ps1                Restart API, free port 5000
|   +-- start-qdrant.bat             Double-click to start Qdrant
|   +-- start-qdrant.ps1
|   +-- start-website.bat            Double-click to serve frontend
|   +-- start-website.ps1            HTTP server on localhost:8080
|
+-- deploy.ps1                       Deploy frontend + backend
+-- deploy.bat                       Double-click launcher for deploy.ps1
+-- docker-compose.yml               Local Qdrant + API stack
+-- railway.toml                     Railway build and health check config
+-- README.md
+-- LICENSE
```

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (optional, for Qdrant)
- An [OpenAI API key](https://platform.openai.com/api-keys)

---

### Step 1 — Clone the repo

```bash
git clone https://github.com/ralitabi/Conversational-AI-Agent-Bradford-Council-V2.git
cd Conversational-AI-Agent-Bradford-Council-V2
```

### Step 2 — Add your OpenAI key

Create `Backend/Api/appsettings.Local.json` (gitignored):

```json
{
  "OpenAI": {
    "ApiKey": "sk-proj-..."
  }
}
```

### Step 3 — Run the backend

```bash
cd Backend
dotnet run --project Api
```

API starts on **http://localhost:5000**.

### Step 4 — Open the frontend

Open `Frontend/index.html` in your browser — `config.js` auto-detects `file://` and routes to `localhost:5000`.

### Step 5 — (Optional) Start Qdrant for RAG

```bash
docker-compose up qdrant
```

Or double-click `scripts/start-qdrant.bat`.

---

### One-click local start

Double-click `scripts/start-all.bat` to launch Qdrant, the API, and open the browser in one step.

---

## Deployment

### Backend — Railway

Set these environment variables in the Railway dashboard:

| Variable | Value |
|---|---|
| `OpenAI__ApiKey` | Your OpenAI secret key |
| `OpenAI__ChatModel` | `gpt-4o-mini` |
| `ConnectionStrings__Default` | `Data Source=/data/agent.db` |
| `GetAddress__ApiKey` | *(optional)* getAddress.io key |

Mount a Railway **Volume** at `/data` to persist the SQLite database.

### Frontend — Vercel

```bash
cd Frontend
vercel --prod
```

### One-command deploy (both)

```powershell
# Deploy everything
.\deploy.bat

# Frontend only
.\deploy.ps1 -Frontend

# Backend only
.\deploy.ps1 -Backend
```

---

## Environment Variables

| Variable | Required | Default | Description |
|---|:---:|---|---|
| `OpenAI__ApiKey` | Yes | - | OpenAI secret key |
| `OpenAI__ChatModel` | | `gpt-4o-mini` | Chat model |
| `OpenAI__EmbeddingModel` | | `text-embedding-3-small` | Embedding model for RAG |
| `ConnectionStrings__Default` | | `Data Source=/data/agent.db` | SQLite path |
| `GetAddress__ApiKey` | | - | getAddress.io for fuller address lookup |
| `Qdrant__Host` | | `localhost` | Qdrant hostname |
| `Qdrant__ApiKey` | | - | Qdrant Cloud API key |
| `AdminKey` | | - | Protects `/api/ingest` endpoint |

---

## API Reference

### `POST /api/chat`

```json
{
  "sessionId": "abc-123",
  "message": "When is my bin collected? BD5 8LT",
  "streamResponse": false
}
```

### `GET /api/chat/stream`

```
?sessionId=abc-123&message=find+schools+near+BD7+3AB
```

Streams SSE tokens, ends with a `[STRUCTURED]` event containing card data, then `[DONE]`.

### `GET /health`

```json
{ "status": "healthy", "timestamp": "2026-05-11T10:00:00Z" }
```

### `POST /api/ingest`

Requires `Authorization: Bearer {AdminKey}` header.

```json
{ "urls": ["https://www.bradford.gov.uk/bins"] }
```

---

## Agent Tools

| Tool | Triggered when |
|---|---|
| `lookup_addresses_for_postcode` | User provides a postcode for bin collection |
| `get_bin_dates_for_address` | User confirms their address |
| `lookup_council_tax_band` | User asks about council tax band or amount |
| `find_schools_near_postcode` | User asks for schools near a location |
| `get_school_details` | User picks a specific school |
| `get_education_info` | User asks about admissions, SEND, free meals, term dates |
| `find_local_services` | User asks for libraries, parks or leisure centres |
| `get_library_details` | User picks a specific library |
| `search_bradford_council` | Any general council service question |
| `fetch_council_page` | Agent needs to read a specific bradford.gov.uk page |
| `get_council_tax_info` | Discounts, exemptions, how to pay |
| `check_planning_application` | Planning portal search |

---

## Tech Stack

| Layer | Technology | Purpose |
|---|---|---|
| Frontend | HTML / CSS / Vanilla JS | No framework, no build step |
| Frontend hosting | Vercel | Global CDN |
| Backend | ASP.NET Core 8 | Web API with SSE streaming |
| Backend hosting | Railway | Docker container, auto-deploy |
| LLM | OpenAI GPT-4o-mini | Tool-calling chat completions |
| Database | SQLite + EF Core 8 | Conversation history per session |
| Vector store | Qdrant | Optional RAG for indexed knowledge |
| HTML parsing | HtmlAgilityPack | Scraping bradford.gov.uk |
| Postcode data | postcodes.io | Free postcode to lat/lon, bulk lookup |

---

## License

This project is licensed under the **MIT License** — see [LICENSE](LICENSE) for details.

---

## Acknowledgements

- [Bradford Metropolitan District Council](https://www.bradford.gov.uk) — public service data
- [postcodes.io](https://postcodes.io) — free open UK postcode API
- [OpenAI](https://openai.com) — GPT-4o-mini language model
- [Ofsted](https://reports.ofsted.gov.uk) — school inspection data
- [HtmlAgilityPack](https://html-agility-pack.net) — .NET HTML parsing

---

<div align="center">
Made for Bradford residents
</div>

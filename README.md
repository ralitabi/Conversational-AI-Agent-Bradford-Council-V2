<div align="center">

# 🏛️ Bradford Council AI Assistant

**A conversational AI assistant for Bradford Council residents**

[![Live Demo](https://img.shields.io/badge/Live%20Demo-bradford--council--ai.vercel.app-blue?style=for-the-badge&logo=vercel)](https://bradford-council-ai.vercel.app)
[![API Health](https://img.shields.io/badge/API-Railway-purple?style=for-the-badge&logo=railway)](https://bradford-council-api-production.up.railway.app/health)
[![License: MIT](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com)

<br/>

> Ask about bin collection dates, council tax bands, nearby schools, libraries, planning applications and more — all from a single chat window, powered by GPT-4o-mini.

<br/>

![Chat Preview](https://bradford-council-ai.vercel.app)

</div>

---

## ✨ What It Does

Bradford Council AI is a full-stack chatbot that gives Bradford residents instant, accurate answers about council services — no more navigating confusing council websites.

| 🗑️ Bin Dates | Exact upcoming grey, green & brown bin collection dates for any Bradford address |
|---|---|
| 💷 Council Tax | Band (A–H) and annual / monthly charge for any Bradford postcode |
| 🏫 School Finder | Nearby schools sorted by walking distance, with Ofsted ratings and admissions links |
| 📚 Library Finder | All Bradford libraries sorted by distance, with opening hours and facilities |
| 🎓 Education Info | Admissions process, SEND support, free school meals, term dates, uniforms |
| 🏗️ Planning | Search Bradford's planning portal by application reference or address |
| 🌐 Live Scraping | Searches and reads `bradford.gov.uk` in real-time for any other council query |

---

## 🖥️ Screenshots

<table>
<tr>
<td align="center"><b>School Finder</b></td>
<td align="center"><b>Bin Collection Card</b></td>
<td align="center"><b>Council Tax Card</b></td>
</tr>
<tr>
<td>Nearby schools with Ofsted ratings and distances, sorted nearest first.</td>
<td>Exact upcoming collection dates for grey, green and brown bins.</td>
<td>Band and annual charge with full rate table and payment links.</td>
</tr>
</table>

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        USER BROWSER                             │
│                                                                 │
│   index.html  ──  chat.js  ──  config.js  ──  style.css        │
│         │                                                       │
│         │  SSE stream / REST POST                               │
└─────────┼───────────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────────┐
│                   RAILWAY  (ASP.NET Core 8)                     │
│                                                                 │
│   ChatController                                                │
│        │                                                        │
│        ▼                                                        │
│   AgentService  ──────────────────────────────────────────┐    │
│        │  agentic loop (up to 3 tool rounds)              │    │
│        │                                                  │    │
│        ▼                                                  ▼    │
│   LlmService                                    CouncilToolService
│   (OpenAI GPT-4o-mini)                          ├── Bin dates  │
│        │                                        ├── Council tax│
│        │  tool calls                            ├── Schools    │
│        └──────────────────────────────────────► ├── Libraries  │
│                                                 ├── Planning   │
│   ConversationService (SQLite)                  └── Web scraper│
│   RagService (Qdrant, optional)                               │
└─────────────────────────────────────────────────────────────────┘
          │                    │                    │
          ▼                    ▼                    ▼
    postcodes.io         bradford.gov.uk      bso.bradford.gov.uk
    (free API)           (scraped live)       (school/bin eForms)
```

---

## 📁 Project Structure

```
Bradford Council Project/
│
├── 📂 Frontend/                        Static site — deployed to Vercel
│   ├── index.html                      Main chat UI
│   ├── login.html                      Optional authentication page
│   ├── chat.js                         All UI logic, card rendering, SSE streaming
│   ├── config.js                       Auto-detects local vs production API URL
│   └── style.css                       Complete stylesheet (cards, bubbles, animations)
│
├── 📂 Backend/                         ASP.NET Core API — deployed to Railway
│   │
│   ├── 📂 Api/                         HTTP layer
│   │   ├── Controllers/
│   │   │   ├── ChatController.cs       POST /api/chat  +  GET /api/chat/stream
│   │   │   └── IngestionController.cs  POST /api/ingest  (RAG ingestion)
│   │   ├── Program.cs                  DI, CORS, rate limiting, middleware
│   │   ├── Dockerfile                  Multi-stage Docker build
│   │   └── appsettings.json            Config skeleton (real keys via env vars)
│   │
│   ├── 📂 Core/                        Domain logic — no external dependencies
│   │   ├── Models/                     ChatRequest, ChatResponse, BinDateCard,
│   │   │                               SchoolCard, CouncilTaxCard, LibraryOption…
│   │   ├── Interfaces/                 Service contracts (IAgentService, ILlmService…)
│   │   └── Services/
│   │       ├── AgentService.cs         Agentic loop, tool orchestration, system prompt
│   │       ├── LlmService.cs           OpenAI client, streaming, tool call parsing
│   │       ├── ConversationService.cs  Per-session history stored in SQLite
│   │       └── RagService.cs           RAG retrieval from Qdrant
│   │
│   ├── 📂 Infrastructure/              External integrations
│   │   ├── Tools/
│   │   │   ├── CouncilToolService.cs           Tool definitions + executor routing
│   │   │   ├── CouncilToolService.Address.cs   Address lookup  (postcodes.io)
│   │   │   ├── CouncilToolService.BinDates.cs  Bin dates       (Bradford eForms scraper)
│   │   │   ├── CouncilToolService.CouncilTax.cs Council tax    (GOV.UK VOA data)
│   │   │   ├── CouncilToolService.Libraries.cs Library finder  (bradford.gov.uk)
│   │   │   ├── CouncilToolService.Schools.cs   School finder   (BSO ASP.NET form)
│   │   │   └── CouncilToolService.Web.cs       General scraper (bradford.gov.uk)
│   │   ├── Data/
│   │   │   └── AgentDbContext.cs       EF Core + SQLite — conversation turns
│   │   ├── Crawlers/
│   │   │   └── CouncilWebCrawler.cs    Bradford.gov.uk crawler for RAG ingestion
│   │   └── VectorStore/
│   │       └── QdrantVectorStore.cs    Qdrant client wrapper for embeddings
│   │
│   └── 📂 Tests/
│       └── AgentServiceTests.cs        xUnit unit tests
│
├── deploy.ps1                          Deploy both frontend and backend
├── deploy.bat                          Double-click launcher for deploy.ps1
├── docker-compose.yml                  Local Qdrant + API development stack
├── start-all.ps1                       Start everything locally with one command
├── railway.toml                        Railway build + health check config
├── README.md                           This file
└── LICENSE                             MIT License
```

---

## 🚀 Getting Started (Local)

### Prerequisites

Before you begin, make sure you have:

- [**.NET 8 SDK**](https://dotnet.microsoft.com/download/dotnet/8.0) — to run the backend
- [**Node.js 18+**](https://nodejs.org/) — for the Vercel and Railway CLIs
- [**Docker Desktop**](https://www.docker.com/products/docker-desktop/) — optional, only needed for RAG/Qdrant
- An [**OpenAI API key**](https://platform.openai.com/api-keys)

---

### Step 1 — Clone the repo

```bash
git clone https://github.com/YOUR_USERNAME/bradford-council-ai.git
cd "bradford-council-ai"
```

### Step 2 — Add your OpenAI key

Create the file `Backend/Api/appsettings.Local.json` (it is gitignored — safe for real keys):

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

The API starts on **http://localhost:5000**. You should see:

```
info: Now listening on: http://localhost:5000
```

### Step 4 — Open the frontend

Open `Frontend/index.html` directly in your browser.
`config.js` automatically detects `file://` or `localhost` and routes to `http://localhost:5000`.

> No build step, no bundler — just open the file.

### Step 5 — (Optional) Start Qdrant for RAG

```bash
docker-compose up qdrant
```

Then ingest Bradford Council pages into the vector store:

```bash
curl -X POST http://localhost:5000/api/ingest \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ADMIN_KEY" \
  -d '{"urls": ["https://www.bradford.gov.uk/bins", "https://www.bradford.gov.uk/libraries"]}'
```

---

## ☁️ Deployment

### Backend → Railway

1. **Install the Railway CLI**
   ```bash
   npm install -g @railway/cli
   railway login
   ```

2. **Set environment variables** in the Railway dashboard:

   | Variable | Value |
   |---|---|
   | `OpenAI__ApiKey` | Your OpenAI secret key (`sk-proj-...`) |
   | `OpenAI__ChatModel` | `gpt-4o-mini` |
   | `ConnectionStrings__Default` | `Data Source=/data/agent.db` |
   | `GetAddress__ApiKey` | *(optional)* getAddress.io key |

3. **Mount a Railway Volume** at path `/data` to persist the SQLite database across redeploys.

4. **Deploy:**
   ```bash
   cd Backend
   railway up --service bradford-council-api
   ```

---

### Frontend → Vercel

1. **Install Vercel CLI**
   ```bash
   npm install -g vercel
   vercel login
   ```

2. **Deploy:**
   ```bash
   cd Frontend
   vercel --prod
   ```

   Your site is live at `https://YOUR-PROJECT.vercel.app`

---

### One-command deploy (both at once)

```powershell
# Deploy everything
.\deploy.bat

# Or from a terminal
.\deploy.ps1

# Frontend only
.\deploy.ps1 -Frontend

# Backend only
.\deploy.ps1 -Backend
```

---

## ⚙️ Environment Variables

| Variable | Required | Default | Description |
|---|:---:|---|---|
| `OpenAI__ApiKey` | ✅ | — | OpenAI secret key |
| `OpenAI__ChatModel` | | `gpt-4o-mini` | Chat completion model |
| `OpenAI__EmbeddingModel` | | `text-embedding-3-small` | Embedding model for RAG |
| `ConnectionStrings__Default` | | `Data Source=/data/agent.db` | SQLite connection string |
| `GetAddress__ApiKey` | | — | [getAddress.io](https://getaddress.io) for fuller address lookup |
| `Qdrant__Host` | | `localhost` | Qdrant server hostname |
| `Qdrant__GrpcPort` | | `6334` | Qdrant gRPC port |
| `Qdrant__ApiKey` | | — | Qdrant Cloud API key |
| `AdminKey` | | — | Bearer token to protect `/api/ingest` |

---

## 🔌 API Reference

### `POST /api/chat`
Non-streaming chat. Returns a full response once the agent finishes.

**Request**
```json
{
  "sessionId": "abc-123",
  "message": "When is my bin collected? BD5 8LT",
  "streamResponse": false
}
```

**Response**
```json
{
  "sessionId": "abc-123",
  "reply": "I found 14 addresses for BD5 8LT — please select yours below.",
  "addresses": [ { "number": 1, "line1": "1 School Road", "postcode": "BD5 8LT", ... } ],
  "binDates": null,
  "schools": null,
  "councilTaxInfo": null,
  "usedRag": false
}
```

---

### `GET /api/chat/stream`
Server-Sent Events stream. Tokens arrive in real-time, followed by a `[STRUCTURED]` event with card data.

```
?sessionId=abc-123&message=find+schools+near+BD7+3AB
```

**SSE events**
```
data: Here are the nearest schools...

data: [STRUCTURED]{"schools":[{"name":"Thornton Primary","ofstedRating":"Good",...}]}

data: [DONE]
```

---

### `GET /health`
```json
{ "status": "healthy", "timestamp": "2026-05-11T10:00:00Z" }
```

---

### `POST /api/ingest`
Crawl and ingest URLs into the Qdrant RAG store. Requires `Authorization: Bearer {AdminKey}` header.

```json
{ "urls": ["https://www.bradford.gov.uk/bins", "https://www.bradford.gov.uk/libraries"] }
```

---

## 🤖 Agent Tools

The AI agent autonomously decides which tools to call based on the user's message:

| Tool | Triggered when… |
|---|---|
| `lookup_addresses_for_postcode` | User gives a postcode for bin collection |
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
| `check_planning_application` | Planning portal search by reference or address |

---

## 🧰 Tech Stack

<table>
<tr><th>Layer</th><th>Technology</th><th>Purpose</th></tr>
<tr><td>Frontend</td><td>HTML / CSS / Vanilla JS</td><td>No framework, no build step — just files</td></tr>
<tr><td>Hosting (Frontend)</td><td>Vercel</td><td>Global CDN, free tier</td></tr>
<tr><td>Backend</td><td>ASP.NET Core 8</td><td>Web API with SSE streaming + rate limiting</td></tr>
<tr><td>Hosting (Backend)</td><td>Railway</td><td>Docker container, auto-deploy from CLI</td></tr>
<tr><td>LLM</td><td>OpenAI GPT-4o-mini</td><td>Chat completions with function/tool calling</td></tr>
<tr><td>Database</td><td>SQLite + EF Core 8</td><td>Lightweight conversation history per session</td></tr>
<tr><td>Vector store</td><td>Qdrant</td><td>Optional RAG for indexed council knowledge</td></tr>
<tr><td>HTML parsing</td><td>HtmlAgilityPack</td><td>Scraping bradford.gov.uk and eForms pages</td></tr>
<tr><td>Postcode data</td><td>postcodes.io</td><td>Free postcode → lat/lon, bulk lookups</td></tr>
</table>

---

## 📜 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgements

- [**Bradford Metropolitan District Council**](https://www.bradford.gov.uk) — public service data
- [**postcodes.io**](https://postcodes.io) — free, open UK postcode API
- [**OpenAI**](https://openai.com) — GPT-4o-mini language model
- [**Ofsted**](https://reports.ofsted.gov.uk) — school inspection data
- [**HtmlAgilityPack**](https://html-agility-pack.net) — .NET HTML parsing library

---

<div align="center">

Made for Bradford residents 🏙️

</div>

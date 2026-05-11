# Bradford Council AI Assistant

A conversational AI assistant for Bradford Council residents. Powered by GPT-4o-mini, it answers questions about council services, looks up real-time data (bin collection dates, council tax bands, nearby schools and libraries), and streams responses directly in the browser.

**Live demo:** https://bradford-council-ai.vercel.app

---

## Features

| Feature | Description |
|---|---|
| **Bin collection dates** | Scrapes Bradford's eForms system (`bso.bradford.gov.uk`) to return exact upcoming grey, green and brown bin dates for any Bradford address |
| **Council tax bands** | Looks up the council tax band (A–H) and annual/monthly charge for any Bradford postcode via GOV.UK VOA data |
| **School finder** | Finds nearby schools sorted by walking distance using the Bradford BSO school finder, with Ofsted ratings and admissions links |
| **Library finder** | Lists all Bradford libraries sorted by distance from a postcode with opening hours, facilities and how to join |
| **Education info** | Scrapes Bradford Council pages for admissions, SEND support, free school meals, term dates and uniform policies |
| **General council queries** | Searches and scrapes `bradford.gov.uk` live to answer any council service question |
| **Streaming responses** | Server-Sent Events (SSE) stream tokens to the browser as they arrive |
| **Conversation history** | Per-session history stored in SQLite, used as context for follow-up questions |
| **RAG (optional)** | Qdrant vector store for indexed council knowledge — bypassed for tool-based queries to save latency |

---

## Architecture

```
Frontend (Vercel)          Backend (Railway)
─────────────────          ──────────────────────────────────────
index.html                 ASP.NET Core 8 Web API
chat.js          ←──SSE──  ChatController  ──►  AgentService
config.js                                  ──►  LlmService (OpenAI)
style.css                                  ──►  CouncilToolService
                                           ──►  ConversationService (SQLite)
                                           ──►  RagService (Qdrant, optional)
```

### Backend projects

| Project | Role |
|---|---|
| `Api` | ASP.NET Core controllers, middleware, DI wiring |
| `Core` | Domain models, service interfaces, LLM/agent logic |
| `Infrastructure` | Tool implementations (scraping, APIs), data access, vector store |
| `Tests` | xUnit unit tests for agent service |

---

## Tech Stack

**Backend**
- ASP.NET Core 8 — Web API with SSE streaming
- OpenAI GPT-4o-mini — LLM with function/tool calling
- SQLite + Entity Framework Core 8 — conversation history
- Qdrant — vector store for RAG (optional)
- HtmlAgilityPack — HTML scraping
- Docker — containerised for Railway deployment

**Frontend**
- Vanilla HTML / CSS / JavaScript — no framework, no build step
- Vercel — static hosting

**External APIs**
- `api.postcodes.io` — postcode to lat/lon lookup (free, no key needed)
- `bso.bradford.gov.uk` — Bradford school finder (scraped)
- `onlineforms.bradford.gov.uk` — Bradford bin date eForms (scraped)
- `api.openai.com` — GPT-4o-mini completions

---

## Project Structure

```
Bradford Council Project/
├── Frontend/
│   ├── index.html          # Chat UI
│   ├── login.html          # Optional auth page
│   ├── chat.js             # All UI logic, card rendering, streaming
│   ├── config.js           # Auto-detects local vs production API URL
│   └── style.css           # All styles
│
├── Backend/
│   ├── Api/
│   │   ├── Controllers/
│   │   │   ├── ChatController.cs       # POST /api/chat + GET /api/chat/stream
│   │   │   └── IngestionController.cs  # POST /api/ingest (RAG ingestion)
│   │   ├── Program.cs                  # DI, middleware, CORS, rate limiting
│   │   ├── Dockerfile                  # Multi-stage Docker build
│   │   └── appsettings.json            # Config (keys via environment variables)
│   │
│   ├── Core/
│   │   ├── Models/         # ChatRequest, ChatResponse, BinDateCard, SchoolCard, etc.
│   │   ├── Interfaces/     # Service contracts
│   │   └── Services/
│   │       ├── AgentService.cs         # Agentic loop, tool orchestration, system prompt
│   │       └── LlmService.cs           # OpenAI client, streaming, tool call parsing
│   │
│   ├── Infrastructure/
│   │   ├── Tools/
│   │   │   ├── CouncilToolService.cs           # Tool definitions + executor routing
│   │   │   ├── CouncilToolService.Address.cs   # Address lookup (postcodes.io + getAddress.io)
│   │   │   ├── CouncilToolService.BinDates.cs  # Bin date scraper (Bradford eForms)
│   │   │   ├── CouncilToolService.CouncilTax.cs# Council tax band lookup (GOV.UK)
│   │   │   ├── CouncilToolService.Libraries.cs # Bradford library finder + details
│   │   │   ├── CouncilToolService.Schools.cs   # School finder (BSO) + education info
│   │   │   └── CouncilToolService.Web.cs       # Bradford.gov.uk scraper + search
│   │   ├── Data/
│   │   │   └── AgentDbContext.cs       # EF Core SQLite context
│   │   ├── Crawlers/
│   │   │   └── CouncilWebCrawler.cs    # Bradford.gov.uk web crawler for RAG ingestion
│   │   └── VectorStore/
│   │       └── QdrantVectorStore.cs    # Qdrant client wrapper
│   │
│   └── Tests/
│       └── AgentServiceTests.cs
│
├── deploy.ps1              # Deploy both frontend and backend
├── deploy.bat              # Double-click launcher for deploy.ps1
├── docker-compose.yml      # Local Qdrant + API stack
├── start-all.ps1           # Start everything locally
└── railway.toml            # Railway deployment config
```

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js](https://nodejs.org/) (for Vercel CLI)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (optional — for Qdrant locally)
- An [OpenAI API key](https://platform.openai.com/api-keys)

### Local development

**1. Clone the repo**
```bash
git clone https://github.com/YOUR_USERNAME/bradford-council-ai.git
cd bradford-council-ai
```

**2. Add your OpenAI key**

Create `Backend/Api/appsettings.Local.json` (gitignored):
```json
{
  "OpenAI": {
    "ApiKey": "sk-..."
  }
}
```

**3. Start the API**
```powershell
cd Backend
dotnet run --project Api
# API runs on http://localhost:5000
```

**4. Open the frontend**

Open `Frontend/index.html` directly in your browser — `config.js` automatically routes to `localhost:5000` when running from `file://`.

**5. (Optional) Start Qdrant for RAG**
```powershell
docker-compose up qdrant
```

---

## Deployment

### Backend — Railway

1. Install the [Railway CLI](https://docs.railway.app/develop/cli): `npm i -g @railway/cli`
2. `railway login`
3. Set environment variables in the Railway dashboard:

| Variable | Value |
|---|---|
| `OpenAI__ApiKey` | Your OpenAI secret key |
| `OpenAI__ChatModel` | `gpt-4o-mini` |
| `ConnectionStrings__Default` | `Data Source=/data/agent.db` |
| `GetAddress__ApiKey` | *(optional)* getAddress.io key for full address lookup |

4. Mount a Railway Volume at `/data` to persist the SQLite database across deploys.

### Frontend — Vercel

1. Install [Vercel CLI](https://vercel.com/docs/cli): `npm i -g vercel`
2. `vercel login`
3. From the `Frontend/` directory: `vercel --prod`

### One-command deploy (both)

```powershell
# Windows — double-click deploy.bat, or from terminal:
.\deploy.ps1

# Frontend only
.\deploy.ps1 -Frontend

# Backend only
.\deploy.ps1 -Backend
```

---

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `OpenAI__ApiKey` | Yes | OpenAI secret key (`sk-...`) |
| `OpenAI__ChatModel` | No | Defaults to `gpt-4o-mini` |
| `OpenAI__EmbeddingModel` | No | Defaults to `text-embedding-3-small` |
| `ConnectionStrings__Default` | No | SQLite path. Default: `Data Source=/data/agent.db` |
| `GetAddress__ApiKey` | No | [getAddress.io](https://getaddress.io) key for richer address lookup |
| `Qdrant__Host` | No | Qdrant hostname for RAG (default: `localhost`) |
| `Qdrant__ApiKey` | No | Qdrant API key (only needed for Qdrant Cloud) |
| `AdminKey` | No | Bearer token to protect the `/api/ingest` endpoint |

---

## API Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/health` | Health check — returns `{"status":"healthy"}` |
| `POST` | `/api/chat` | Non-streaming chat — returns full `ChatResponse` |
| `GET` | `/api/chat/stream?sessionId=&message=` | SSE streaming chat |
| `POST` | `/api/ingest` | Ingest URLs into the RAG vector store (requires `AdminKey`) |

---

## Available Tools (Agent)

The agent has access to these tools and calls them autonomously:

| Tool | Trigger |
|---|---|
| `lookup_addresses_for_postcode` | User provides a Bradford postcode for bin dates |
| `get_bin_dates_for_address` | User confirms their address |
| `lookup_council_tax_band` | User asks about council tax band or amount |
| `find_schools_near_postcode` | User asks for schools near a location |
| `get_school_details` | User picks a specific school |
| `get_education_info` | User asks about admissions, SEND, free meals, term dates |
| `find_local_services` | User asks for libraries, parks, leisure centres |
| `get_library_details` | User picks a specific library |
| `search_bradford_council` | General bradford.gov.uk search |
| `fetch_council_page` | Read a specific bradford.gov.uk page |
| `get_council_tax_info` | Council tax discounts, exemptions, payment info |
| `check_planning_application` | Planning portal search |

---

## License

MIT — see [LICENSE](LICENSE)

---

## Acknowledgements

- [Bradford Council](https://www.bradford.gov.uk) — public service data
- [postcodes.io](https://postcodes.io) — free UK postcode API
- [Ofsted](https://reports.ofsted.gov.uk) — school inspection data
- [OpenAI](https://openai.com) — GPT-4o-mini

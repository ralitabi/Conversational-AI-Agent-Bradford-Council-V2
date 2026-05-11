# Bradford Council AI Assistant

A conversational AI assistant for Bradford Council residents — bin dates, council tax, schools, libraries and more from a single chat window.

**Live Demo:** https://bradford-council-ai.vercel.app &nbsp;|&nbsp; **API:** https://bradford-council-api-production.up.railway.app/health

---

## Features

| Service | Description |
|---|---|
| Bin Collection | Exact grey, green and brown bin dates for any Bradford address |
| Council Tax | Band A–H and annual / monthly charge for any Bradford postcode |
| School Finder | Nearby schools sorted by distance with Ofsted ratings |
| Library Finder | Libraries sorted by distance with opening hours |
| Education Info | Admissions, SEND support, free meals, term dates |
| Planning | Search the Bradford planning portal by reference or address |
| General Queries | Live scraping of bradford.gov.uk for any council question |

---

## Architecture

```
User Browser (Vercel)
    index.html / chat.js / config.js / style.css
         |
         |  HTTP POST  /  SSE Stream
         v
Railway — ASP.NET Core 8
    ChatController
        └── AgentService  (agentic loop, up to 3 rounds)
                ├── LlmService          → OpenAI GPT-4o-mini
                ├── CouncilToolService  → Bin / Tax / Schools / Libraries / Web
                ├── ConversationService → SQLite
                └── RagService          → Qdrant (optional)
         |
         v
    postcodes.io  |  bradford.gov.uk  |  bso.bradford.gov.uk
```

---

## Project Structure

```
Bradford Council Project/
├── Frontend/               index.html, chat.js, config.js, style.css
├── Backend/
│   ├── Api/                ChatController, Program.cs, Dockerfile
│   ├── Core/               Models, Interfaces, AgentService, LlmService
│   ├── Infrastructure/     CouncilToolService, SQLite, Qdrant, Crawler
│   └── Tests/
├── scripts/                start-all / start-api / start-qdrant (.bat + .ps1)
├── deploy.bat / deploy.ps1
├── docker-compose.yml
└── railway.toml
```

---

## Getting Started

### Prerequisites
- .NET 8 SDK
- Node.js 18+
- Docker Desktop *(optional — for Qdrant RAG)*
- OpenAI API key

### Run locally

```bash
# 1. Clone
git clone https://github.com/ralitabi/Conversational-AI-Agent-Bradford-Council-V2.git
cd Conversational-AI-Agent-Bradford-Council-V2

# 2. Add API key — create Backend/Api/appsettings.Local.json
{ "OpenAI": { "ApiKey": "sk-proj-..." } }

# 3. Start API
cd Backend && dotnet run --project Api

# 4. Open Frontend/index.html in your browser
```

Or double-click **`scripts/start-all.bat`** to launch everything in one step.

---

## Deployment

### Backend — Railway
Set environment variables in the Railway dashboard, then:
```bash
.\deploy.ps1 -Backend
```

### Frontend — Vercel
```bash
.\deploy.ps1 -Frontend
```

### Deploy everything
```bash
.\deploy.bat
```

---

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `OpenAI__ApiKey` | Yes | OpenAI secret key |
| `OpenAI__ChatModel` | | Default: `gpt-4o-mini` |
| `ConnectionStrings__Default` | | Default: `Data Source=/data/agent.db` |
| `Qdrant__Host` | | Default: `localhost` |
| `Qdrant__ApiKey` | | Qdrant Cloud key |
| `GetAddress__ApiKey` | | Optional address lookup |
| `AdminKey` | | Protects `/api/ingest` |

---

## API Reference

| Endpoint | Description |
|---|---|
| `POST /api/chat` | Full response as JSON |
| `GET /api/chat/stream` | SSE token stream + `[STRUCTURED]` card event |
| `GET /health` | Health check |
| `POST /api/ingest` | Index URLs into Qdrant (requires `AdminKey`) |

---

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | HTML / CSS / Vanilla JS — Vercel |
| Backend | ASP.NET Core 8 — Railway |
| AI | OpenAI GPT-4o-mini (tool calling, SSE streaming) |
| Database | SQLite + EF Core 8 |
| Vector store | Qdrant (optional RAG) |
| HTML parsing | HtmlAgilityPack |
| Postcode data | postcodes.io |

---

## License

MIT — see [LICENSE](LICENSE).

*Made for Bradford residents by Raja Ali Tabish.*

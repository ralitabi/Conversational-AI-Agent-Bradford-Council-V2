<div align="center"># Bradford Council AI AssistantA conversational AI assistant designed to help Bradford residents access council services through a single, intuitive chat interface.[Live Demo](https://bradford-council-ai.vercel.app) •[API Health](https://bradford-council-api-production.up.railway.app/health).NET 8 • OpenAI GPT-4o-mini • ASP.NET Core • Railway • Vercel</div>---## OverviewBradford Council AI provides instant, accurate answers to council-related queries using natural language interaction.The system is designed to simplify access to public services by allowing residents to ask questions directly instead of navigating multiple council webpages.---## Features| Service | Description ||---|---|| Bin Collection | Retrieve upcoming grey, green, and brown bin collection dates || Council Tax | View council tax bands and payment information || School Finder | Search nearby schools with Ofsted ratings and admissions links || Library Finder | Locate libraries with opening hours and facilities || Education Information | Access admissions, SEND support, school meals, and term dates || Planning Applications | Search planning applications by reference or address || Live Council Search | Retrieve live information directly from Bradford Council services |---## Architecture```textUser Browser│├── Frontend (HTML, CSS, JavaScript)│       ││       └── SSE / REST Communication│└── ASP.NET Core API (Railway)        │        ├── AgentService        │       ├── LlmService (GPT-4o-mini)        │       └── CouncilToolService        │        ├── ConversationService (SQLite)        └── RagService (Qdrant - optional)External Services├── postcodes.io├── bradford.gov.uk└── Bradford BSO services

Project Structure
bradford-council-ai/│├── Frontend/│   ├── index.html│   ├── chat.js│   ├── config.js│   └── style.css│├── Backend/│   ├── Api/│   ├── Core/│   ├── Infrastructure/│   └── Tests/│├── scripts/├── docker-compose.yml├── railway.toml├── deploy.ps1└── README.md

Getting Started
Prerequisites


.NET 8 SDK


Node.js 18+


Docker Desktop (optional)


OpenAI API key



Clone the Repository
git clone https://github.com/ralitabi/Conversational-AI-Agent-Bradford-Council-V2.gitcd Conversational-AI-Agent-Bradford-Council-V2
Configure OpenAI
Create:
Backend/Api/appsettings.Local.json
Add:
{  "OpenAI": {    "ApiKey": "sk-proj-..."  }}
Run the Backend
cd Backenddotnet run --project Api
The API starts on:
http://localhost:5000
Run the Frontend
Open:
Frontend/index.html

Deployment
Backend — Railway
Configure environment variables in Railway and deploy the Backend project.
Frontend — Vercel
cd Frontendvercel --prod
One-Command Deployment
.\deploy.bat

Environment Variables
VariableDescriptionOpenAI__ApiKeyOpenAI API keyOpenAI__ChatModelChat model (default: gpt-4o-mini)ConnectionStrings__DefaultSQLite database pathGetAddress__ApiKeyOptional getAddress.io keyQdrant__HostQdrant hostnameAdminKeyProtects ingestion endpoint

API Endpoints
EndpointDescriptionPOST /api/chatReturns a complete AI responseGET /api/chat/streamStreams responses in real timeGET /healthAPI health statusPOST /api/ingestAdds data to vector storage

AI Capabilities
The assistant dynamically selects tools based on user requests, including:


Address lookup


Bin collection retrieval


Council tax queries


School and library search


Planning application lookup


General council information retrieval



Technology Stack
LayerTechnologyFrontendHTML, CSS, JavaScriptBackendASP.NET Core 8AI ModelGPT-4o-miniDatabaseSQLite + EF CoreVector StoreQdrantHostingRailway and Vercel

License
This project is licensed under the MIT License.

Acknowledgements


Bradford Metropolitan District Council


postcodes.io


OpenAI


Ofsted


HtmlAgilityPack



<div align="center">
Built to improve access to public services through conversational AI.
</div>
```
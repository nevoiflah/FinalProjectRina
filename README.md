# Ruppin Academic Advisor - AI and Robotics Wrapper
**Final Project 2025-2026 | Ruppin Academic Center**

## 1. Project Overview
This project represents a comprehensive software wrapper designed to bridge the gap between human interaction and robotic systems (virtual or physical). It integrates advanced Text-to-Speech (TTS) and Speech-to-Text (STT) capabilities with Generative AI to facilitate natural, spoken dialogue.

Key innovations include an "AI Memory" system based on Retrieval-Augmented Generation (RAG), which allows the system to learn from past successful interactions without the need for expensive model fine-tuning, and a dedicated Administrative Dashboard for monitoring and analysis.

---

## 2. Academic Analysis

### 2.1 Executive Summary
This project presents the development of an innovative Wrapper system for integrating TTS and STT functions in virtual and physical robots driven by Generative AI. The system aims to create a flexible and accessible interface enabling natural and intuitive communication between humans and robots.

The project involves developing a cloud-based software system integrating advanced AI technologies:
1.  **Hybrid Backend**: C# ASP.NET Core server for user management and business logic, combined with a dedicated Python Flask Microservice managing interactions with Large Language Models (LLM).
2.  **RAG (Retrieval-Augmented Generation)**: An organizational "memory" mechanism that learns from successful past conversations and guides the AI in real-time, without expensive fine-tuning.
3.  **Admin Dashboard**: A control tool for system administrators allowing user tracking, chat analysis (Initial Question vs. Final Result), and continuous improvement of responses.
4.  **OpenAI Whisper & TTS**: For human-level speech recognition and generation.

The system provides a unified, easy-to-use interface allowing developers and end-users to communicate with robots using natural language. It includes full internationalization support (Hebrew/English) and modern design adapted for academic institutions.

### 2.2 Problem Statement & Need
In the modern era, robots and virtual agents are becoming integral to daily life. However, human-robot interaction is typically limited to traditional graphical interfaces or complex programming languages. The need for natural interaction via spoken language is growing.

**Core Problems Addressed:**
1.  **Lack of Organizational Memory**: Standard language models (like GPT-3.5) are unaware of organizational history or similar past cases, leading to generic answers.
2.  **Integration Complexity**: Combining STT, TTS, and LLMs requires complex synchronization between disjoint services.
3.  **Lack of Control Tools**: Administrators typically lack convenient access to conversation data ("What are users asking vs. what are they getting?").

**Business & Social Need:**
*   **Education & Academic Advising**: Virtual advisors capable of guiding students to suitable study paths based on admission thresholds and grades.
*   **Smart Customer Service**: Virtual agents that learn from past solutions (RAG) and provide accurate 24/7 service.
*   **Accessibility**: Providing technology access to people with disabilities through natural voice conversation.

### 2.3 Existing Solutions & Limitations
Current market solutions suffer from fragmentation. Developers must manually stitch together STT providers (Google/AWS), LLMs (OpenAI/Anthropic), and TTS services, resulting in high latency and maintenance overhead. Most solutions lack a "memory" component, meaning every conversation starts from zero knowledge of the organization's prior successes.

**Our Innovation**: A unified Wrapper that consolidates speech technologies with advanced AI RAG capabilities, providing an End-to-End (E2E) solution that includes management, analytics, and multi-language support out of the box.

### 2.4 Solution Description
Our comprehensive system integrates:
1.  **Unified API Interface**: Abstraction layer for TTS, STT, and Chat.
2.  **Smart Admin Dashboard**: Control panel displaying all users and their conversation "stories" (Initial Question -> Final Result).
3.  **RAG Mechanism**: The system actively searches for similar past cases in the database and injects successful solutions into the current conversation.
4.  **Microservices Architecture**: Separation between the main server (C#) and the AI service (Python) for flexibility and performance.
5.  **Conversation Mode**: Continuous voice dialogue.

### 2.5 System Architecture
The system employs a Client-Server architecture with clear component separation:
```
[ Browser - React/Vite Client ]
  |--> Admin Dashboard
  |--> Chat Interface
        |
      HTTPS / REST
        |
[ Main Server - C# ASP.NET Core ]
  |-- UserController
  |-- AdminController
  |-- ChatController
  |-- SttController / TtsController
  |-- BL: ChatService (RAG Logic)
        |
        |---[ MongoDB Atlas ]
        |     |-- users
        |     |-- chatSessions
        |     |-- knowledge
        |
      HTTP (Internal)
        |
[ AI Service - Python Flask ]
  |-- Prompt Engineering
  |-- Context Injection
        |
      HTTPS
        |
[ OpenAI API (GPT-3.5, Whisper, TTS) ]
```

### 2.6 Challenges & Innovations
1.  **RAG without Heavy Infrastructure**: The challenge was to implement a memory mechanism without heavy infrastructure. **Solution**: MongoDB-backed knowledge storage, OpenAI embeddings, and cached retrieval logic in C#.
2.  **Language Synchronization**: Managing a bilingual interface (RTL/LTR) that affects the AI's System Prompt (instructing it to answer in the correct language).
3.  **State Management in Python**: Moving to a Python Microservice required efficient Context passing in every request (Stateless), solved by injecting history into the request body from the C# server.

---

## 3. Technical Installation & Setup

### Prerequisites
- .NET 8.0 SDK
- Python 3.8 or higher
- Node.js 18 or higher
- MongoDB Atlas database
- OpenAI API Key

### Required Configuration
Do not commit real secrets. `Server/appsettings.json` is ignored by git and should be used only for local development.

The .NET API expects:
```txt
ConnectionStrings__MongoDB
MongoDB__DatabaseName
OpenAI__ApiKey
PythonService__Url
```

The Python AI service expects:
```txt
OPENAI_API_KEY
```

The Vite client expects this value at build time:
```txt
VITE_API_BASE_URL
```

### Service Startup
**Terminal 1: Python AI Service**
```bash
cd Server/AI_Service
pip install -r requirements.txt
python app.py
```

**Terminal 2: C# Backend**
```bash
cd Server
dotnet run
```

**Terminal 3: React Client**
```bash
cd Client
npm install
VITE_API_BASE_URL=http://localhost:5102 npm run dev
```

For admin features, log in with the configured admin user.

### Health Checks
```bash
curl http://localhost:5102/health
curl http://localhost:5001/health
```

---

## 4. Azure Deployment

Recommended Azure layout:
- **Azure Static Web Apps** for `Client`
- **Azure App Service** for the .NET API in `Server`
- **Azure App Service Linux** for the Python service in `Server/AI_Service`
- **MongoDB Atlas** for data storage

### Create Resources
```bash
SUFFIX="$(date +%m%d%H%M)"
RG="rina-final-project-rg"
APP_LOCATION="eastus"
SWA_LOCATION="eastus2"

PLAN="rina-plan-$SUFFIX"
API_APP="rina-api-$SUFFIX"
AI_APP="rina-ai-$SUFFIX"
STATIC_APP="rina-client-$SUFFIX"

az group create --name "$RG" --location "$APP_LOCATION"

az appservice plan create \
  --name "$PLAN" \
  --resource-group "$RG" \
  --location "$APP_LOCATION" \
  --sku B1 \
  --is-linux
```

### Deploy Python AI Service
```bash
az webapp create \
  --resource-group "$RG" \
  --plan "$PLAN" \
  --name "$AI_APP" \
  --runtime "PYTHON:3.11"

az webapp config appsettings set \
  --resource-group "$RG" \
  --name "$AI_APP" \
  --settings OPENAI_API_KEY="$OPENAI_API_KEY" SCM_DO_BUILD_DURING_DEPLOYMENT=true

az webapp config set \
  --resource-group "$RG" \
  --name "$AI_APP" \
  --startup-file "gunicorn --bind=0.0.0.0:\$PORT app:app"

cd Server/AI_Service
zip -r ai-service.zip . -x "*.DS_Store" "__pycache__/*" "*.pyc"
az webapp deploy --resource-group "$RG" --name "$AI_APP" --src-path ai-service.zip --type zip
cd ../..
```

### Deploy .NET API
```bash
az webapp create \
  --resource-group "$RG" \
  --plan "$PLAN" \
  --name "$API_APP" \
  --runtime "DOTNETCORE:8.0"

az webapp config appsettings set \
  --resource-group "$RG" \
  --name "$API_APP" \
  --settings \
    ConnectionStrings__MongoDB="$MONGODB_CONNECTION_STRING" \
    MongoDB__DatabaseName="FinalProjectRina" \
    OpenAI__ApiKey="$OPENAI_API_KEY" \
    PythonService__Url="https://$AI_APP.azurewebsites.net" \
    ASPNETCORE_ENVIRONMENT="Production"

cd Server
dotnet publish -c Release -o publish
cd publish
zip -r ../server.zip . -x "*.DS_Store"
cd ..
az webapp deploy --resource-group "$RG" --name "$API_APP" --src-path server.zip --type zip
cd ..
```

### Deploy React Client
`VITE_API_BASE_URL` must be provided during `npm run build`; setting it afterward in Azure Static Web Apps does not update the already-built JavaScript.

```bash
az staticwebapp create \
  --name "$STATIC_APP" \
  --resource-group "$RG" \
  --location "$SWA_LOCATION" \
  --sku Free

cd Client
npm install
VITE_API_BASE_URL="https://$API_APP.azurewebsites.net" npm run build
npm install -g @azure/static-web-apps-cli

SWA_TOKEN=$(az staticwebapp secrets list \
  --name "$STATIC_APP" \
  --resource-group "$RG" \
  --query "properties.apiKey" \
  --output tsv)

swa deploy ./dist --deployment-token "$SWA_TOKEN" --env production
cd ..
```

### Verify Production
```bash
FRONTEND_HOST=$(az staticwebapp show --name "$STATIC_APP" --resource-group "$RG" --query defaultHostname --output tsv)

echo "Frontend: https://$FRONTEND_HOST"
echo "API:      https://$API_APP.azurewebsites.net"
echo "AI:       https://$AI_APP.azurewebsites.net"

curl "https://$API_APP.azurewebsites.net/health"
curl "https://$AI_APP.azurewebsites.net/health"
```

### MongoDB Atlas Network Access
Add the .NET API outbound IP addresses to MongoDB Atlas:
```bash
az webapp show \
  --resource-group "$RG" \
  --name "$API_APP" \
  --query outboundIpAddresses \
  --output tsv
```

Add those IPs in MongoDB Atlas under **Network Access**.

### Static App Refresh Support
`Client/public/staticwebapp.config.json` configures Azure Static Web Apps to rewrite React client-side routes to `index.html`. This prevents 404s when refreshing routes such as `/login` or `/admin`.

---

## 5. API Documentation

### POST /api/chat
Initiates a chat interaction.
- **Body**: `{ "message": "string", "userId": "string" }`
- **Response**: JSON object containing the AI reply.

### GET /api/admin/stats
Retrieves system statistics (Admin only).
- **Query Param**: `userId` (ID of the requesting admin).
- **Response**: Array of user objects including name, email, join date, initial question, and final result.

### GET /health
Returns API health status.

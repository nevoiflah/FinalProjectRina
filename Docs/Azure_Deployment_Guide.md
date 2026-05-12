# Azure Deployment Guide for Ruppin Academic Advisor

This guide deploys the current project architecture:

- React/Vite client to **Azure Static Web Apps**
- ASP.NET Core 8 API to **Azure App Service**
- Python Flask AI service to **Azure App Service Linux**
- MongoDB Atlas as the production database
- OpenAI for chat, embeddings, STT, and TTS

Do not commit real secrets. Configure secrets through Azure app settings and MongoDB Atlas.

## Prerequisites

1. Azure for Students subscription.
2. Azure CLI installed and logged in.
3. Node.js 18 or newer.
4. .NET 8 SDK.
5. MongoDB Atlas connection string.
6. OpenAI API key.

```bash
az login
az account show --output table
```

## 1. Set Deployment Variables

Run from the repository root:

```bash
cd /Volumes/MEMORY/Projects/FinalProjectRina

SUFFIX="$(date +%m%d%H%M)"
RG="rina-final-project-rg"
APP_LOCATION="eastus"
SWA_LOCATION="eastus2"

PLAN="rina-plan-$SUFFIX"
API_APP="rina-api-$SUFFIX"
AI_APP="rina-ai-$SUFFIX"
STATIC_APP="rina-client-$SUFFIX"

echo "Resource group: $RG"
echo "API app:        $API_APP"
echo "AI app:         $AI_APP"
echo "Static app:     $STATIC_APP"
```

Enter secrets for this terminal session:

```bash
echo "Paste OpenAI API key:"
read -s OPENAI_API_KEY
echo

echo "Paste MongoDB Atlas connection string:"
read -s MONGODB_CONNECTION_STRING
echo
```

## 2. Create Azure Resources

```bash
az group create \
  --name "$RG" \
  --location "$APP_LOCATION"

az appservice plan create \
  --name "$PLAN" \
  --resource-group "$RG" \
  --location "$APP_LOCATION" \
  --sku B1 \
  --is-linux
```

`B1` uses Azure student credits. It is more reliable for this multi-service setup than free-tier hosting.

## 3. Deploy Python AI Service

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

az webapp deploy \
  --resource-group "$RG" \
  --name "$AI_APP" \
  --src-path ai-service.zip \
  --type zip

cd ../..
```

Verify:

```bash
curl "https://$AI_APP.azurewebsites.net/health"
```

Expected response includes:

```json
{"status":"healthy","openaiConfigured":true}
```

## 4. Deploy ASP.NET Core API

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

az webapp deploy \
  --resource-group "$RG" \
  --name "$API_APP" \
  --src-path server.zip \
  --type zip

cd ..
```

Verify:

```bash
curl "https://$API_APP.azurewebsites.net/health"
curl "https://$API_APP.azurewebsites.net/api/user"
```

## 5. Configure MongoDB Atlas Network Access

Get the outbound IP addresses for the API app:

```bash
az webapp show \
  --resource-group "$RG" \
  --name "$API_APP" \
  --query outboundIpAddresses \
  --output tsv
```

In MongoDB Atlas:

1. Open **Network Access**.
2. Add the Azure outbound IP addresses.
3. Confirm the database user in the connection string has read/write permissions.

For a short demo only, `0.0.0.0/0` can be used temporarily, but it should not remain open for production.

## 6. Deploy React/Vite Client

`VITE_API_BASE_URL` must be set during the build. Azure Static Web Apps app settings do not rewrite an already-built Vite bundle.

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

swa deploy ./dist \
  --deployment-token "$SWA_TOKEN" \
  --env production

cd ..
```

Verify that the API URL was compiled into the bundle:

```bash
grep -R "https://$API_APP.azurewebsites.net" Client/dist/assets
```

## 7. Verify The Live System

```bash
FRONTEND_HOST=$(az staticwebapp show \
  --name "$STATIC_APP" \
  --resource-group "$RG" \
  --query defaultHostname \
  --output tsv)

echo "Frontend: https://$FRONTEND_HOST"
echo "API:      https://$API_APP.azurewebsites.net"
echo "AI:       https://$AI_APP.azurewebsites.net"
```

Manual checks:

1. Open the frontend URL.
2. Refresh `/login`, `/admin`, and `/` to confirm client-side routing works.
3. Log in or register a user.
4. Send a chat message.
5. Test TTS and STT from the chat screen.
6. Open the admin dashboard with an admin user.

## 8. Troubleshooting

### Refresh Returns 404

Confirm `Client/public/staticwebapp.config.json` exists and was copied into `Client/dist`:

```bash
cat Client/dist/staticwebapp.config.json
```

Redeploy the frontend after rebuilding.

### Login Returns 405

The frontend was probably built without `VITE_API_BASE_URL`, so requests are going to the Static Web App instead of the API.

Rebuild and redeploy:

```bash
cd Client
VITE_API_BASE_URL="https://$API_APP.azurewebsites.net" npm run build
swa deploy ./dist --deployment-token "$SWA_TOKEN" --env production
cd ..
```

### API Cannot Reach MongoDB

Check MongoDB Atlas network access and the Azure app setting:

```bash
az webapp config appsettings list \
  --resource-group "$RG" \
  --name "$API_APP" \
  --query "[?name=='ConnectionStrings__MongoDB' || name=='MongoDB__DatabaseName']"
```

### API Cannot Reach Python Service

Check the configured Python service URL:

```bash
az webapp config appsettings list \
  --resource-group "$RG" \
  --name "$API_APP" \
  --query "[?name=='PythonService__Url']"
```

Then verify the Python service:

```bash
curl "https://$AI_APP.azurewebsites.net/health"
```

### View Logs

```bash
az webapp log tail --resource-group "$RG" --name "$API_APP"
az webapp log tail --resource-group "$RG" --name "$AI_APP"
```

## 9. Domain Setup

After the Azure URLs work:

- Point `www.yourdomain.com` to Azure Static Web Apps.
- Point `api.yourdomain.com` to the API App Service.
- Keep the Python AI service private by convention; the API can call its Azure URL directly.

Configure custom domains from each Azure resource's **Custom domains** page and follow the DNS records Azure provides.

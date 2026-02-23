# Azure Deployment Guide for Ruppin Academic Advisor

This guide outlines the steps to deploy your **C# ASP.NET Core Backend**, **Python Flask AI Microservice**, and **SQL Database** to the Microsoft Azure Cloud.

## Prerequisites
1.  **Azure Account**: [Sign up for free](https://azure.microsoft.com/free/) (or use Azure for Students).
2.  **Azure CLI**: Install the Azure Command Line Interface (optional but helpful).
3.  **Visual Studio 2022**: Installed with Azure development workload.

---

## Step 1: Create Azure SQL Database
Since you need a "table-oriented database", Azure SQL is the native choice.

1.  Log in to the **Azure Portal**.
2.  Search for **SQL Databases** and click **Create**.
3.  **Resource Group**: Create a new one (e.g., `RuppinProject_RG`).
4.  **Database Name**: e.g., `RuppinDB`.
5.  **Server**: Click "Create new".
    *   **Server name**: Global unique name (e.g., `ruppin-sql-server-nevo`).
    *   **Authentication**: Use "Use SQL authentication". Set a robust Admin Login and Password. **Save these!**
6.  **Pricing Tier**: Select "Basic" or "Standard S0" (cheapest options suitable for projects).
7.  **Networking**:
    *   **Firewall rules**: Select "Yes" for "Allow Azure services and resources to access this server".
    *   Add your current client IP address to allow local connection from SSMS.
8.  **Review + create**.

### Update Database Schema
Once created:
1.  Get the **Connection String** from the database "Overview" -> "Show database connection strings".
2.  Open **SSMS** (SQL Server Management Studio) locally.
3.  Connect to the new Azure Server (e.g., `ruppin-sql-server-nevo.database.windows.net`) using your admin credentials.
4.  Run your existing `UserTable.sql` and `ChatSessionsTable.sql` scripts to create the tables in the cloud.

---

## Step 2: Deploy C# ASP.NET Core Backend
1.  In **Visual Studio**, right-click the `Server` project -> **Publish**.
2.  Target: **Azure**.
3.  Specific Target: **Azure App Service (Windows)**.
4.  **Create New**:
    *   Name: `ruppin-backend-nevo` (must be unique).
    *   Plan: Free (F1) or Shared (D1) if available, otherwise Basic (B1).
5.  **Database Connection**:
    *   Visual Studio might detect your SQL Database. If not, click "Next" and finish.
    *   In the Publish summary screen, click "Configure" next to **Service Dependencies**.
    *   Select "Azure SQL Database", connect to the DB you created in Step 1.
    *   Visual Studio will update the connection string in the published app settings automatically.
6.  **Publish**.
7.  Once finished, your API will be live at `https://ruppin-backend-nevo.azurewebsites.net`.

---

## Step 3: Deploy Python AI Microservice
Deploy as a separate Web App.

1.  In **Azure Portal**, create a new **Web App**.
2.  Name: `ruppin-ai-service-nevo`.
3.  **Publish**: Code.
4.  **Runtime stack**: Python 3.11 (or 3.10).
5.  **OS**: Linux.
6.  **Plan**: Select the same region and Resource Group. Use Free (F1) or Basic (B1).
7.  **Create**.

### Deploy Code (from VS Code)
1.  Open the `Server/AI_Service` folder in VS Code.
2.  Install "Azure App Service" extension.
3.  Right-click the App Service you just created -> **Deploy to Web App**.
4.  Select the `Server/AI_Service` folder.
5.  Azure will detect `requirements.txt` and build the environment.

### Configure Environment Variables
1.  Go to the Python Web App in Azure Portal.
2.  **Settings** -> **Environment variables**.
3.  Add:
    *   `OPENAI_API_KEY`: Your actual key (starting with `sk-proj...`).
4.  **Save**.

---

## Step 4: Final Configuration & Connection
Now link the two services and the frontend.

### 1. Update C# Backend Configuration
The C# backend needs to know where the Python service is.
1.  Go to your **C# App Service** in Azure Portal.
2.  **Settings** -> **Environment variables**.
3.  Add:
    *   `PythonService__Url`: `https://ruppin-ai-service-nevo.azurewebsites.net` (Note the double underscore `__` for nested JSON keys in Azure).
    
### 2. Update Frontend Code
The HTML/JS frontend is currently served by your C# app's `wwwroot`.
1.  Since you are serving static files from the C# backend, you generally **do not** need to change the API URL in `script.js` if you are using relative paths (e.g., `fetch('/api/chat')`).
2.  **Check your `client/script.js`**:
    *   If you have `const API_BASE_URL = "http://localhost:5117";`, change it to your Azure URL: `const API_BASE_URL = "";` (empty string implies relative path) OR `"https://ruppin-backend-nevo.azurewebsites.net"`.
    *   **Recommendation**: Use relative paths (`/api/...`) so it works both locally and in prod suitable for serving from `wwwroot`.

### 3. Re-Publish C# Logic
If you changed `script.js`, Re-Publish the C# project so the new `wwwroot` files go to the cloud.

---

## Step 5: Verification
1.  Open `https://ruppin-backend-nevo.azurewebsites.net`.
2.  The login page should load.
3.  Create a user (this tests the SQL Database connection).
4.  Log in and try the chat (this tests the Python connection).

## Troubleshooting
*   **500 Errors**: Check "Log Stream" in Azure Portal for the specific App Service.
*   **CORS Errors**: In C# App Service -> **Settings** -> **CORS**. Add `*` (or your specific domain) to allowed origins.

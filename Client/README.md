# Ruppin Academic Advisor Client

React/Vite frontend for the Ruppin Academic Advisor system.

## Local Development

```bash
npm install
VITE_API_BASE_URL=http://localhost:5102 npm run dev
```

## Production Build

`VITE_API_BASE_URL` is compiled into the static JavaScript bundle, so it must be set when building:

```bash
VITE_API_BASE_URL=https://YOUR_API_APP.azurewebsites.net npm run build
```

The Azure Static Web Apps routing fallback lives in `public/staticwebapp.config.json`; it is copied into `dist` during `npm run build` and allows routes such as `/login` and `/admin` to work after refresh.

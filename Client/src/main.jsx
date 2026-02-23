import React from 'react';

window.addEventListener('error', (event) => {
  document.body.innerHTML += `<div style="background: white; color: red; padding: 20px; position: fixed; top: 0; left: 0; right: 0; z-index: 9999;">
    <h2>Runtime Error</h2>
    <pre>${event.message}</pre>
    <pre>${event.error?.stack}</pre>
  </div>`;
});

window.addEventListener('unhandledrejection', (event) => {
  document.body.innerHTML += `<div style="background: white; color: red; padding: 20px; position: fixed; top: 0; left: 0; right: 0; z-index: 9999;">
    <h2>Unhandled Promise Rejection</h2>
    <pre>${event.reason?.message || event.reason}</pre>
    <pre>${event.reason?.stack}</pre>
  </div>`;
});
import { createRoot } from 'react-dom/client';
import App from './App.jsx';
import './index.css';

createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
)

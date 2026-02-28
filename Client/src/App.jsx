import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import Login from './pages/Login';
import Register from './pages/Register';
import Chat from './pages/Chat';
import Admin from './pages/Admin';

const PrivateRoute = ({ children, adminOnly }) => {
  const userStr = localStorage.getItem('chatUser');
  if (!userStr) return <Navigate to="/login" />;

  const user = JSON.parse(userStr);
  if (adminOnly && !user.isAdmin && !user.IsAdmin) {
    return <Navigate to="/" />;
  }
  return children;
};

import { LanguageProvider } from './context/LanguageContext';
import AnimatedBackground from './components/AnimatedBackground';

function App() {
  return (
    <LanguageProvider>
      <AnimatedBackground />
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />
          <Route
            path="/"
            element={
              <PrivateRoute>
                <Chat />
              </PrivateRoute>
            }
          />
          <Route
            path="/admin"
            element={
              <PrivateRoute adminOnly={true}>
                <Admin />
              </PrivateRoute>
            }
          />
        </Routes>
      </BrowserRouter>
    </LanguageProvider>
  );
}

export default App;

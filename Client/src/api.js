import axios from 'axios';

const API_BASE = 'http://localhost:5102';

const api = axios.create({
  baseURL: API_BASE,
  headers: {
    'Content-Type': 'application/json',
  },
});

export const loginUser = async (email, password) => {
  const response = await api.post('/api/user/login', { email, password });
  return response.data;
};

export const registerUser = async (userData) => {
  const response = await api.post('/api/user/register', userData);
  return response.data;
};

export const sendChatMessage = async (message, userId) => {
  const response = await api.post('/api/chat', { message, userId });
  return response.data;
};

export const fetchAdminStats = async (userId) => {
  const response = await api.get(`/api/admin/sessions?userId=${userId}`);
  return response.data;
};

export const getTtsAudio = async (text) => {
  const response = await fetch(`${API_BASE}/api/tts`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ text }),
  });
  if (!response.ok) throw new Error('TTS Failed');
  const buffer = await response.arrayBuffer();
  return new Blob([buffer], { type: 'audio/wav' });
};

export const getSttTranscript = async (audioBlob, language = 'he') => {
  const form = new FormData();
  form.append('audio', audioBlob, 'speech.webm');
  form.append('language', language);
  const response = await fetch(`${API_BASE}/api/stt`, {
    method: 'POST',
    body: form,
  });
  if (!response.ok) throw new Error('STT Failed');
  const data = await response.json();
  return data.transcript;
};

export const endSession = async (userId) => {
  await api.post('/api/chat/end', { userId });
};

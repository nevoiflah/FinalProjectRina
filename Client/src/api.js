import axios from 'axios';

const API_BASE = (import.meta.env.VITE_API_BASE_URL || (import.meta.env.DEV ? 'http://localhost:5102' : '')).replace(/\/$/, '');

if (!API_BASE) {
  throw new Error('VITE_API_BASE_URL is required for production builds.');
}

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

export const sendChatMessage = async (message, userId, history = [], language, persona) => {
  const response = await api.post('/api/chat', { message, userId, history, language, persona });
  return response.data;
};

export const fetchAdminStats = async (userId) => {
  const response = await api.get(`/api/admin/sessions?userId=${userId}`);
  return response.data;
};

export const getTtsAudio = async (text, voice, speed) => {
  const response = await fetch(`${API_BASE}/api/tts`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ text, voice, speed }),
  });
  if (!response.ok) throw new Error('TTS Failed');
  // Keep the server's real content type (audio/mpeg) so playback works across browsers.
  return await response.blob();
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

export const submitFeedback = async (userId, question, answer, rating) => {
  await api.post('/api/chat/feedback', { userId, question, answer, rating });
};

// --- Admin: self-improving RAG review ---
export const fetchLearningQueue = async (adminId) => {
  const response = await api.get(`/api/admin/learning?userId=${adminId}`);
  return response.data;
};

export const approveLearningCandidate = async (adminId, id, fact, category) => {
  const response = await api.post(`/api/admin/learning/${id}/approve?adminId=${adminId}`, { fact, category });
  return response.data;
};

export const rejectLearningCandidate = async (adminId, id) => {
  const response = await api.post(`/api/admin/learning/${id}/reject?adminId=${adminId}`);
  return response.data;
};

export const fetchLearnedFacts = async (adminId) => {
  const response = await api.get(`/api/admin/knowledge/learned?userId=${adminId}`);
  return response.data;
};

export const pruneLearnedFact = async (adminId, id) => {
  const response = await api.delete(`/api/admin/knowledge/${id}?adminId=${adminId}`);
  return response.data;
};

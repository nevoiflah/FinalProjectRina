const express = require('express');
const cors = require('cors');
const multer = require('multer');

const { handleChat } = require('./Controllers/chatController');
const { handleStt } = require('./Controllers/sttController');
const { handleTts } = require('./Controllers/ttsController');

const app = express();
const upload = multer({
  storage: multer.memoryStorage(),
  limits: {
    fileSize: 5 * 1024 * 1024, // 5MB max audio payloads
  },
});

const PORT = process.env.PORT || 3000;

app.use(cors());
app.use(express.json({ limit: '1mb' }));

app.post('/api/chat', handleChat);
app.post('/api/stt', upload.single('audio'), handleStt);
app.post('/api/tts', handleTts);

app.use((err, req, res, next) => {
  // Centralized error response to keep controllers lean.
  console.error(err);
  const status = err.status || 500;
  res.status(status).json({ error: err.message || 'Internal Server Error' });
});

app.listen(PORT, () => {
  console.log(`Server is running on port ${PORT}`);
});

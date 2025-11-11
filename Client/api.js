(function () {
	/**
	 * api.js — browser-friendly API bridge that mirrors the UI in index.html.
	 * It exposes a single global `BotAPI` object so regular <script> tags
	 * (no modules) can access chat/STT/TTS helpers.
	 */

	const USE_MOCK_API = window.__USE_MOCK_API__ ?? true;
	const API_BASE = window.__CHAT_API_BASE__ || '';

	const mockReplies = [
		"That's an interesting question! I'd be happy to help you with that.",
		"I understand what you're asking. Let me provide some insights.",
		"Great question! Here's what I think about that.",
		"Thanks for sharing. Let me help you with that information.",
		"I can definitely assist you with that. Here's my response.",
	];

	function delay(ms) {
		return new Promise(resolve => setTimeout(resolve, ms));
	}

	function ensureOk(response, label) {
		if (!response.ok) {
			throw new Error(`${label} failed with status ${response.status}`);
		}
		return response;
	}

	function createToneWavBlob(durationMs = 600, freqHz = 550, sampleRate = 22050) {
		const length = Math.floor((durationMs / 1000) * sampleRate);
		const headerSize = 44;
		const bytesPerSample = 2;
		const blockAlign = 1 * bytesPerSample;
		const dataSize = length * bytesPerSample;
		const buffer = new ArrayBuffer(headerSize + dataSize);
		const view = new DataView(buffer);

		function writeString(offset, s) {
			for (let i = 0; i < s.length; i++) view.setUint8(offset + i, s.charCodeAt(i));
		}

		writeString(0, 'RIFF');
		view.setUint32(4, 36 + dataSize, true);
		writeString(8, 'WAVE');
		writeString(12, 'fmt ');
		view.setUint32(16, 16, true);
		view.setUint16(20, 1, true);
		view.setUint16(22, 1, true);
		view.setUint32(24, sampleRate, true);
		view.setUint32(28, sampleRate * blockAlign, true);
		view.setUint16(32, blockAlign, true);
		view.setUint16(34, 16, true);
		writeString(36, 'data');
		view.setUint32(40, dataSize, true);

		let offset = 44;
		for (let i = 0; i < length; i++) {
			const t = i / sampleRate;
			const sample = Math.sin(2 * Math.PI * freqHz * t);
			const s16 = Math.max(-1, Math.min(1, sample)) * 0x7fff;
			view.setInt16(offset, s16, true);
			offset += 2;
		}
		return new Blob([view], { type: 'audio/wav' });
	}

	async function chat(message) {
		if (!message) throw new Error('Message text is required');
		if (USE_MOCK_API) {
			await delay(700);
			const reply = mockReplies[Math.floor(Math.random() * mockReplies.length)];
			return { reply };
		}
		const res = await fetch(`${API_BASE}/api/chat`, {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify({ message }),
		});
		const data = await ensureOk(res, 'Chat').json();
		return { reply: data.reply ?? '' };
	}

	async function stt(audioBlob) {
		if (!(audioBlob instanceof Blob)) {
			throw new Error('Audio blob is required for STT');
		}
		if (USE_MOCK_API) {
			await delay(600);
			const ts = new Date().toLocaleTimeString();
			return { transcript: `Mock transcript captured at ${ts}` };
		}
		const form = new FormData();
		form.append('audio', audioBlob, 'speech.webm');
		const res = await fetch(`${API_BASE}/api/stt`, {
			method: 'POST',
			body: form,
		});
		const data = await ensureOk(res, 'STT').json();
		return { transcript: data.transcript ?? '' };
	}

	async function tts(text) {
		if (!text) throw new Error('Text is required for TTS');
		if (USE_MOCK_API) {
			await delay(500);
			return createToneWavBlob(750, 620);
		}
		const res = await fetch(`${API_BASE}/api/tts`, {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify({ text }),
		});
		const arrayBuf = await ensureOk(res, 'TTS').arrayBuffer();
		return new Blob([arrayBuf], { type: res.headers.get('Content-Type') || 'audio/wav' });
	}

	window.BotAPI = Object.freeze({
		mode: USE_MOCK_API ? 'mock' : 'real',
		chat,
		stt,
		tts,
	});
})();


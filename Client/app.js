(() => {
	const INITIAL_BOT_MESSAGE = "Hello! I'm your AI assistant. Type a message or use the microphone button to speak with me. How can I help you today?";
	const TRANSITION_MS = 300;

	class ChatApp {
		constructor() {
			this.refs = this.cacheElements();
			if (!this.refs) return;

			this.currentUser = this.loadCurrentUser();
			if (!this.currentUser) {
				this.redirectToLogin();
				return;
			}

			// TTS is disabled by default - users won't hear beeps after each message
			this.ttsEnabled = false;

			this.voiceController = this.createVoiceController();
			this.bindEvents();
			this.resetConversation();
		}

		cacheElements() {
			const refs = {
				startScreen: document.getElementById('startScreen'),
				chatScreen: document.getElementById('chatScreen'),
				startChatBtn: document.getElementById('startChatBtn'),
				backBtn: document.getElementById('backBtn'),
				sendBtn: document.getElementById('sendBtn'),
				micBtn: document.getElementById('micBtn'),
				messageInput: document.getElementById('messageInput'),
				chatMessages: document.getElementById('chatMessages'),
				recordingIndicator: document.getElementById('recordingIndicator'),
				ttsAudio: document.getElementById('ttsAudio'),
				ttsToggle: document.getElementById('ttsToggle'),
			};

			const missing = Object.entries(refs)
				.filter(([, el]) => !el)
				.map(([key]) => key);
			if (missing.length) {
				console.error(`ChatApp initialization failed – missing elements: ${missing.join(', ')}`);
				return null;
			}
			return refs;
		}

		createVoiceController() {
			if (!window.ChatUtils?.VoiceInputController) return null;
			return new window.ChatUtils.VoiceInputController({
				micButton: this.refs.micBtn,
				indicatorEl: this.refs.recordingIndicator,
				inputEl: this.refs.messageInput,
				// Changed: Put transcription in input field for review/edit instead of auto-sending
				onTranscription: (text) => this.handleVoiceTranscription(text),
			});
		}

		bindEvents() {
			const { startChatBtn, backBtn, sendBtn, micBtn, messageInput, ttsToggle } = this.refs;

			startChatBtn.addEventListener('click', () => this.startChat());
			backBtn.addEventListener('click', () => this.backToStart());
			sendBtn.addEventListener('click', () => this.handleSend());

			messageInput.addEventListener('keypress', (event) => {
				if (event.key === 'Enter') {
					event.preventDefault();
					this.handleSend();
				}
			});

			if (micBtn && this.voiceController) {
				micBtn.addEventListener('click', () => this.voiceController.toggle());
			}

			// Toggle TTS on/off
			if (ttsToggle) {
				ttsToggle.addEventListener('click', () => this.toggleTts());
			}
		}

		toggleTts() {
			this.ttsEnabled = !this.ttsEnabled;
			const { ttsToggle } = this.refs;
			if (ttsToggle) {
				ttsToggle.classList.toggle('active', this.ttsEnabled);
				ttsToggle.setAttribute('aria-pressed', String(this.ttsEnabled));
				ttsToggle.title = this.ttsEnabled ? 'Voice responses ON' : 'Voice responses OFF';
				
				// Visual feedback
				const icon = ttsToggle.querySelector('svg');
				if (icon) {
					icon.style.opacity = this.ttsEnabled ? '1' : '0.5';
				}
			}
		}

		loadCurrentUser() {
			const raw = localStorage.getItem('chatUser');
			if (!raw) return null;
			try {
				const parsed = JSON.parse(raw);
				return parsed && (parsed.userId || parsed.UserId) ? parsed : null;
			} catch {
				return null;
			}
		}

		redirectToLogin(message) {
			if (message) {
				alert(message);
			}
			window.location.replace('login.html');
		}

		ensureAuthenticated() {
			if (this.currentUser && (this.currentUser.userId || this.currentUser.UserId)) {
				return true;
			}
			this.redirectToLogin('Your session has expired. Please log in again.');
			return false;
		}

		startChat() {
			if (!this.ensureAuthenticated()) return;
			const { startScreen, chatScreen, messageInput } = this.refs;
			startScreen.classList.add('fade-out');
			setTimeout(() => {
				startScreen.classList.add('hidden');
				startScreen.classList.remove('fade-out');
				chatScreen.classList.remove('hidden');
				messageInput.focus();
			}, TRANSITION_MS);
		}

		backToStart() {
			const { startScreen, chatScreen, messageInput } = this.refs;
			chatScreen.classList.add('fade-out');
			setTimeout(() => {
				chatScreen.classList.add('hidden');
				chatScreen.classList.remove('fade-out');
				startScreen.classList.remove('hidden', 'fade-out');
				messageInput.value = '';
				this.resetConversation();
			}, TRANSITION_MS);
		}

		resetConversation() {
			this.refs.chatMessages.innerHTML = '';
			this.addMessage(INITIAL_BOT_MESSAGE, 'bot');
			this.typingIndicator = null;
			this.lastBotReply = INITIAL_BOT_MESSAGE;
		}

		handleVoiceTranscription(text) {
			if (!text) return;
			// NEW BEHAVIOR: Put transcribed text in input field so user can review/edit
			// before sending. User must click send button or press Enter to actually send.
			this.refs.messageInput.value = text;
			this.refs.messageInput.focus();
			
			// Move cursor to end of text
			this.refs.messageInput.setSelectionRange(text.length, text.length);
		}

		handleSend() {
			const text = this.refs.messageInput.value.trim();
			if (!text) return;
			this.refs.messageInput.value = '';
			this.processOutgoingMessage(text);
		}

		async processOutgoingMessage(text) {
			if (!this.ensureAuthenticated()) return;
			this.addMessage(text, 'user');
			this.showTypingIndicator();
			this.refs.sendBtn.disabled = true;

			try {
				const { reply } = await window.BotAPI.chat(text);
				this.lastBotReply = reply || "I'm sorry, I didn't catch that.";
				this.addMessage(this.lastBotReply, 'bot');
				
				// Only play TTS if user has enabled it
				if (this.ttsEnabled) {
					this.tryPlayTts(this.lastBotReply);
				}
			} catch (err) {
				console.error('Chat request failed:', err);
				this.addMessage('Something went wrong. Please try again.', 'bot');
			} finally {
				this.hideTypingIndicator();
				this.refs.sendBtn.disabled = false;
			}
		}

		showTypingIndicator() {
			if (this.typingIndicator) return;
			const indicator = window.ChatUtils.createMessageElement('Thinking...', 'bot');
			indicator.classList.add('typing');
			this.refs.chatMessages.appendChild(indicator);
			window.ChatUtils.scrollToBottom(this.refs.chatMessages);
			this.typingIndicator = indicator;
		}

		hideTypingIndicator() {
			if (!this.typingIndicator) return;
			this.typingIndicator.remove();
			this.typingIndicator = null;
		}

		addMessage(text, sender) {
			window.ChatUtils.appendMessage(this.refs.chatMessages, text, sender);
		}

		async tryPlayTts(text) {
			if (!text || !this.refs.ttsAudio) return;
			if (window.BotAPI?.mode === 'mock') {
				// Skip TTS in mock mode
				return;
			}
			try {
				const audioBlob = await window.BotAPI.tts(text);
				const url = URL.createObjectURL(audioBlob);
				this.refs.ttsAudio.src = url;
				await this.refs.ttsAudio.play().catch(() => {
					// Autoplay might be blocked; that's okay
				});
			} catch (err) {
				console.warn('TTS playback failed:', err);
			}
		}
	}

	document.addEventListener('DOMContentLoaded', () => {
		if (!window.BotAPI) {
			console.error('BotAPI is not available. Ensure api.js is loaded first.');
			return;
		}
		if (!window.ChatUtils) {
			console.error('ChatUtils is not available. Ensure script.js is loaded before app.js.');
			return;
		}
		if (window.__CHAT_APP_INITIALIZED__) return;
		window.__CHAT_APP_INITIALIZED__ = true;
		new ChatApp();
	});
})();
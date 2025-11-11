(() => {
	const INITIAL_BOT_MESSAGE = "Hello! I'm your AI assistant. Type a message or use the microphone button to speak with me. How can I help you today?";
	const TRANSITION_MS = 300;

	class ChatApp {
		constructor() {
			this.refs = this.cacheElements();
			if (!this.refs) return;

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
			};

			const missing = Object.entries(refs)
				.filter(([, el]) => !el)
				.map(([key]) => key);
			if (missing.length) {
				console.error(`ChatApp initialization failed — missing elements: ${missing.join(', ')}`);
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
				onTranscription: (text) => this.handleVoiceTranscription(text),
			});
		}

		bindEvents() {
			const { startChatBtn, backBtn, sendBtn, micBtn, messageInput } = this.refs;

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
		}

		startChat() {
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
			this.refs.messageInput.value = '';
			this.processOutgoingMessage(text);
		}

		handleSend() {
			const text = this.refs.messageInput.value.trim();
			if (!text) return;
			this.refs.messageInput.value = '';
			this.processOutgoingMessage(text);
		}

		async processOutgoingMessage(text) {
			this.addMessage(text, 'user');
			this.showTypingIndicator();
			this.refs.sendBtn.disabled = true;

			try {
				const { reply } = await window.BotAPI.chat(text);
				this.lastBotReply = reply || "I'm sorry, I didn't catch that.";
				this.addMessage(this.lastBotReply, 'bot');
				this.tryPlayTts(this.lastBotReply);
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
			try {
				const audioBlob = await window.BotAPI.tts(text);
				const url = URL.createObjectURL(audioBlob);
				this.refs.ttsAudio.src = url;
				await this.refs.ttsAudio.play().catch(() => {
					// Autoplay might be blocked; user can trigger playback later if needed.
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

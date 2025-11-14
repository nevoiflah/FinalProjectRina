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

			// TTS is disabled by default
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
				micBtn.addEventListener('click', () => {
					// In conversation mode, manual mic click should work differently
					if (this.conversationState?.isActive) {
						// Let conversation mode handle it
						return;
					}
					this.voiceController.toggle();
				});
			}

			if (ttsToggle) {
				ttsToggle.addEventListener('click', () => this.toggleTts());
			}
		}

		toggleTts() {
			this.ttsEnabled = !this.ttsEnabled;
			const { ttsToggle, chatScreen } = this.refs;
			
			if (ttsToggle) {
				ttsToggle.classList.toggle('active', this.ttsEnabled);
				ttsToggle.setAttribute('aria-pressed', String(this.ttsEnabled));
				ttsToggle.title = this.ttsEnabled ? 'Conversation mode ON' : 'Conversation mode OFF';
				
				const icon = ttsToggle.querySelector('svg');
				if (icon) {
					icon.style.opacity = this.ttsEnabled ? '1' : '0.5';
				}
			}

			if (this.ttsEnabled) {
				chatScreen?.classList.add('conversation-mode');
				this.startConversationMode();
			} else {
				chatScreen?.classList.remove('conversation-mode');
				this.stopConversationMode();
			}
		}

		startConversationMode() {
			console.log('[Conversation] Starting human-like conversation mode');
			
			// Show notification
			this.showConversationModeNotification();
			
			// Initialize state
			this.conversationState = {
				isActive: true,
				isListening: false,
				isSpeaking: false,
				audioContext: null,
				analyser: null,
				silenceTimer: null,
				waitTimer: null,
				silenceThreshold: 1500,
				waitAfterResponse: 2500,
			};

			// Create visual indicator
			this.createConversationIndicator();
			
			// Start listening after brief delay
			setTimeout(() => this.beginListening(), 1000);
		}

		stopConversationMode() {
			console.log('[Conversation] Stopping conversation mode');
			
			if (this.conversationState) {
				this.conversationState.isActive = false;
				
				if (this.conversationState.silenceTimer) {
					clearTimeout(this.conversationState.silenceTimer);
				}
				if (this.conversationState.waitTimer) {
					clearTimeout(this.conversationState.waitTimer);
				}
				if (this.conversationState.audioContext) {
					this.conversationState.audioContext.close();
				}
			}

			if (this.voiceController?.isRecording) {
				this.voiceController.stop();
			}

			this.removeConversationIndicator();
		}

		showConversationModeNotification() {
			const notification = document.createElement('div');
			notification.className = 'conversation-mode-notification';
			notification.innerHTML = `
				<div class="notification-content">
					<div class="notification-icon">
						<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
							<path d="M12 2a3 3 0 0 0-3 3v7a3 3 0 0 0 6 0V5a3 3 0 0 0-3-3Z"></path>
							<path d="M19 10v2a7 7 0 0 1-14 0v-2"></path>
							<line x1="12" y1="19" x2="12" y2="22"></line>
						</svg>
					</div>
					<div class="notification-text">
						<strong>Conversation Mode Active</strong>
						<p>Speak naturally - I'll listen and respond with voice</p>
					</div>
				</div>
			`;
			
			this.refs.chatMessages.appendChild(notification);
			window.ChatUtils.scrollToBottom(this.refs.chatMessages);
			
			setTimeout(() => {
				notification.style.opacity = '0';
				setTimeout(() => notification.remove(), 300);
			}, 4000);
		}

		createConversationIndicator() {
			const existing = document.getElementById('conversationIndicator');
			if (existing) existing.remove();

			const indicator = document.createElement('div');
			indicator.id = 'conversationIndicator';
			indicator.className = 'conversation-indicator';
			indicator.innerHTML = `
				<div class="conversation-animation">
					<div class="wave-bars">
						<span class="wave-bar"></span>
						<span class="wave-bar"></span>
						<span class="wave-bar"></span>
						<span class="wave-bar"></span>
						<span class="wave-bar"></span>
					</div>
				</div>
				<div class="conversation-status-text">Listening...</div>
			`;
			
			this.refs.chatScreen.querySelector('.chat-container').appendChild(indicator);
		}

		removeConversationIndicator() {
			const indicator = document.getElementById('conversationIndicator');
			if (indicator) {
				indicator.classList.add('fade-out');
				setTimeout(() => indicator.remove(), 300);
			}
		}

		updateConversationStatus(status, state = 'listening') {
			const statusText = document.querySelector('.conversation-status-text');
			const animation = document.querySelector('.conversation-animation');
			
			if (statusText) statusText.textContent = status;
			if (animation) {
				animation.className = 'conversation-animation ' + state;
			}
		}

		async beginListening() {
			if (!this.conversationState?.isActive) return;
			if (this.conversationState.isSpeaking) return;

			console.log('[Conversation] Begin listening');
			this.conversationState.isListening = true;
			this.updateConversationStatus('Listening... speak now', 'listening');

			if (this.voiceController) {
				this.voiceController.onTranscription = async (text) => {
					await this.handleConversationTranscript(text);
				};

				await this.voiceController.start();
				this.setupVoiceActivityDetection();
			}
		}

		setupVoiceActivityDetection() {
			if (!this.voiceController?.stream) return;

			try {
				const AudioContext = window.AudioContext || window.webkitAudioContext;
				this.conversationState.audioContext = new AudioContext();
				
				const source = this.conversationState.audioContext.createMediaStreamSource(
					this.voiceController.stream
				);
				
				this.conversationState.analyser = this.conversationState.audioContext.createAnalyser();
				this.conversationState.analyser.fftSize = 512;
				source.connect(this.conversationState.analyser);

				this.monitorVoiceActivity();
				
			} catch (err) {
				console.warn('[Conversation] Voice detection setup failed:', err);
				this.startSimpleSilenceTimer();
			}
		}

		monitorVoiceActivity() {
			if (!this.conversationState?.isActive || !this.conversationState?.isListening) return;

			const analyser = this.conversationState.analyser;
			if (!analyser) return;

			const dataArray = new Uint8Array(analyser.frequencyBinCount);
			let lastSoundTime = Date.now();
			let soundDetected = false;

			const checkAudio = () => {
				if (!this.conversationState?.isActive || !this.conversationState?.isListening) return;

				analyser.getByteFrequencyData(dataArray);
				const average = dataArray.reduce((a, b) => a + b) / dataArray.length;
				
				if (average > 30) {
					lastSoundTime = Date.now();
					if (!soundDetected) {
						soundDetected = true;
						console.log('[Conversation] Voice detected');
						this.updateConversationStatus('Listening to you...', 'active');
					}
					
					if (this.conversationState.silenceTimer) {
						clearTimeout(this.conversationState.silenceTimer);
						this.conversationState.silenceTimer = null;
					}
				} else if (soundDetected) {
					const silenceDuration = Date.now() - lastSoundTime;
					
					if (silenceDuration > this.conversationState.silenceThreshold) {
						console.log('[Conversation] Silence detected, processing');
						this.finishListening();
						return;
					}
				}

				requestAnimationFrame(checkAudio);
			};

			checkAudio();
		}

		startSimpleSilenceTimer() {
			this.conversationState.silenceTimer = setTimeout(() => {
				if (this.conversationState?.isListening) {
					this.finishListening();
				}
			}, 3000);
		}

		finishListening() {
			if (!this.conversationState?.isListening) return;

			console.log('[Conversation] Finishing listening');
			this.conversationState.isListening = false;
			this.updateConversationStatus('Processing...', 'processing');

			if (this.voiceController?.isRecording) {
				this.voiceController.stop();
			}
		}

		async handleConversationTranscript(text) {
			if (!text || !this.conversationState?.isActive) return;

			console.log('[Conversation] Transcript:', text);
			
			if (this.conversationState.silenceTimer) {
				clearTimeout(this.conversationState.silenceTimer);
			}

			if (this.conversationState.audioContext) {
				this.conversationState.audioContext.close();
				this.conversationState.audioContext = null;
			}

			this.addMessage(text, 'user');
			this.updateConversationStatus('Thinking...', 'thinking');

			await this.processConversationMessage(text);
		}

		async processConversationMessage(text) {
			if (!this.conversationState?.isActive) return;

			this.showTypingIndicator();

			try {
				const { reply } = await window.BotAPI.chat(text);
				this.lastBotReply = reply || "I'm sorry, I didn't catch that.";
				
				this.hideTypingIndicator();
				this.addMessage(this.lastBotReply, 'bot');

				await this.speakConversationResponse(this.lastBotReply);
				this.waitForNextConversationInput();

			} catch (err) {
				console.error('[Conversation] Error:', err);
				this.hideTypingIndicator();
				this.addMessage('Something went wrong. Please try again.', 'bot');
				this.waitForNextConversationInput();
			}
		}

		async speakConversationResponse(text) {
			if (!this.conversationState?.isActive) return;

			console.log('[Conversation] Speaking response');
			this.conversationState.isSpeaking = true;
			this.updateConversationStatus('Speaking...', 'speaking');

			try {
				const audioBlob = await window.BotAPI.tts(text);
				const url = URL.createObjectURL(audioBlob);
				this.refs.ttsAudio.src = url;
				
				await new Promise((resolve) => {
					this.refs.ttsAudio.onended = () => {
						console.log('[Conversation] Finished speaking');
						resolve();
					};
					this.refs.ttsAudio.onerror = resolve;
					this.refs.ttsAudio.play().catch((err) => {
						console.warn('[Conversation] Play failed:', err);
						resolve();
					});
				});

			} catch (err) {
				console.warn('[Conversation] TTS failed:', err);
			} finally {
				this.conversationState.isSpeaking = false;
			}
		}

		waitForNextConversationInput() {
			if (!this.conversationState?.isActive) return;

			console.log('[Conversation] Waiting for next input');
			this.updateConversationStatus('Your turn... (speak or type)', 'waiting');

			this.conversationState.waitTimer = setTimeout(() => {
				if (this.conversationState?.isActive) {
					this.beginListening();
				}
			}, this.conversationState.waitAfterResponse);
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
			
			// Stop conversation mode if active
			if (this.ttsEnabled) {
				this.ttsEnabled = false;
				this.stopConversationMode();
			}
			
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
			
			// If in conversation mode, it's handled separately
			if (this.conversationState?.isActive) return;
			
			// Normal mode: put in input field for review
			this.refs.messageInput.value = text;
			this.refs.messageInput.focus();
			this.refs.messageInput.setSelectionRange(text.length, text.length);
		}

		handleSend() {
			const text = this.refs.messageInput.value.trim();
			if (!text) return;
			
			// If in conversation mode, handle as manual input
			if (this.conversationState?.isActive) {
				this.refs.messageInput.value = '';
				// Cancel any wait timer
				if (this.conversationState.waitTimer) {
					clearTimeout(this.conversationState.waitTimer);
				}
				// Process the typed message
				this.addMessage(text, 'user');
				this.updateConversationStatus('Thinking...', 'thinking');
				this.processConversationMessage(text);
				return;
			}
			
			// Normal mode
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
				
				if (this.ttsEnabled && !this.conversationState) {
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
			if (window.BotAPI?.mode === 'mock') return;
			
			try {
				const audioBlob = await window.BotAPI.tts(text);
				const url = URL.createObjectURL(audioBlob);
				this.refs.ttsAudio.src = url;
				await this.refs.ttsAudio.play().catch(() => {});
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
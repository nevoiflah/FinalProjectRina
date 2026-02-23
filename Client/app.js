(() => {
	const INITIAL_BOT_MESSAGE = "Welcome to Ruppin Academic Center!<br>I'm your personal Academic Advisor. I can help you check eligibility, find the right degree, or explain our preparatory programs.<br><br>How can I help you today?";
	const TRANSITION_MS = 300;

	// Logic to fetch initial message dynamically
	function getInitialMessage() {
		return window.langManager ?
			window.langManager.getText('initial_message') :
			"Welcome to Ruppin Academic Center!<br>I'm your personal Academic Advisor. I can help you check eligibility, find the right degree, or explain our preparatory programs.<br><br>How can I help you today?";
	}

	class ChatApp {
		constructor() {
			// ... existing setup ...
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

			// Wait for LanguageManager to be ready before resetting conversation
			setTimeout(() => {
				this.resetConversation();
			}, 100);

			// Listen for language changes
			window.addEventListener('languageChanged', () => {
				this.resetConversation();
			});
		}

		cacheElements() {
			const refs = {
				startScreen: document.getElementById('startScreen'),
				adminPanelBtn: document.getElementById('adminPanelBtn'),
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
				console.error(`ChatApp initialization failed â€“ missing elements: ${missing.join(', ')}`);
				return null;
			}
			return refs;
		}

		createVoiceController() {
			if (!window.ChatUtils?.VoiceInputController) return null;
			const controller = new window.ChatUtils.VoiceInputController({
				micButton: this.refs.micBtn,
				indicatorEl: this.refs.recordingIndicator,
				inputEl: this.refs.messageInput,
				onTranscription: (text) => this.handleVoiceTranscription(text),
			});
			// Save the original handler for later restoration
			this.originalOnTranscription = controller.onTranscription;
			return controller;
		}

		bindEvents() {
			const { startChatBtn, backBtn, sendBtn, micBtn, messageInput, ttsToggle, adminPanelBtn } = this.refs;

			// Admin Button Logic
			if (adminPanelBtn && this.currentUser && (this.currentUser.isAdmin || this.currentUser.IsAdmin)) {
				adminPanelBtn.classList.remove('hidden');
				adminPanelBtn.addEventListener('click', () => {
					window.location.href = 'admin.html';
				});
			}

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

			// Logout
			const logoutBtn = document.getElementById('logoutBtn');
			if (logoutBtn) {
				logoutBtn.addEventListener('click', () => {
					if (confirm(window.langManager ? window.langManager.getText('logout_confirm') : 'Are you sure you want to logout?')) {
						if (window.BotAPI?.endSession) {
							window.BotAPI.endSession();
						}
						localStorage.removeItem('chatUser');
						if (window.LanguageManager) window.LanguageManager.resetToDefault();
						window.location.replace('login.html');
					}
				});
			}

			// Quick Actions
			const quickActions = document.querySelectorAll('.chip-btn');
			quickActions.forEach(btn => {
				btn.addEventListener('click', () => {
					const action = btn.dataset.action;
					this.handleQuickAction(action);
				});
			});
		}

		handleQuickAction(action) {
			let prompt = "";
			let userText = "";

			switch (action) {
				case 'eligibility':
					userText = "I want to check my eligibility.";
					prompt = "I want to check if I can get into a degree. What grades do you need from me?";
					break;
				case 'discovery':
					userText = "Help me choose a degree.";
					prompt = "I'm not sure what to study. Can you help me find a degree based on my hobbies and interests?";
					break;
				case 'mechina':
					userText = "How can I improve my chances?";
					prompt = "My grades are low. Tell me about the Pre-Academic Preparatory Program (Mechina).";
					break;
			}

			if (prompt) {
				this.addMessage(userText, 'user');
				this.processOutgoingMessage(prompt, false); // false = don't display prompt as user message
			}
		}

		// Updated processOutgoingMessage to optionally skip adding user message (if already added)
		async processOutgoingMessage(text, displayAsUser = true) {
			if (!this.ensureAuthenticated()) return;

			if (displayAsUser) {
				this.addMessage(text, 'user');
			}

			this.showTypingIndicator();
			if (this.refs.sendBtn) this.refs.sendBtn.disabled = true;

			try {
				let payloadText = text;
				// Inject Language Instruction if specific language is selected
				if (window.langManager && window.langManager.currentLang === 'he') {
					payloadText += " (Please respond in Hebrew)";
				}

				const { reply } = await window.BotAPI.chat(payloadText);
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
				if (this.refs.sendBtn) this.refs.sendBtn.disabled = false;
			}
		}

		toggleTts() {
			this.ttsEnabled = !this.ttsEnabled;
			const { ttsToggle, chatScreen } = this.refs;

			// New overlay reference
			const voiceOverlay = document.getElementById('voiceOverlay');
			const voiceCloseBtn = document.getElementById('voiceCloseBtn');
			const voiceMicBtn = document.getElementById('voiceMicBtn');

			// Simple toggle logic
			if (this.ttsEnabled) {
				// Activate Voice Mode
				if (voiceOverlay) voiceOverlay.classList.remove('hidden');
				this.startConversationMode();

				// Bind close button dynamically if needed
				if (voiceCloseBtn) voiceCloseBtn.onclick = () => this.toggleTts(); // Toggle off
				if (voiceMicBtn) voiceMicBtn.onclick = () => {
					// Manual intervention could go here, for now just a visual indicator
				};

				// Update main Toggle Icon state
				ttsToggle.classList.add('active');
			} else {
				// Deactivate Voice Mode
				if (voiceOverlay) voiceOverlay.classList.add('hidden');
				this.stopConversationMode();
				ttsToggle.classList.remove('active');
			}
		}

		startConversationMode() {
			console.log('[Conversation] Starting immersive mode');
			this.conversationState = {
				isActive: true,
				isListening: false,
				isSpeaking: false,
				audioContext: null,
				analyser: null,
				silenceTimer: null,
				waitTimer: null,
				silenceThreshold: 1000,
				waitAfterResponse: 600,
			};

			// Start listening
			this.updateVoiceOverlayState('listening', 'Listening...');
			setTimeout(() => this.beginListening(), 500);
		}

		updateVoiceOverlayState(state, statusText) {
			const overlay = document.getElementById('voiceOverlay');
			const statusEl = document.getElementById('voiceStatus');

			if (!overlay) return;

			// Remove all states first
			overlay.classList.remove('listening', 'thinking', 'speaking');

			if (state) overlay.classList.add(state);
			if (statusEl && statusText) statusEl.textContent = statusText;
		}

		// Update existing methods to hook into new UI
		updateConversationStatus(status, state) {
			// Proxy to new method
			this.updateVoiceOverlayState(state, status);
		}

		stopConversationMode() {
			console.log('[Conversation] Stopping immersive mode');
			if (this.conversationState) {
				this.conversationState.isActive = false;
				if (this.conversationState.silenceTimer) clearTimeout(this.conversationState.silenceTimer);
				if (this.conversationState.waitTimer) clearTimeout(this.conversationState.waitTimer);
				if (this.conversationState.audioContext) this.conversationState.audioContext.close();
			}

			if (this.voiceController?.isRecording) {
				this.voiceController.stop();
			}

			// Reset Overlay
			this.updateVoiceOverlayState('', '');
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
				this.refs.ttsAudio.playbackRate = 1.2; // Speak faster

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

			// End Session on Server
			if (window.BotAPI?.endSession) {
				window.BotAPI.endSession();
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
			if (!this.refs.chatMessages) return;
			this.refs.chatMessages.innerHTML = '';
			const msg = getInitialMessage();
			this.addMessage(msg, 'bot');
			this.typingIndicator = null;
			this.lastBotReply = msg;
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

		// Logic merged into processOutgoingMessage above


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
				await this.refs.ttsAudio.play().catch(() => { });
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
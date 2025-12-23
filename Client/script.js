(() => {
	/**
	 * script.js â€” shared helpers aligned with the current chat UI.
	 * Exposes `window.ChatUtils` containing:
	 *  - appendMessage / createMessageElement DOM helpers
	 *  - scrollToBottom utility
	 *  - VoiceInputController for microphone + STT flow
	 */

	const HIDDEN_CLASS = 'hidden';

	function createMessageElement(text, sender) {
		const wrapper = document.createElement('div');
		wrapper.className = `message ${sender}-message`;

		if (sender === 'bot') {
			const avatar = document.createElement('div');
			avatar.className = 'message-avatar';
			// Ruppin 'R' Avatar
			avatar.textContent = 'R';
			avatar.style.background = '#1a365d'; // Ruppin Blue
			avatar.style.color = '#ffffff';
			avatar.style.fontWeight = 'bold';
			avatar.style.fontFamily = 'Arial, sans-serif';
			wrapper.appendChild(avatar);
		}

		const content = document.createElement('div');
		content.className = 'message-content';
		content.innerHTML = text;
		wrapper.appendChild(content);

		if (sender === 'user') {
			const avatar = document.createElement('div');
			avatar.className = 'message-avatar';
			// Emoji removed
			// avatar.textContent = '👤';
			avatar.innerHTML = `<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path><circle cx="12" cy="7" r="4"></circle></svg>`;
			wrapper.appendChild(avatar);
		}

		return wrapper;
	}

	function appendMessage(container, text, sender) {
		if (!container) return null;
		const node = createMessageElement(text, sender);
		container.appendChild(node);
		scrollToBottom(container);
		return node;
	}

	function scrollToBottom(container) {
		if (!container) return;
		container.scrollTop = container.scrollHeight;
	}

	class VoiceInputController {
		constructor({ micButton, indicatorEl, inputEl, onTranscription } = {}) {
			this.micButton = micButton;
			this.indicatorEl = indicatorEl;
			this.inputEl = inputEl;
			this.onTranscription = onTranscription;

			this.mediaRecorder = null;
			this.stream = null;
			this.audioChunks = [];
			this.isRecording = false;
		}

		async toggle() {
			if (this.isRecording) {
				this.stop();
			} else {
				await this.start();
			}
		}

		async start() {
			if (this.isRecording) return;
			if (!navigator.mediaDevices?.getUserMedia) {
				alert('Microphone access is not supported in this browser.');
				return;
			}
			try {
				this.stream = await navigator.mediaDevices.getUserMedia({ audio: true });
				this.audioChunks = [];
				this.mediaRecorder = new MediaRecorder(this.stream);
				this.mediaRecorder.addEventListener('dataavailable', (event) => {
					if (event.data && event.data.size > 0) {
						this.audioChunks.push(event.data);
					}
				});
				this.mediaRecorder.addEventListener('stop', () => this.handleStop());
				this.mediaRecorder.start();
				this.isRecording = true;
				this.updateUI(true);
			} catch (err) {
				console.error('Microphone access denied:', err);
				alert('Please allow microphone access to use voice chat');
				this.cleanup();
			}
		}

		stop() {
			if (this.mediaRecorder && this.mediaRecorder.state === 'recording') {
				this.mediaRecorder.stop();
				return;
			}
			this.cleanup();
		}

		async handleStop() {
			try {
				if (!this.audioChunks.length) {
					console.warn('No audio chunks recorded');
					return;
				}

				const audioBlob = new Blob(this.audioChunks, { type: 'audio/webm' });

				// Check for minimum audio length (approx < 0.2s check by size)
				// Low quality webm is ~4KB/s. 0.1s is ~400 bytes. Be safe with 1KB.
				if (audioBlob.size < 1000) {
					console.warn('Audio too short:', audioBlob.size);
					alert('Recording too short. Please hold the button and speak clearly.');
					return;
				}

				console.log('Audio recorded:', {
					size: audioBlob.size,
					type: audioBlob.type,
					chunks: this.audioChunks.length
				});

				if (!window.BotAPI?.stt) {
					console.error('BotAPI.stt is not available');
					alert('Speech-to-text service is not available. Please check your setup.');
					return;
				}

				const result = await window.BotAPI.stt(audioBlob);
				console.log('STT result:', JSON.stringify(result));
				console.log('Transcript from result:', result?.transcript);

				if (result?.transcript && result.transcript.trim() !== '') {
					console.log('Valid transcript received, calling onTranscription');
					if (typeof this.onTranscription === 'function') {
						this.onTranscription(result.transcript);
					} else if (this.inputEl) {
						this.inputEl.value = result.transcript;
						this.inputEl.focus();
					}
				} else {
					console.warn('No valid transcript in result. Result:', result);
					alert('No speech detected or transcript was empty. Please try speaking again more clearly.');
				}
			} catch (err) {
				console.error('STT failed:', err);
				alert(`Could not transcribe the audio: ${err.message}\n\nPlease check the console for details.`);
			} finally {
				this.cleanup();
			}
		}

		updateUI(isRecording) {
			if (this.micButton) {
				this.micButton.classList.toggle('recording', isRecording);
				this.micButton.setAttribute('aria-pressed', String(isRecording));
			}
			if (this.indicatorEl) {
				this.indicatorEl.classList.toggle(HIDDEN_CLASS, !isRecording);
			}
			if (this.inputEl) {
				this.inputEl.placeholder = isRecording ? 'Listening...' : 'Type your message...';
			}
		}

		cleanup() {
			this.isRecording = false;
			this.updateUI(false);
			if (this.mediaRecorder) {
				this.mediaRecorder.ondataavailable = null;
				this.mediaRecorder.onstop = null;
				this.mediaRecorder = null;
			}
			if (this.stream) {
				this.stream.getTracks().forEach(track => track.stop());
				this.stream = null;
			}
			this.audioChunks = [];
		}
	}

	window.ChatUtils = Object.freeze({
		appendMessage,
		createMessageElement,
		scrollToBottom,
		VoiceInputController,
	});
})();
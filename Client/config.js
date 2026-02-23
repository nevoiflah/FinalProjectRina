(() => {
	const STORAGE_KEY = 'chatApiBase';
	const params = new URLSearchParams(window.location.search);
	const queryBase = params.get('apiBase');

	// Hardcoded for stability - avoid sticky localStorage issues
	let base = window.__CHAT_API_BASE__ || 'http://localhost:5000';

	if (queryBase) {
		base = queryBase;
	}

	function setBase(url) {
		if (url && url.trim()) {
			base = url.trim().replace(/\/$/, '');
			localStorage.setItem(STORAGE_KEY, base);
		} else {
			base = '';
			localStorage.removeItem(STORAGE_KEY);
		}

		window.__CHAT_API_BASE__ = base;
	}

	window.__CHAT_API_BASE__ = base;
	window.ChatConfig = Object.freeze({
		getApiBase: () => base,
		setApiBase: setBase,
	});
})();

(() => {
	const STORAGE_KEY = 'chatApiBase';
	const params = new URLSearchParams(window.location.search);
	const queryBase = params.get('apiBase');

	let base = window.__CHAT_API_BASE__ || localStorage.getItem(STORAGE_KEY) || '';
	if (queryBase) {
		base = queryBase;
		localStorage.setItem(STORAGE_KEY, base);
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

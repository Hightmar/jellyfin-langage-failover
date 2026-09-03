const pluginId = 'a5b6c7d8-1234-5678-9abc-def012345678';

// Populated at viewshow from ApiClient.getCultures(). Falls back to a small
// hardcoded list if the API call fails so the page still works.
let LANG_LIST = [];
let LANG_NAMES = {};

const FALLBACK_LANGS = [
    { code: 'en', name: 'English' }, { code: 'fr', name: 'French' },
    { code: 'es', name: 'Spanish' }, { code: 'de', name: 'German' },
    { code: 'it', name: 'Italian' }, { code: 'pt', name: 'Portuguese' },
    { code: 'ru', name: 'Russian' }, { code: 'ja', name: 'Japanese' },
    { code: 'ko', name: 'Korean' }, { code: 'zh', name: 'Chinese' },
    { code: 'ar', name: 'Arabic' }, { code: 'hi', name: 'Hindi' },
    { code: 'nl', name: 'Dutch' }, { code: 'pl', name: 'Polish' },
    { code: 'sv', name: 'Swedish' }, { code: 'tr', name: 'Turkish' }
];

let currentConfig = null;
let currentUserId = null;
let currentView = null;
let culturesLoaded = false;

// --- Language catalogue ---

function applyLangList(list) {
    LANG_LIST = list.slice().sort((a, b) => a.name.localeCompare(b.name));
    LANG_NAMES = {};
    LANG_LIST.forEach((l) => { LANG_NAMES[l.code] = l.name; });
}

function loadCultures() {
    if (culturesLoaded) return Promise.resolve();
    return ApiClient.getCultures().then((cultures) => {
        const byKey = {};
        cultures.forEach((c) => {
            const code = c.TwoLetterISOLanguageName || c.ThreeLetterISOLanguageName;
            if (!code || byKey[code]) return;
            byKey[code] = { code: code, name: c.DisplayName || c.Name || code };
        });
        applyLangList(Object.keys(byKey).map((k) => byKey[k]));
        culturesLoaded = true;
    }).catch(() => {
        applyLangList(FALLBACK_LANGS);
        culturesLoaded = true;
    });
}

function getLangName(code) {
    return LANG_NAMES[code] || String(code).toUpperCase();
}

function escapeHtml(s) {
    return String(s === null || s === undefined ? '' : s).replace(/[&<>"']/g, (c) =>
        ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
}

// --- Language list rendering ---
//
// A "list pair" is a <ul class="lf-chips"> of ordered languages plus the
// <select> beside it offering the codes not already in the list. Every
// operation is keyed off the <ul> element itself rather than an element id or
// an encoded type string, so the two global lists and every per-series
// override list go through exactly the same code.

function selectForList(listEl) {
    return listEl.parentElement
        ? listEl.parentElement.querySelector('.lf-add-row select')
        : null;
}

function getCodesFromList(listEl) {
    return Array.from(listEl.querySelectorAll('.lf-chip-code'), (el) => el.textContent.trim());
}

function refreshLangSelect(selectEl, excludedCodes) {
    const excluded = new Set(excludedCodes || []);
    selectEl.innerHTML = '';

    const placeholder = document.createElement('option');
    placeholder.value = '';
    placeholder.textContent = 'Select a language…';
    selectEl.appendChild(placeholder);

    LANG_LIST.forEach((l) => {
        if (excluded.has(l.code)) return;
        const opt = document.createElement('option');
        opt.value = l.code;
        opt.textContent = `${l.name} (${l.code})`;
        selectEl.appendChild(opt);
    });
}

function renderLangList(listEl, languages) {
    const codes = languages || [];
    listEl.innerHTML = '';

    if (codes.length === 0) {
        const empty = document.createElement('li');
        empty.className = 'lf-empty';
        empty.textContent = 'No languages added yet.';
        listEl.appendChild(empty);
        setupListDragDrop(listEl);
        return;
    }

    codes.forEach((code, index) => {
        const li = document.createElement('li');
        li.className = 'lf-chip';
        li.draggable = true;
        li.dataset.index = index;
        li.innerHTML =
            '<span class="lf-drag-handle" title="Drag to reorder">&#x2630;</span>' +
            `<span class="lf-chip-priority">${index + 1}</span>` +
            `<span class="lf-chip-name">${escapeHtml(getLangName(code))}` +
                `<span class="lf-chip-code">${escapeHtml(code)}</span></span>` +
            '<span class="lf-chip-actions">' +
                `<button class="lf-icon-btn btnMoveUp" title="Move up" data-index="${index}"` +
                    `${index === 0 ? ' disabled' : ''}>&#8593;</button>` +
                `<button class="lf-icon-btn btnMoveDown" title="Move down" data-index="${index}"` +
                    `${index === codes.length - 1 ? ' disabled' : ''}>&#8595;</button>` +
                `<button class="lf-icon-btn lf-danger btnRemove" title="Remove" data-index="${index}">&times;</button>` +
            '</span>';
        listEl.appendChild(li);
    });

    setupListDragDrop(listEl);
}

/** Re-renders a list and the select beside it from a new set of codes. */
function renderPair(listEl, codes) {
    renderLangList(listEl, codes);
    const selectEl = selectForList(listEl);
    if (selectEl) refreshLangSelect(selectEl, codes);
}

function setupListDragDrop(listEl) {
    if (listEl.dataset.dragBound === '1') return;
    listEl.dataset.dragBound = '1';

    listEl.addEventListener('dragstart', (e) => {
        const chip = e.target.closest('.lf-chip');
        if (!chip) return;
        chip.classList.add('lf-dragging');
        e.dataTransfer.effectAllowed = 'move';
        try { e.dataTransfer.setData('text/plain', chip.dataset.index); } catch (_) { /* not all browsers allow this here */ }
    });

    listEl.addEventListener('dragend', () => {
        listEl.querySelectorAll('.lf-dragging, .lf-drag-over').forEach((c) => {
            c.classList.remove('lf-dragging', 'lf-drag-over');
        });
    });

    listEl.addEventListener('dragover', (e) => {
        const chip = e.target.closest('.lf-chip');
        if (!chip || chip.classList.contains('lf-dragging')) return;
        e.preventDefault();
        e.dataTransfer.dropEffect = 'move';
        listEl.querySelectorAll('.lf-chip.lf-drag-over').forEach((c) => {
            if (c !== chip) c.classList.remove('lf-drag-over');
        });
        chip.classList.add('lf-drag-over');
    });

    listEl.addEventListener('dragleave', (e) => {
        const chip = e.target.closest('.lf-chip');
        if (chip && !chip.contains(e.relatedTarget)) chip.classList.remove('lf-drag-over');
    });

    listEl.addEventListener('drop', (e) => {
        e.preventDefault();
        const dragging = listEl.querySelector('.lf-chip.lf-dragging');
        const target = e.target.closest('.lf-chip');
        if (!dragging || !target || dragging === target) return;

        const fromIdx = parseInt(dragging.dataset.index, 10);
        const toIdx = parseInt(target.dataset.index, 10);

        const codes = getCodesFromList(listEl);
        const moved = codes.splice(fromIdx, 1)[0];
        codes.splice(fromIdx < toIdx ? toIdx - 1 : toIdx, 0, moved);

        renderPair(listEl, codes);
    });
}

/**
 * Handles a move-up / move-down / remove click on any language list, global or
 * per-series. Which list the click belongs to is read off the DOM.
 */
function handleListAction(e) {
    const btn = e.target.closest('button');
    if (!btn) return;

    const listEl = btn.closest('.lf-chips');
    if (!listEl) return;

    const index = parseInt(btn.dataset.index, 10);
    if (Number.isNaN(index)) return;

    const codes = getCodesFromList(listEl);

    if (btn.classList.contains('btnMoveUp') && index > 0) {
        [codes[index - 1], codes[index]] = [codes[index], codes[index - 1]];
    } else if (btn.classList.contains('btnMoveDown') && index < codes.length - 1) {
        [codes[index + 1], codes[index]] = [codes[index], codes[index + 1]];
    } else if (btn.classList.contains('btnRemove')) {
        codes.splice(index, 1);
    } else {
        return;
    }

    renderPair(listEl, codes);
}

/** Handles an "+ Add" click next to any language list, global or per-series. */
function handleAddLanguage(btn) {
    const addRow = btn.closest('.lf-add-row');
    if (!addRow || !addRow.parentElement) return;

    const selectEl = addRow.querySelector('select');
    const listEl = addRow.parentElement.querySelector('.lf-chips');
    if (!selectEl || !listEl || !selectEl.value) return;

    const codes = getCodesFromList(listEl);
    if (codes.indexOf(selectEl.value) === -1) codes.push(selectEl.value);
    renderPair(listEl, codes);
}

// --- Configuration model ---

function stripHyphens(id) {
    return id.replace(/-/g, '');
}

function findUserPrefs(userId) {
    if (!currentConfig || !Array.isArray(currentConfig.UserPreferences)) return null;
    const key = stripHyphens(userId);
    return currentConfig.UserPreferences.find((p) => p.UserId === key) || null;
}

function getOrCreateUserPrefs(userId) {
    return findUserPrefs(userId) || {
        UserId: stripHyphens(userId),
        AudioLanguages: [],
        SubtitleLanguages: [],
        PreferNonForcedSubtitles: true,
        PreferOriginalAudio: false,
        PreferForcedWhenAudioMatches: true,
        Enabled: true,
        SeriesOverrides: []
    };
}

function saveUserPrefsToConfig(userId, prefs) {
    if (!currentConfig) currentConfig = { UserPreferences: [] };
    if (!Array.isArray(currentConfig.UserPreferences)) currentConfig.UserPreferences = [];

    const key = stripHyphens(userId);
    prefs.UserId = key;

    const idx = currentConfig.UserPreferences.findIndex((p) => p.UserId === key);
    if (idx >= 0) {
        currentConfig.UserPreferences[idx] = prefs;
    } else {
        currentConfig.UserPreferences.push(prefs);
    }
}

// --- Series overrides ---

function renderSeriesOverrides(overrides) {
    const container = currentView.querySelector('#seriesOverridesList');
    container.innerHTML = '';

    if (!overrides || overrides.length === 0) {
        const empty = document.createElement('div');
        empty.className = 'lf-empty';
        empty.textContent = 'No series overrides. Use the search above to add one.';
        container.appendChild(empty);
        return;
    }

    overrides.forEach((ov) => {
        const div = document.createElement('div');
        div.className = 'lf-series-override';
        // The block carries its own identity so reading the UI back never has to
        // assume the DOM order still lines up with the saved configuration array.
        div.dataset.seriesId = ov.SeriesId || '';
        div.dataset.seriesName = ov.SeriesName || '';

        div.innerHTML =
            '<div class="lf-series-override-header">' +
                `<span class="lf-series-override-title">${escapeHtml(ov.SeriesName || ov.SeriesId)}</span>` +
                '<button class="lf-icon-btn lf-danger btnRemoveOverride" type="button" title="Remove override">&times;</button>' +
            '</div>' +
            '<div class="lf-row">' +
                '<div class="lf-col">' +
                    '<div class="lf-subsection-title">Audio</div>' +
                    '<ul class="lf-chips" data-ov-role="audio"></ul>' +
                    '<div class="lf-add-row"><select></select>' +
                        '<button class="btnAddLang" type="button">+ Add</button></div>' +
                '</div>' +
                '<div class="lf-col">' +
                    '<div class="lf-subsection-title">Subtitles</div>' +
                    '<ul class="lf-chips" data-ov-role="subtitle"></ul>' +
                    '<div class="lf-add-row"><select></select>' +
                        '<button class="btnAddLang" type="button">+ Add</button></div>' +
                '</div>' +
            '</div>';

        container.appendChild(div);

        renderPair(div.querySelector('[data-ov-role="audio"]'), ov.AudioLanguages || []);
        renderPair(div.querySelector('[data-ov-role="subtitle"]'), ov.SubtitleLanguages || []);
    });
}

function addSeriesOverride(seriesId, seriesName) {
    const prefs = getCurrentPrefs();
    if (!prefs.SeriesOverrides) prefs.SeriesOverrides = [];

    if (prefs.SeriesOverrides.some((o) => o.SeriesId === seriesId)) {
        showStatus(`Override for "${seriesName}" already exists.`, true);
        return;
    }

    prefs.SeriesOverrides.push({
        SeriesId: seriesId,
        SeriesName: seriesName,
        AudioLanguages: [],
        SubtitleLanguages: []
    });

    saveUserPrefsToConfig(currentUserId, prefs);
    renderSeriesOverrides(prefs.SeriesOverrides);
}

function removeSeriesOverride(seriesId) {
    const prefs = getCurrentPrefs();
    if (!prefs.SeriesOverrides) return;

    const idx = prefs.SeriesOverrides.findIndex((o) => o.SeriesId === seriesId);
    if (idx < 0) return;

    prefs.SeriesOverrides.splice(idx, 1);
    saveUserPrefsToConfig(currentUserId, prefs);
    renderSeriesOverrides(prefs.SeriesOverrides);
}

function searchSeries(query) {
    const resultsList = currentView.querySelector('#seriesSearchResults');
    if (!query || query.length < 2) {
        resultsList.style.display = 'none';
        return;
    }

    const showMessage = (text) => {
        resultsList.innerHTML = '';
        const li = document.createElement('li');
        li.className = 'lf-no-results';
        li.textContent = text;
        resultsList.appendChild(li);
        resultsList.style.display = 'block';
    };

    ApiClient.getItems(ApiClient.getCurrentUserId(), {
        SearchTerm: query,
        IncludeItemTypes: 'Series',
        Recursive: true,
        Limit: 10
    }).then((result) => {
        if (!result.Items || result.Items.length === 0) {
            showMessage('No series found');
            return;
        }

        resultsList.innerHTML = '';
        result.Items.forEach((series) => {
            const li = document.createElement('li');
            li.textContent = series.Name + (series.ProductionYear ? ` (${series.ProductionYear})` : '');
            li.dataset.seriesId = stripHyphens(series.Id);
            li.dataset.seriesName = series.Name;
            li.addEventListener('click', () => {
                addSeriesOverride(li.dataset.seriesId, li.dataset.seriesName);
                resultsList.style.display = 'none';
                currentView.querySelector('#seriesSearchInput').value = '';
            });
            resultsList.appendChild(li);
        });
        resultsList.style.display = 'block';
    }).catch((err) => {
        console.error('Language Failover series search error:', err);
        showMessage('Search failed. Check the server logs.');
    });
}

// --- Reading and writing the form ---

function globalList(type) {
    return currentView.querySelector(type === 'audio' ? '#audioLangList' : '#subtitleLangList');
}

function getSeriesOverridesFromUI() {
    const blocks = currentView.querySelectorAll('#seriesOverridesList .lf-series-override');
    return Array.from(blocks, (block) => {
        const audioList = block.querySelector('[data-ov-role="audio"]');
        const subList = block.querySelector('[data-ov-role="subtitle"]');
        return {
            SeriesId: block.dataset.seriesId || '',
            SeriesName: block.dataset.seriesName || '',
            AudioLanguages: audioList ? getCodesFromList(audioList) : [],
            SubtitleLanguages: subList ? getCodesFromList(subList) : []
        };
    });
}

function getCurrentPrefs() {
    return {
        AudioLanguages: getCodesFromList(globalList('audio')),
        SubtitleLanguages: getCodesFromList(globalList('subtitle')),
        PreferNonForcedSubtitles: currentView.querySelector('#chkPreferNonForced').checked,
        PreferOriginalAudio: currentView.querySelector('#chkPreferOriginal').checked,
        PreferForcedWhenAudioMatches: currentView.querySelector('#chkPreferForcedWhenAudioMatches').checked,
        Enabled: currentView.querySelector('#chkEnabled').checked,
        SeriesOverrides: getSeriesOverridesFromUI()
    };
}

function loadUserPrefs() {
    const view = currentView;
    const prefs = getOrCreateUserPrefs(currentUserId);

    view.querySelector('#chkEnabled').checked = prefs.Enabled !== false;
    view.querySelector('#chkPreferNonForced').checked = prefs.PreferNonForcedSubtitles !== false;
    view.querySelector('#chkPreferOriginal').checked = prefs.PreferOriginalAudio === true;
    view.querySelector('#chkPreferForcedWhenAudioMatches').checked = prefs.PreferForcedWhenAudioMatches !== false;

    renderPair(globalList('audio'), prefs.AudioLanguages || []);
    renderPair(globalList('subtitle'), prefs.SubtitleLanguages || []);
    renderSeriesOverrides(prefs.SeriesOverrides || []);
}

let statusTimer = null;

function showStatus(msg, isError) {
    const el = currentView.querySelector('#statusMessage');
    el.textContent = msg;
    el.className = `lf-status ${isError ? 'lf-status-error' : 'lf-status-success'}`;

    clearTimeout(statusTimer);
    statusTimer = setTimeout(() => { el.className = 'lf-status'; }, 3000);
}

function doSave() {
    if (!currentUserId) return;
    saveUserPrefsToConfig(currentUserId, getCurrentPrefs());

    ApiClient.updatePluginConfiguration(pluginId, currentConfig)
        .then(() => ApiClient.getPluginConfiguration(pluginId))
        .then((config) => {
            currentConfig = config;
            loadUserPrefs();
            showStatus('Configuration saved.', false);
        })
        .catch((err) => {
            console.error('Language Failover save error:', err);
            showStatus('Error saving configuration.', true);
        });
}

export default function (view) {
    currentView = view;

    view.addEventListener('viewshow', () => {
        loadCultures()
            .then(() => ApiClient.getUsers())
            .then((users) => {
                const selectUser = view.querySelector('#selectUser');
                const previousUserId = currentUserId;

                selectUser.innerHTML = '';
                users.forEach((user) => {
                    const opt = document.createElement('option');
                    opt.value = user.Id;
                    opt.textContent = user.Name;
                    selectUser.appendChild(opt);
                });

                return ApiClient.getPluginConfiguration(pluginId).then((config) => {
                    currentConfig = config;
                    if (previousUserId && users.some((u) => u.Id === previousUserId)) {
                        selectUser.value = previousUserId;
                        currentUserId = previousUserId;
                    } else if (users.length > 0) {
                        currentUserId = users[0].Id;
                    }
                    loadUserPrefs();
                });
            })
            .catch((err) => {
                console.error('Language Failover load error:', err);
                showStatus('Failed to load configuration.', true);
            });
    });

    view.querySelector('#selectUser').addEventListener('change', function () {
        if (currentUserId) saveUserPrefsToConfig(currentUserId, getCurrentPrefs());
        currentUserId = this.value;
        loadUserPrefs();
    });

    // One delegated handler for every language list on the page — the two global
    // ones and every per-series override. Which list a click belongs to is read
    // off the DOM, so adding a list needs no wiring here.
    view.addEventListener('click', (e) => {
        const btn = e.target.closest('button');
        if (!btn) return;

        if (btn.classList.contains('btnRemoveOverride')) {
            const block = btn.closest('.lf-series-override');
            if (block) removeSeriesOverride(block.dataset.seriesId);
        } else if (btn.classList.contains('btnAddLang')) {
            handleAddLanguage(btn);
        } else {
            handleListAction(e);
        }
    });

    // Close the series search dropdown on any click outside it. Bound to the
    // view rather than to document, so it dies with the view instead of piling
    // up on every navigation to this page.
    view.addEventListener('click', (e) => {
        if (e.target.closest('.lf-search-wrapper')) return;
        const results = view.querySelector('#seriesSearchResults');
        if (results) results.style.display = 'none';
    });

    let searchTimeout = null;
    const searchInput = view.querySelector('#seriesSearchInput');

    searchInput.addEventListener('input', function () {
        const query = this.value.trim();
        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(() => searchSeries(query), 300);
    });

    searchInput.addEventListener('focus', function () {
        if (this.value.trim().length >= 2) {
            view.querySelector('#seriesSearchResults').style.display = 'block';
        }
    });

    view.querySelector('#btnSave').addEventListener('click', doSave);
}

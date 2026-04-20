var pluginId = 'a5b6c7d8-1234-5678-9abc-def012345678';

var LANG_NAMES = {
    zh: 'Chinese', ko: 'Korean', ja: 'Japanese', en: 'English', fr: 'French',
    de: 'German', es: 'Spanish', pt: 'Portuguese', it: 'Italian', ru: 'Russian',
    ar: 'Arabic', hi: 'Hindi', th: 'Thai', vi: 'Vietnamese', pl: 'Polish',
    nl: 'Dutch', sv: 'Swedish', no: 'Norwegian', da: 'Danish', fi: 'Finnish'
};

var LANG_CODES = Object.keys(LANG_NAMES);

var currentConfig = null;
var currentUserId = null;
var currentView = null;

function getLangName(code) {
    return LANG_NAMES[code] || code.toUpperCase();
}

function escapeHtml(s) {
    return String(s || '').replace(/[&<>"']/g, function (c) {
        return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c];
    });
}

function renderLangList(listEl, languages, type) {
    listEl.innerHTML = '';
    if (!languages || languages.length === 0) {
        var empty = document.createElement('li');
        empty.className = 'lf-empty';
        empty.textContent = 'No languages added yet.';
        listEl.appendChild(empty);
        return;
    }
    languages.forEach(function (code, index) {
        var li = document.createElement('li');
        li.className = 'lf-chip';
        li.innerHTML =
            '<span class="lf-chip-priority">' + (index + 1) + '</span>' +
            '<span class="lf-chip-name">' + escapeHtml(getLangName(code)) +
                '<span class="lf-chip-code">' + escapeHtml(code) + '</span></span>' +
            '<span class="lf-chip-actions">' +
                '<button class="lf-icon-btn btnMoveUp" title="Move up" data-type="' + type + '" data-index="' + index + '"' +
                    (index === 0 ? ' disabled' : '') + '>&#8593;</button>' +
                '<button class="lf-icon-btn btnMoveDown" title="Move down" data-type="' + type + '" data-index="' + index + '"' +
                    (index === languages.length - 1 ? ' disabled' : '') + '>&#8595;</button>' +
                '<button class="lf-icon-btn lf-danger btnRemove" title="Remove" data-type="' + type + '" data-index="' + index + '">&times;</button>' +
            '</span>';
        listEl.appendChild(li);
    });
}

function refreshLangSelect(selectEl, excludedCodes) {
    var excluded = {};
    (excludedCodes || []).forEach(function (c) { excluded[c] = true; });
    selectEl.innerHTML = '';
    var placeholder = document.createElement('option');
    placeholder.value = '';
    placeholder.textContent = 'Select a language…';
    selectEl.appendChild(placeholder);
    LANG_CODES.forEach(function (code) {
        if (excluded[code]) return;
        var opt = document.createElement('option');
        opt.value = code;
        opt.textContent = getLangName(code) + ' (' + code + ')';
        selectEl.appendChild(opt);
    });
}

function getCodesFromList(listEl) {
    var codes = [];
    listEl.querySelectorAll('.lf-chip').forEach(function (chip) {
        var codeEl = chip.querySelector('.lf-chip-code');
        if (codeEl) codes.push(codeEl.textContent.trim());
    });
    return codes;
}

function findUserPrefs(userId) {
    if (!currentConfig || !Array.isArray(currentConfig.UserPreferences)) {
        return null;
    }
    var key = userId.replace(/-/g, '');
    return currentConfig.UserPreferences.find(function (p) {
        return p.UserId === key;
    }) || null;
}

function getOrCreateUserPrefs(userId) {
    var found = findUserPrefs(userId);
    if (found) return found;
    return {
        UserId: userId.replace(/-/g, ''),
        AudioLanguages: [],
        SubtitleLanguages: [],
        PreferNonForcedSubtitles: true,
        Enabled: true,
        SeriesOverrides: []
    };
}

function saveUserPrefsToConfig(userId, prefs) {
    if (!currentConfig) currentConfig = { UserPreferences: [] };
    if (!Array.isArray(currentConfig.UserPreferences)) currentConfig.UserPreferences = [];
    var key = userId.replace(/-/g, '');
    prefs.UserId = key;
    var idx = currentConfig.UserPreferences.findIndex(function (p) { return p.UserId === key; });
    if (idx >= 0) {
        currentConfig.UserPreferences[idx] = prefs;
    } else {
        currentConfig.UserPreferences.push(prefs);
    }
}

// --- Series Overrides ---

function renderSeriesOverrides(overrides) {
    var container = currentView.querySelector('#seriesOverridesList');
    container.innerHTML = '';
    if (!overrides || overrides.length === 0) {
        var empty = document.createElement('div');
        empty.className = 'lf-empty';
        empty.textContent = 'No series overrides. Use the search above to add one.';
        container.appendChild(empty);
        return;
    }
    overrides.forEach(function (ov, ovIndex) {
        var div = document.createElement('div');
        div.className = 'lf-series-override';
        div.dataset.index = ovIndex;

        var audioListId = 'soAudio_' + ovIndex;
        var subListId = 'soSub_' + ovIndex;
        var audioSelId = 'soAudioSel_' + ovIndex;
        var subSelId = 'soSubSel_' + ovIndex;

        div.innerHTML =
            '<div class="lf-series-override-header">' +
                '<span class="lf-series-override-title">' + escapeHtml(ov.SeriesName || ov.SeriesId) + '</span>' +
                '<button class="lf-icon-btn lf-danger btnRemoveOverride" title="Remove override" data-ov-index="' + ovIndex + '">&times;</button>' +
            '</div>' +
            '<div class="lf-row">' +
                '<div class="lf-col">' +
                    '<div class="lf-subsection-title">Audio</div>' +
                    '<ul id="' + audioListId + '" class="lf-chips"></ul>' +
                    '<div class="lf-add-row">' +
                        '<select id="' + audioSelId + '"></select>' +
                        '<button class="btnAddOvLang" data-ov-index="' + ovIndex + '" data-ov-type="audio">+ Add</button>' +
                    '</div>' +
                '</div>' +
                '<div class="lf-col">' +
                    '<div class="lf-subsection-title">Subtitles</div>' +
                    '<ul id="' + subListId + '" class="lf-chips"></ul>' +
                    '<div class="lf-add-row">' +
                        '<select id="' + subSelId + '"></select>' +
                        '<button class="btnAddOvLang" data-ov-index="' + ovIndex + '" data-ov-type="subtitle">+ Add</button>' +
                    '</div>' +
                '</div>' +
            '</div>';

        container.appendChild(div);

        var audioLangs = ov.AudioLanguages || [];
        var subLangs = ov.SubtitleLanguages || [];
        renderLangList(div.querySelector('#' + audioListId), audioLangs, 'ov_' + ovIndex + '_audio');
        renderLangList(div.querySelector('#' + subListId), subLangs, 'ov_' + ovIndex + '_subtitle');
        refreshLangSelect(div.querySelector('#' + audioSelId), audioLangs);
        refreshLangSelect(div.querySelector('#' + subSelId), subLangs);
    });
}

function handleOverrideListAction(e) {
    var btn = e.target.closest('button');
    if (!btn) return;

    var type = btn.dataset.type;
    if (!type || !type.indexOf || type.indexOf('ov_') !== 0) return;

    var index = parseInt(btn.dataset.index, 10);
    var parts = type.split('_');
    var ovIndex = parseInt(parts[1], 10);
    var langType = parts[2];

    var listId = langType === 'audio' ? 'soAudio_' + ovIndex : 'soSub_' + ovIndex;
    var selId = langType === 'audio' ? 'soAudioSel_' + ovIndex : 'soSubSel_' + ovIndex;
    var listEl = currentView.querySelector('#' + listId);
    var selectEl = currentView.querySelector('#' + selId);
    if (!listEl) return;

    var codes = getCodesFromList(listEl);

    if (btn.classList.contains('btnMoveUp') && index > 0) {
        var tmp = codes[index - 1];
        codes[index - 1] = codes[index];
        codes[index] = tmp;
    } else if (btn.classList.contains('btnMoveDown') && index < codes.length - 1) {
        var tmp2 = codes[index + 1];
        codes[index + 1] = codes[index];
        codes[index] = tmp2;
    } else if (btn.classList.contains('btnRemove')) {
        codes.splice(index, 1);
    }

    renderLangList(listEl, codes, type);
    if (selectEl) refreshLangSelect(selectEl, codes);
}

function loadUserPrefs() {
    var view = currentView;
    var prefs = getOrCreateUserPrefs(currentUserId);
    view.querySelector('#chkEnabled').checked = prefs.Enabled !== false;
    view.querySelector('#chkPreferNonForced').checked = prefs.PreferNonForcedSubtitles !== false;

    var audioLangs = prefs.AudioLanguages || [];
    var subLangs = prefs.SubtitleLanguages || [];
    renderLangList(view.querySelector('#audioLangList'), audioLangs, 'audio');
    renderLangList(view.querySelector('#subtitleLangList'), subLangs, 'subtitle');
    refreshLangSelect(view.querySelector('#audioLangSelect'), audioLangs);
    refreshLangSelect(view.querySelector('#subtitleLangSelect'), subLangs);
    renderSeriesOverrides(prefs.SeriesOverrides || []);
}

function getListCodes(type) {
    var listEl = currentView.querySelector(type === 'audio' ? '#audioLangList' : '#subtitleLangList');
    return getCodesFromList(listEl);
}

function getSeriesOverridesFromUI() {
    var overrides = [];
    var container = currentView.querySelector('#seriesOverridesList');
    var blocks = container.querySelectorAll('.lf-series-override');
    blocks.forEach(function (block, idx) {
        var nameEl = block.querySelector('.lf-series-override-title');
        var audioList = block.querySelector('#soAudio_' + idx);
        var subList = block.querySelector('#soSub_' + idx);

        var prefs = getOrCreateUserPrefs(currentUserId);
        var existingOv = (prefs.SeriesOverrides || [])[idx];

        overrides.push({
            SeriesId: existingOv ? existingOv.SeriesId : '',
            SeriesName: nameEl ? nameEl.textContent : '',
            AudioLanguages: audioList ? getCodesFromList(audioList) : [],
            SubtitleLanguages: subList ? getCodesFromList(subList) : []
        });
    });
    return overrides;
}

function getCurrentPrefs() {
    return {
        AudioLanguages: getListCodes('audio'),
        SubtitleLanguages: getListCodes('subtitle'),
        PreferNonForcedSubtitles: currentView.querySelector('#chkPreferNonForced').checked,
        Enabled: currentView.querySelector('#chkEnabled').checked,
        SeriesOverrides: getSeriesOverridesFromUI()
    };
}

function handleListAction(e) {
    var btn = e.target.closest('button');
    if (!btn) return;

    var type = btn.dataset.type;
    if (type !== 'audio' && type !== 'subtitle') return;
    var index = parseInt(btn.dataset.index, 10);
    var codes = getListCodes(type);

    if (btn.classList.contains('btnMoveUp') && index > 0) {
        var tmp = codes[index - 1];
        codes[index - 1] = codes[index];
        codes[index] = tmp;
    } else if (btn.classList.contains('btnMoveDown') && index < codes.length - 1) {
        var tmp2 = codes[index + 1];
        codes[index + 1] = codes[index];
        codes[index] = tmp2;
    } else if (btn.classList.contains('btnRemove')) {
        codes.splice(index, 1);
    }

    var listEl = currentView.querySelector(type === 'audio' ? '#audioLangList' : '#subtitleLangList');
    var selectEl = currentView.querySelector(type === 'audio' ? '#audioLangSelect' : '#subtitleLangSelect');
    renderLangList(listEl, codes, type);
    refreshLangSelect(selectEl, codes);
}

function addLanguage(type) {
    var selectEl = currentView.querySelector(type === 'audio' ? '#audioLangSelect' : '#subtitleLangSelect');
    var code = selectEl.value;
    if (!code) return;

    var codes = getListCodes(type);
    if (codes.indexOf(code) === -1) codes.push(code);

    var listEl = currentView.querySelector(type === 'audio' ? '#audioLangList' : '#subtitleLangList');
    renderLangList(listEl, codes, type);
    refreshLangSelect(selectEl, codes);
}

function searchSeries(query) {
    var resultsList = currentView.querySelector('#seriesSearchResults');
    if (!query || query.length < 2) {
        resultsList.style.display = 'none';
        return;
    }

    ApiClient.getItems(ApiClient.getCurrentUserId(), {
        SearchTerm: query,
        IncludeItemTypes: 'Series',
        Recursive: true,
        Limit: 10,
        Fields: 'PrimaryImageAspectRatio'
    }).then(function (result) {
        resultsList.innerHTML = '';
        if (!result.Items || result.Items.length === 0) {
            var li = document.createElement('li');
            li.className = 'lf-no-results';
            li.textContent = 'No series found';
            resultsList.appendChild(li);
        } else {
            result.Items.forEach(function (series) {
                var li = document.createElement('li');
                var label = series.Name + (series.ProductionYear ? ' (' + series.ProductionYear + ')' : '');
                li.textContent = label;
                li.dataset.seriesId = series.Id.replace(/-/g, '');
                li.dataset.seriesName = series.Name;
                li.addEventListener('click', function () {
                    addSeriesOverride(li.dataset.seriesId, li.dataset.seriesName);
                    resultsList.style.display = 'none';
                    currentView.querySelector('#seriesSearchInput').value = '';
                });
                resultsList.appendChild(li);
            });
        }
        resultsList.style.display = 'block';
    });
}

function addSeriesOverride(seriesId, seriesName) {
    var prefs = getCurrentPrefs();
    if (!prefs.SeriesOverrides) prefs.SeriesOverrides = [];

    var exists = prefs.SeriesOverrides.some(function (o) { return o.SeriesId === seriesId; });
    if (exists) {
        showStatus('Override for "' + seriesName + '" already exists.', true);
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

function removeSeriesOverride(ovIndex) {
    var prefs = getCurrentPrefs();
    if (prefs.SeriesOverrides && prefs.SeriesOverrides[ovIndex] !== undefined) {
        prefs.SeriesOverrides.splice(ovIndex, 1);
        saveUserPrefsToConfig(currentUserId, prefs);
        renderSeriesOverrides(prefs.SeriesOverrides);
    }
}

function showStatus(msg, isError) {
    var el = currentView.querySelector('#statusMessage');
    el.textContent = msg;
    el.className = 'lf-status ' + (isError ? 'lf-status-error' : 'lf-status-success');
    setTimeout(function () { el.className = 'lf-status'; }, 3000);
}

function doSave() {
    if (!currentUserId) return;
    saveUserPrefsToConfig(currentUserId, getCurrentPrefs());

    ApiClient.updatePluginConfiguration(pluginId, currentConfig).then(function () {
        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            currentConfig = config;
            loadUserPrefs();
            showStatus('Configuration saved.', false);
        });
    }).catch(function (err) {
        console.error('Language Failover save error:', err);
        showStatus('Error saving configuration.', true);
    });
}

export default function (view) {
    currentView = view;

    view.addEventListener('viewshow', function () {
        ApiClient.getUsers().then(function (users) {
            var selectUser = view.querySelector('#selectUser');
            var previousUserId = currentUserId;
            selectUser.innerHTML = '';
            users.forEach(function (user) {
                var opt = document.createElement('option');
                opt.value = user.Id;
                opt.textContent = user.Name;
                selectUser.appendChild(opt);
            });

            ApiClient.getPluginConfiguration(pluginId).then(function (config) {
                currentConfig = config;
                if (previousUserId && users.some(function (u) { return u.Id === previousUserId; })) {
                    selectUser.value = previousUserId;
                    currentUserId = previousUserId;
                } else if (users.length > 0) {
                    currentUserId = users[0].Id;
                }
                loadUserPrefs();
            });
        });
    });

    view.querySelector('#selectUser').addEventListener('change', function () {
        if (currentUserId) {
            saveUserPrefsToConfig(currentUserId, getCurrentPrefs());
        }
        currentUserId = this.value;
        loadUserPrefs();
    });

    view.querySelector('#audioLangList').addEventListener('click', function (e) { handleListAction(e); });
    view.querySelector('#subtitleLangList').addEventListener('click', function (e) { handleListAction(e); });
    view.querySelector('#btnAddAudio').addEventListener('click', function () { addLanguage('audio'); });
    view.querySelector('#btnAddSubtitle').addEventListener('click', function () { addLanguage('subtitle'); });

    view.querySelector('#seriesOverridesList').addEventListener('click', function (e) {
        var btn = e.target.closest('button');
        if (!btn) return;

        if (btn.classList.contains('btnRemoveOverride')) {
            removeSeriesOverride(parseInt(btn.dataset.ovIndex, 10));
            return;
        }

        if (btn.classList.contains('btnAddOvLang')) {
            var ovIndex = parseInt(btn.dataset.ovIndex, 10);
            var ovType = btn.dataset.ovType;
            var selId = ovType === 'audio' ? 'soAudioSel_' + ovIndex : 'soSubSel_' + ovIndex;
            var listId = ovType === 'audio' ? 'soAudio_' + ovIndex : 'soSub_' + ovIndex;
            var selectEl = view.querySelector('#' + selId);
            var listEl = view.querySelector('#' + listId);
            if (!selectEl || !listEl) return;

            var code = selectEl.value;
            if (!code) return;

            var codes = getCodesFromList(listEl);
            if (codes.indexOf(code) === -1) codes.push(code);

            renderLangList(listEl, codes, 'ov_' + ovIndex + '_' + ovType);
            refreshLangSelect(selectEl, codes);
            return;
        }

        handleOverrideListAction(e);
    });

    var searchTimeout = null;
    var searchInput = view.querySelector('#seriesSearchInput');
    searchInput.addEventListener('input', function () {
        var query = this.value.trim();
        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(function () { searchSeries(query); }, 300);
    });
    searchInput.addEventListener('focus', function () {
        if (this.value.trim().length >= 2) {
            view.querySelector('#seriesSearchResults').style.display = 'block';
        }
    });
    document.addEventListener('click', function (e) {
        if (!view.contains(e.target)) return;
        if (!e.target.closest('.lf-search-wrapper')) {
            var results = view.querySelector('#seriesSearchResults');
            if (results) results.style.display = 'none';
        }
    });

    view.querySelector('#btnSave').addEventListener('click', doSave);
}

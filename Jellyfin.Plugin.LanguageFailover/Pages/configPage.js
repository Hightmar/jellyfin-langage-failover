var pluginId = 'a5b6c7d8-1234-5678-9abc-def012345678';

var LANG_NAMES = {
    zh: 'Chinese', ko: 'Korean', ja: 'Japanese', en: 'English', fr: 'French',
    de: 'German', es: 'Spanish', pt: 'Portuguese', it: 'Italian', ru: 'Russian',
    ar: 'Arabic', hi: 'Hindi', th: 'Thai', vi: 'Vietnamese', pl: 'Polish',
    nl: 'Dutch', sv: 'Swedish', no: 'Norwegian', da: 'Danish', fi: 'Finnish'
};

var LANG_OPTIONS_HTML = Object.keys(LANG_NAMES).map(function (code) {
    return '<option value="' + code + '">' + LANG_NAMES[code] + ' (' + code + ')</option>';
}).join('');

var currentConfig = null;
var currentUserId = null;
var currentView = null;

function getLangName(code) {
    return LANG_NAMES[code] || code.toUpperCase();
}

function renderLangList(listEl, languages, type) {
    listEl.innerHTML = '';
    languages.forEach(function (code, index) {
        var li = document.createElement('li');
        li.innerHTML =
            '<span class="lang-priority">' + (index + 1) + '</span>' +
            '<span class="lang-name">' + getLangName(code) + ' (' + code + ')</span>' +
            '<button class="btnMoveUp" data-type="' + type + '" data-index="' + index + '"' +
                (index === 0 ? ' disabled' : '') + '>&uarr;</button>' +
            '<button class="btnMoveDown" data-type="' + type + '" data-index="' + index + '"' +
                (index === languages.length - 1 ? ' disabled' : '') + '>&darr;</button>' +
            '<button class="btnRemove" data-type="' + type + '" data-index="' + index + '">&times;</button>';
        listEl.appendChild(li);
    });
}

function getCodesFromList(listEl) {
    var codes = [];
    listEl.querySelectorAll('.lang-name').forEach(function (span) {
        var match = span.textContent.match(/\(([^)]+)\)$/);
        if (match) codes.push(match[1]);
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
    (overrides || []).forEach(function (ov, ovIndex) {
        var div = document.createElement('div');
        div.className = 'series-override';
        div.dataset.index = ovIndex;

        var audioListId = 'soAudio_' + ovIndex;
        var subListId = 'soSub_' + ovIndex;
        var audioSelId = 'soAudioSel_' + ovIndex;
        var subSelId = 'soSubSel_' + ovIndex;

        div.innerHTML =
            '<div class="series-override-header">' +
                '<strong>' + (ov.SeriesName || ov.SeriesId) + '</strong>' +
                '<button class="btnRemoveOverride" data-ov-index="' + ovIndex + '" style="color:#f44">&times; Remove</button>' +
            '</div>' +
            '<div style="font-size:0.9em">' +
                '<div>Audio:</div>' +
                '<ul id="' + audioListId + '" class="lang-list"></ul>' +
                '<div class="lang-add-row">' +
                    '<select id="' + audioSelId + '" multiple size="4" style="min-width:180px">' + LANG_OPTIONS_HTML + '</select>' +
                    '<button class="btnAddOvLang" data-ov-index="' + ovIndex + '" data-ov-type="audio">Add</button>' +
                '</div>' +
                '<div style="margin-top:0.5em">Subtitles:</div>' +
                '<ul id="' + subListId + '" class="lang-list"></ul>' +
                '<div class="lang-add-row">' +
                    '<select id="' + subSelId + '" multiple size="4" style="min-width:180px">' + LANG_OPTIONS_HTML + '</select>' +
                    '<button class="btnAddOvLang" data-ov-index="' + ovIndex + '" data-ov-type="subtitle">Add</button>' +
                '</div>' +
            '</div>';

        container.appendChild(div);

        // Render existing languages
        renderLangList(div.querySelector('#' + audioListId), ov.AudioLanguages || [], 'ov_' + ovIndex + '_audio');
        renderLangList(div.querySelector('#' + subListId), ov.SubtitleLanguages || [], 'ov_' + ovIndex + '_subtitle');
    });
}

function handleOverrideListAction(e) {
    var btn = e.target.closest('button');
    if (!btn) return;

    var type = btn.dataset.type;
    if (!type || !type.startsWith('ov_')) return;

    var index = parseInt(btn.dataset.index, 10);
    var parts = type.split('_');
    var ovIndex = parseInt(parts[1], 10);
    var langType = parts[2]; // 'audio' or 'subtitle'

    var listId = langType === 'audio' ? 'soAudio_' + ovIndex : 'soSub_' + ovIndex;
    var listEl = currentView.querySelector('#' + listId);
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
}

function loadUserPrefs() {
    var view = currentView;
    var prefs = getOrCreateUserPrefs(currentUserId);
    view.querySelector('#chkEnabled').checked = prefs.Enabled !== false;
    view.querySelector('#chkPreferNonForced').checked = prefs.PreferNonForcedSubtitles !== false;
    renderLangList(view.querySelector('#audioLangList'), prefs.AudioLanguages || [], 'audio');
    renderLangList(view.querySelector('#subtitleLangList'), prefs.SubtitleLanguages || [], 'subtitle');
    renderSeriesOverrides(prefs.SeriesOverrides || []);
}

function getListCodes(type) {
    var listEl = currentView.querySelector(type === 'audio' ? '#audioLangList' : '#subtitleLangList');
    return getCodesFromList(listEl);
}

function getSeriesOverridesFromUI() {
    var overrides = [];
    var container = currentView.querySelector('#seriesOverridesList');
    var blocks = container.querySelectorAll('.series-override');
    blocks.forEach(function (block, idx) {
        var nameEl = block.querySelector('strong');
        var audioList = block.querySelector('#soAudio_' + idx);
        var subList = block.querySelector('#soSub_' + idx);
        var removeBtn = block.querySelector('.btnRemoveOverride');
        var seriesId = removeBtn ? removeBtn.dataset.seriesId : '';

        // Get seriesId from the existing prefs
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
    renderLangList(listEl, codes, type);
}

function addLanguages(type) {
    var selectEl = currentView.querySelector(type === 'audio' ? '#audioLangSelect' : '#subtitleLangSelect');
    var selected = Array.from(selectEl.selectedOptions).map(function (o) { return o.value; }).filter(Boolean);
    if (selected.length === 0) return;

    var codes = getListCodes(type);
    selected.forEach(function (code) {
        if (codes.indexOf(code) === -1) {
            codes.push(code);
        }
    });

    var listEl = currentView.querySelector(type === 'audio' ? '#audioLangList' : '#subtitleLangList');
    renderLangList(listEl, codes, type);
    Array.from(selectEl.options).forEach(function (o) { o.selected = false; });
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
        if (result.Items.length === 0) {
            resultsList.innerHTML = '<li style="color:#999">No series found</li>';
        } else {
            result.Items.forEach(function (series) {
                var li = document.createElement('li');
                li.textContent = series.Name + (series.ProductionYear ? ' (' + series.ProductionYear + ')' : '');
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

    // Check if already exists
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

    // Save to in-memory config and re-render
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
    el.className = 'status-message ' + (isError ? 'error' : 'success');
    setTimeout(function () { el.className = 'status-message'; }, 3000);
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
    view.querySelector('#btnAddAudio').addEventListener('click', function () { addLanguages('audio'); });
    view.querySelector('#btnAddSubtitle').addEventListener('click', function () { addLanguages('subtitle'); });

    // Series overrides - delegate events on the container
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

            var selected = Array.from(selectEl.selectedOptions).map(function (o) { return o.value; }).filter(Boolean);
            if (selected.length === 0) return;

            var codes = getCodesFromList(listEl);
            selected.forEach(function (code) {
                if (codes.indexOf(code) === -1) codes.push(code);
            });
            renderLangList(listEl, codes, 'ov_' + ovIndex + '_' + ovType);
            Array.from(selectEl.options).forEach(function (o) { o.selected = false; });
            return;
        }

        // Handle move up/down/remove within override language lists
        handleOverrideListAction(e);
    });

    // Series search
    var searchTimeout = null;
    view.querySelector('#seriesSearchInput').addEventListener('input', function () {
        var query = this.value.trim();
        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(function () { searchSeries(query); }, 300);
    });
    view.querySelector('#btnSearchSeries').addEventListener('click', function () {
        searchSeries(view.querySelector('#seriesSearchInput').value.trim());
    });

    view.querySelector('#btnSave').addEventListener('click', doSave);
}

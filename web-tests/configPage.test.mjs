// Drives the real admin configuration page in jsdom: the actual configPage.html
// and configPage.js are loaded, clicked the way an admin would click them, and
// the object handed to ApiClient.updatePluginConfiguration is asserted.
//
// This is the only coverage the page has — it is browser code that never reaches
// the .NET test suite — so it deliberately exercises whole flows rather than
// individual functions.

import { test, before, beforeEach } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { JSDOM } from 'jsdom';

const PAGES_DIR = join(dirname(fileURLToPath(import.meta.url)),
    '..', 'Jellyfin.Plugin.LanguageFailover', 'Pages');

const USERS = [
    { Id: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee', Name: 'Alice' },
    { Id: 'ffffffff-1111-2222-3333-444444444444', Name: 'Bob' },
];

const CULTURES = [
    { TwoLetterISOLanguageName: 'fr', ThreeLetterISOLanguageName: 'fre', DisplayName: 'French' },
    { TwoLetterISOLanguageName: 'en', ThreeLetterISOLanguageName: 'eng', DisplayName: 'English' },
    { TwoLetterISOLanguageName: 'ja', ThreeLetterISOLanguageName: 'jpn', DisplayName: 'Japanese' },
];

const SERIES = { Id: '11111111-2222-3333-4444-555555555555', Name: 'Frieren', ProductionYear: 2023 };

let initPage;
let view;
let stored;
let saved;
let searchShouldFail;

before(async () => {
    // Node treats a bare .js file as CommonJS here, so import a copy under an
    // .mjs name. The bytes are the shipped file's.
    const copy = new URL('./configPage.copy.mjs', import.meta.url);
    writeFileSync(copy, readFileSync(join(PAGES_DIR, 'configPage.js')));
    ({ default: initPage } = await import(copy.href));
});

beforeEach(() => {
    const html = readFileSync(join(PAGES_DIR, 'configPage.html'), 'utf8');
    const dom = new JSDOM(`<!doctype html><body>${html}</body>`);

    global.window = dom.window;
    global.document = dom.window.document;

    stored = { UserPreferences: [] };
    saved = null;
    searchShouldFail = false;

    global.ApiClient = {
        getCultures: () => Promise.resolve(CULTURES),
        getUsers: () => Promise.resolve(USERS),
        getCurrentUserId: () => USERS[0].Id,
        getPluginConfiguration: () => Promise.resolve(structuredClone(stored)),
        updatePluginConfiguration: (_id, config) => {
            saved = structuredClone(config);
            stored = structuredClone(config);
            return Promise.resolve();
        },
        getItems: () => searchShouldFail
            ? Promise.reject(new Error('boom'))
            : Promise.resolve({ Items: [SERIES] }),
    };

    view = document.getElementById('LanguageFailoverConfigPage');
    initPage(view);
});

// --- helpers ---

const settle = async (times = 5) => {
    for (let i = 0; i < times; i++) await new Promise((r) => setTimeout(r, 0));
};

const $ = (sel) => view.querySelector(sel);
const click = (el) => el.dispatchEvent(new window.MouseEvent('click', { bubbles: true }));
const codesIn = (listEl) => Array.from(listEl.querySelectorAll('.lf-chip-code'), (e) => e.textContent);

const boot = async () => {
    view.dispatchEvent(new window.Event('viewshow'));
    await settle();
};

/** The <select> and "+ Add" button that belong to a given language list. */
const controlsFor = (listEl) => ({
    select: listEl.parentElement.querySelector('.lf-add-row select'),
    add: listEl.parentElement.querySelector('.btnAddLang'),
});

const addLanguages = (listEl, codes) => {
    const { select, add } = controlsFor(listEl);
    for (const code of codes) {
        select.value = code;
        click(add);
    }
};

const addSeriesOverride = async () => {
    const search = $('#seriesSearchInput');
    search.value = 'frieren';
    search.dispatchEvent(new window.Event('input'));
    await new Promise((r) => setTimeout(r, 350));
    await settle();
    click($('#seriesSearchResults li'));
    await settle();
};

// --- tests ---

test('populates users and the language catalogue from the API', async () => {
    await boot();

    assert.equal($('#selectUser').options.length, 2);
    assert.match($('#audioLangList').textContent, /No languages added yet/);
    // placeholder + the three cultures the API returned
    assert.equal(controlsFor($('#audioLangList')).select.options.length, 4);
});

test('adding a language moves it out of the select and into the list', async () => {
    await boot();
    const list = $('#audioLangList');

    addLanguages(list, ['en', 'fr', 'ja']);

    assert.deepEqual(codesIn(list), ['en', 'fr', 'ja']);
    assert.equal(controlsFor(list).select.options.length, 1, 'only the placeholder is left');
    assert.deepEqual(
        Array.from(list.querySelectorAll('.lf-chip-priority'), (e) => e.textContent),
        ['1', '2', '3']);
});

test('the same language cannot be added twice', async () => {
    await boot();
    const list = $('#audioLangList');

    addLanguages(list, ['fr']);
    const { select, add } = controlsFor(list);
    select.value = 'fr';   // no longer offered, but force it
    click(add);

    assert.deepEqual(codesIn(list), ['fr']);
});

test('move up, move down and remove reorder the list', async () => {
    await boot();
    const list = $('#audioLangList');
    addLanguages(list, ['en', 'fr', 'ja']);

    click(list.querySelectorAll('.lf-chip')[2].querySelector('.btnMoveDown'));
    assert.deepEqual(codesIn(list), ['en', 'fr', 'ja'], 'move-down on the last entry does nothing');

    click(list.querySelectorAll('.lf-chip')[2].querySelector('.btnMoveUp'));
    assert.deepEqual(codesIn(list), ['en', 'ja', 'fr']);

    click(list.querySelectorAll('.lf-chip')[0].querySelector('.btnMoveUp'));
    assert.deepEqual(codesIn(list), ['en', 'ja', 'fr'], 'move-up on the first entry does nothing');

    click(list.querySelectorAll('.lf-chip')[0].querySelector('.btnRemove'));
    assert.deepEqual(codesIn(list), ['ja', 'fr']);
    assert.equal(controlsFor(list).select.options.length, 2, 'the removed code is offered again');
});

test('the audio and subtitle lists are independent', async () => {
    await boot();

    addLanguages($('#audioLangList'), ['ja']);
    addLanguages($('#subtitleLangList'), ['fr']);

    assert.deepEqual(codesIn($('#audioLangList')), ['ja']);
    assert.deepEqual(codesIn($('#subtitleLangList')), ['fr']);
});

test('a series override gets its own independent language lists', async () => {
    await boot();
    addLanguages($('#audioLangList'), ['fr']);
    await addSeriesOverride();

    const block = $('#seriesOverridesList .lf-series-override');
    assert.ok(block);
    assert.equal(block.dataset.seriesId, '11111111222233334444555555555555', 'hyphen-free id');
    assert.equal(block.dataset.seriesName, 'Frieren');

    addLanguages(block.querySelector('[data-ov-role="audio"]'), ['ja']);

    assert.deepEqual(codesIn(block.querySelector('[data-ov-role="audio"]')), ['ja']);
    assert.deepEqual(codesIn($('#audioLangList')), ['fr'], 'global list untouched');
});

test('the same series cannot be overridden twice', async () => {
    await boot();
    await addSeriesOverride();
    await addSeriesOverride();

    assert.equal($('#seriesOverridesList').querySelectorAll('.lf-series-override').length, 1);
    assert.match($('#statusMessage').textContent, /already exists/);
});

test('save sends the full preference object and it survives a reload', async () => {
    await boot();
    addLanguages($('#audioLangList'), ['ja', 'fr']);
    addLanguages($('#subtitleLangList'), ['fr']);
    await addSeriesOverride();
    addLanguages($('#seriesOverridesList [data-ov-role="audio"]'), ['ja']);
    $('#chkPreferOriginal').checked = true;

    click($('#btnSave'));
    await settle();

    assert.ok(saved, 'the configuration was sent');
    const prefs = saved.UserPreferences[0];
    assert.equal(prefs.UserId, 'aaaaaaaabbbbccccddddeeeeeeeeeeee', 'hyphen-free user id');
    assert.deepEqual(prefs.AudioLanguages, ['ja', 'fr']);
    assert.deepEqual(prefs.SubtitleLanguages, ['fr']);
    assert.equal(prefs.PreferOriginalAudio, true);
    assert.equal(prefs.PreferNonForcedSubtitles, true);
    assert.equal(prefs.Enabled, true);
    assert.equal(prefs.SeriesOverrides.length, 1);
    assert.equal(prefs.SeriesOverrides[0].SeriesId, '11111111222233334444555555555555');
    assert.equal(prefs.SeriesOverrides[0].SeriesName, 'Frieren');
    assert.deepEqual(prefs.SeriesOverrides[0].AudioLanguages, ['ja']);
    assert.deepEqual(prefs.SeriesOverrides[0].SubtitleLanguages, []);
    assert.match($('#statusMessage').className, /lf-status-success/);

    await boot();
    assert.deepEqual(codesIn($('#audioLangList')), ['ja', 'fr']);
    assert.equal($('#chkPreferOriginal').checked, true);
    assert.equal($('#seriesOverridesList .lf-series-override').dataset.seriesName, 'Frieren');
});

test('switching users keeps each user\'s preferences separate', async () => {
    await boot();
    addLanguages($('#audioLangList'), ['ja']);

    const selectUser = $('#selectUser');
    selectUser.value = USERS[1].Id;
    selectUser.dispatchEvent(new window.Event('change'));

    assert.match($('#audioLangList').textContent, /No languages added yet/, 'Bob starts empty');
    addLanguages($('#audioLangList'), ['fr']);

    selectUser.value = USERS[0].Id;
    selectUser.dispatchEvent(new window.Event('change'));
    assert.deepEqual(codesIn($('#audioLangList')), ['ja'], "Alice's list came back");

    click($('#btnSave'));
    await settle();

    const byUser = Object.fromEntries(saved.UserPreferences.map((p) => [p.UserId, p.AudioLanguages]));
    assert.deepEqual(byUser['aaaaaaaabbbbccccddddeeeeeeeeeeee'], ['ja']);
    assert.deepEqual(byUser['ffffffff111122223333444444444444'], ['fr']);
});

test('removing an override removes the right one', async () => {
    await boot();
    await addSeriesOverride();

    click($('#seriesOverridesList .btnRemoveOverride'));

    assert.equal($('#seriesOverridesList .lf-series-override'), null);
    assert.match($('#seriesOverridesList').textContent, /No series overrides/);
});

test('a failing series search reports itself instead of going silent', async () => {
    await boot();
    searchShouldFail = true;

    const search = $('#seriesSearchInput');
    search.value = 'frieren';
    search.dispatchEvent(new window.Event('input'));
    await new Promise((r) => setTimeout(r, 350));
    await settle();

    assert.match($('#seriesSearchResults').textContent, /Search failed/);
});

test('series names are escaped rather than injected as markup', async () => {
    await boot();
    global.ApiClient.getItems = () => Promise.resolve({
        Items: [{ Id: SERIES.Id, Name: '<img src=x onerror=alert(1)>' }],
    });

    const search = $('#seriesSearchInput');
    search.value = 'evil';
    search.dispatchEvent(new window.Event('input'));
    await new Promise((r) => setTimeout(r, 350));
    await settle();
    click($('#seriesSearchResults li'));
    await settle();

    const title = $('.lf-series-override-title');
    assert.equal(title.querySelector('img'), null, 'no element was injected');
    assert.equal(title.textContent, '<img src=x onerror=alert(1)>');
});

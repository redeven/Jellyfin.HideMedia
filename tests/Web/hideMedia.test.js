import assert from 'node:assert/strict';
import { afterEach, describe, test } from 'node:test';
import { createClient, fail } from './harness.js';

const MOVIE = 'a1b2c3d4e5f60718293a4b5c6d7e8f90';
const EPISODE = '0f1e2d3c4b5a69788796a5b4c3d2e1f0';
const SERIES = '11112222333344445555666677778888';

let client;

function setup(options) {
    client = createClient(options);
    return client;
}

afterEach(() => client?.close());

describe('context menu entry', () => {
    test('adds "Hide from Home" right after "Add to playlist" for an eligible card', async () => {
        const c = setup({ routes: { ['GET HideMedia/Status/' + MOVIE]: { Eligible: true, Hidden: false, TargetId: MOVIE } } });
        c.click(c.addCard(MOVIE).querySelector('[data-action="menu"]'));

        const sheet = await c.openActionSheet();

        const item = c.toggleItem(sheet);
        assert.ok(item, 'menu item was not added');
        assert.equal(item.previousElementSibling.getAttribute('data-id'), 'addtoplaylist');
        assert.equal(item.querySelector('.actionSheetItemText').textContent, 'Hide from Home');
        assert.ok(item.querySelector('.actionsheetMenuItemIcon').classList.contains('visibility_off'));
        assert.equal(item.querySelector('.listItemBodyText.secondary'), null);
        assert.ok(!item.classList.contains('selected'));
        assert.deepEqual(c.requests.map(r => r.method + ' ' + r.path), ['GET HideMedia/Status/' + MOVIE]);
    });

    test('offers "Unhide from Home" for an item that is already hidden', async () => {
        const c = setup({ routes: { ['GET HideMedia/Status/' + EPISODE]: { Eligible: true, Hidden: true, TargetId: SERIES } } });
        c.click(c.addCard(EPISODE).querySelector('[data-action="menu"]'));

        const item = c.toggleItem(await c.openActionSheet());

        assert.equal(item.querySelector('.actionSheetItemText').textContent, 'Unhide from Home');
        assert.ok(item.querySelector('.actionsheetMenuItemIcon').classList.contains('visibility'));
    });

    test('adds nothing for items the server says are not eligible', async () => {
        const c = setup({ routes: { ['GET HideMedia/Status/' + MOVIE]: { Eligible: false, Hidden: false, TargetId: null } } });
        c.click(c.addCard(MOVIE).querySelector('[data-action="menu"]'));

        const sheet = await c.openActionSheet();

        assert.equal(c.toggleItem(sheet), null);
    });

    test('works from a right-click anywhere on a card', async () => {
        const c = setup({ routes: { ['GET HideMedia/Status/' + MOVIE]: { Eligible: true, Hidden: false, TargetId: MOVIE } } });
        c.click(c.addCard(MOVIE).querySelector('.cardImage'), 'contextmenu');

        assert.ok(c.toggleItem(await c.openActionSheet()));
    });

    test('works from the more button on an item details page', async () => {
        const c = setup({
            hash: '#/details?id=' + SERIES + '&serverId=abc',
            routes: { ['GET HideMedia/Status/' + SERIES]: { Eligible: true, Hidden: false, TargetId: SERIES } }
        });
        c.click(c.addDetailsMoreButton().firstChild);

        assert.ok(c.toggleItem(await c.openActionSheet()));
    });

    test('ignores a plain click on a card that opens it rather than its menu', async () => {
        const c = setup();
        c.click(c.addCard(MOVIE).querySelector('.cardImage'));

        await c.openActionSheet();

        assert.deepEqual(c.requests, []);
    });

    test('ignores menus that are not item context menus', async () => {
        const c = setup();
        c.click(c.addCard(MOVIE).querySelector('[data-action="menu"]'));

        await c.openActionSheet(['sortname', 'sortdate']);

        assert.deepEqual(c.requests, []);
    });

    test('ignores menus opened long after the last card interaction', async () => {
        const c = setup();
        let now = 1_000_000;
        c.window.Date.now = () => now;
        c.click(c.addCard(MOVIE).querySelector('[data-action="menu"]'));

        now += 3001;
        await c.openActionSheet();

        assert.deepEqual(c.requests, []);
    });

    test('uses a card interaction only once', async () => {
        const c = setup({ routes: { ['GET HideMedia/Status/' + MOVIE]: { Eligible: true, Hidden: false, TargetId: MOVIE } } });
        c.click(c.addCard(MOVIE).querySelector('[data-action="menu"]'));

        await c.openActionSheet();
        const second = await c.openActionSheet();

        assert.equal(c.toggleItem(second), null);
        assert.equal(c.requests.length, 1);
    });

    test('clicks inside an open menu do not replace the remembered card', async () => {
        const c = setup({ routes: { ['GET HideMedia/Status/' + MOVIE]: { Eligible: true, Hidden: false, TargetId: MOVIE } } });
        const openSheet = await c.openActionSheet();
        c.click(c.addCard(MOVIE).querySelector('[data-action="menu"]'));

        // Menu entries carry their own data-id, which must not be mistaken for an item id.
        c.click(openSheet.querySelector('[data-id="refresh"]'), 'contextmenu');
        await c.openActionSheet();

        assert.deepEqual(c.requests.map(r => r.path), ['HideMedia/Status/' + MOVIE]);
    });

    test('appends to the end of the menu when there is no "Add to playlist" entry', async () => {
        const c = setup({ routes: { ['GET HideMedia/Status/' + MOVIE]: { Eligible: true, Hidden: false, TargetId: MOVIE } } });
        c.click(c.addCard(MOVIE).querySelector('[data-action="menu"]'));

        const sheet = await c.openActionSheet(['select', 'refresh']);

        const scroller = sheet.querySelector('.actionSheetScroller');
        assert.equal(scroller.lastElementChild, c.toggleItem(sheet));
        assert.equal(c.toggleItem(sheet).querySelector('.actionSheetItemText').textContent, 'Hide from Home');
    });

    test('does not add the entry when the menu closed before the status arrived', async () => {
        let respond;
        const c = setup({ routes: { ['GET HideMedia/Status/' + MOVIE]: () => new Promise(r => { respond = r; }) } });
        c.click(c.addCard(MOVIE).querySelector('[data-action="menu"]'));
        const sheet = await c.openActionSheet();

        sheet.remove();
        respond({ Eligible: true, Hidden: false, TargetId: MOVIE });
        await c.flush();

        assert.equal(c.toggleItem(sheet), null);
    });

    test('silently skips the entry when the status request fails', async () => {
        const c = setup({ routes: { ['GET HideMedia/Status/' + MOVIE]: fail } });
        c.click(c.addCard(MOVIE).querySelector('[data-action="menu"]'));

        const sheet = await c.openActionSheet();

        assert.equal(c.toggleItem(sheet), null);
        assert.deepEqual(c.toasts(), []);
    });

    test('loading the script twice does not duplicate behaviour', async () => {
        const c = setup({ routes: { ['GET HideMedia/Status/' + MOVIE]: { Eligible: true, Hidden: false, TargetId: MOVIE } } });
        c.load();
        c.click(c.addCard(MOVIE).querySelector('[data-action="menu"]'));

        const sheet = await c.openActionSheet();

        assert.equal(c.requests.length, 1);
        assert.equal(sheet.querySelectorAll('[data-id="hidemedia-toggle"]').length, 1);
    });
});

describe('hiding and unhiding', () => {
    async function openMenuFor(c, card) {
        c.click(card.querySelector('[data-action="menu"]'));
        return c.toggleItem(await c.openActionSheet());
    }

    test('hiding an episode on Home removes every card of its series right away', async () => {
        const c = setup({
            routes: {
                ['GET HideMedia/Status/' + EPISODE]: { Eligible: true, Hidden: false, TargetId: SERIES },
                ['POST HideMedia/Hidden/' + EPISODE]: { Eligible: true, Hidden: true, TargetId: SERIES }
            }
        });
        const episodeCard = c.addCard(EPISODE);
        const seriesCard = c.addCard('11112222-3333-4444-5555-666677778888'.toUpperCase());
        const otherCard = c.addCard(MOVIE);

        c.click(await openMenuFor(c, episodeCard));
        await c.flush();

        assert.equal(c.requests.at(-1).method, 'POST');
        assert.equal(c.requests.at(-1).path, 'HideMedia/Hidden/' + EPISODE);
        assert.ok(!episodeCard.isConnected);
        assert.ok(!seriesCard.isConnected);
        assert.ok(otherCard.isConnected);
        assert.deepEqual(c.toasts(), ['Hidden from Home']);
    });

    test('hiding outside Home leaves the page alone', async () => {
        const c = setup({
            hash: '#/tv?topParentId=123',
            routes: {
                ['GET HideMedia/Status/' + EPISODE]: { Eligible: true, Hidden: false, TargetId: SERIES },
                ['POST HideMedia/Hidden/' + EPISODE]: { Eligible: true, Hidden: true, TargetId: SERIES }
            }
        });
        const episodeCard = c.addCard(EPISODE);
        const seriesCard = c.addCard(SERIES);

        c.click(await openMenuFor(c, episodeCard));
        await c.flush();

        assert.ok(episodeCard.isConnected);
        assert.ok(seriesCard.isConnected);
        assert.deepEqual(c.toasts(), ['Hidden from Home']);
    });

    test('unhiding sends a DELETE and keeps the cards', async () => {
        const c = setup({
            routes: {
                ['GET HideMedia/Status/' + MOVIE]: { Eligible: true, Hidden: true, TargetId: MOVIE },
                ['DELETE HideMedia/Hidden/' + MOVIE]: { Eligible: true, Hidden: false, TargetId: MOVIE }
            }
        });
        const card = c.addCard(MOVIE);

        c.click(await openMenuFor(c, card));
        await c.flush();

        assert.equal(c.requests.at(-1).method, 'DELETE');
        assert.ok(card.isConnected);
        assert.deepEqual(c.toasts(), ['No longer hidden from Home']);
    });

    test('a failed update tells the user and keeps the card', async () => {
        const c = setup({
            routes: {
                ['GET HideMedia/Status/' + MOVIE]: { Eligible: true, Hidden: false, TargetId: MOVIE },
                ['POST HideMedia/Hidden/' + MOVIE]: fail
            }
        });
        const card = c.addCard(MOVIE);

        c.click(await openMenuFor(c, card));
        await c.flush();

        assert.ok(card.isConnected);
        assert.deepEqual(c.toasts(), ['Could not update Home visibility']);
    });
});

describe('settings', () => {
    function addHomePreferencesLink(c) {
        const link = c.document.createElement('a');
        link.className = 'lnkHomePreferences listItem-border';
        link.setAttribute('href', '#/mypreferenceshome');
        link.innerHTML = '<div class="listItem"><span class="material-icons listItemIcon listItemIcon-transparent home"></span>'
            + '<div class="listItemBody"><div class="listItemBodyText">Home</div></div></div>';
        const list = c.document.createElement('div');
        list.appendChild(link);
        c.document.body.appendChild(list);
        return link;
    }

    function settingsLink(c) {
        return c.document.querySelector('.lnkHideMediaPreferences');
    }

    async function openDialog(c) {
        addHomePreferencesLink(c);
        await c.flush();
        c.click(settingsLink(c));
        await c.flush();
        return c.dialog();
    }

    function checkbox(dialog, section) {
        return dialog.querySelector('input[data-section="' + section + '"]');
    }

    test('adds a "Hide from Home" entry after the Home preferences link, once', async () => {
        const c = setup();
        const home = addHomePreferencesLink(c);
        await c.flush();
        c.document.body.appendChild(c.document.createElement('div'));
        await c.flush();

        const links = c.document.querySelectorAll('.lnkHideMediaPreferences');
        assert.equal(links.length, 1);
        assert.equal(home.nextElementSibling, links[0]);
        assert.equal(links[0].querySelector('.listItemBodyText').textContent, 'Hide from Home');
        assert.ok(links[0].querySelector('.listItemIcon').classList.contains('visibility_off'));
        assert.ok(!links[0].classList.contains('lnkHomePreferences'));
        assert.equal(home.querySelector('.listItemBodyText').textContent, 'Home');
    });

    test('opening it shows the saved settings without navigating away', async () => {
        const c = setup({ routes: { 'GET HideMedia/Settings': { RecentlyAdded: true, ContinueWatching: false, NextUp: true } } });
        addHomePreferencesLink(c);
        await c.flush();
        const click = new c.window.MouseEvent('click', { bubbles: true, cancelable: true });

        settingsLink(c).dispatchEvent(click);
        await c.flush();

        assert.ok(click.defaultPrevented);
        const dialog = c.dialog();
        assert.ok(dialog);
        assert.equal(checkbox(dialog, 'RecentlyAdded').checked, true);
        assert.equal(checkbox(dialog, 'ContinueWatching').checked, false);
        assert.equal(checkbox(dialog, 'NextUp').checked, true);
    });

    test('changing a checkbox saves all three sections', async () => {
        const c = setup({
            routes: {
                'GET HideMedia/Settings': { RecentlyAdded: true, ContinueWatching: true, NextUp: true },
                'POST HideMedia/Settings': call => call.body
            }
        });
        const dialog = await openDialog(c);

        const box = checkbox(dialog, 'NextUp');
        box.checked = false;
        box.dispatchEvent(new c.window.Event('change'));
        await c.flush();

        const save = c.requests.at(-1);
        assert.equal(save.method, 'POST');
        assert.equal(save.path, 'HideMedia/Settings');
        assert.equal(save.request.contentType, 'application/json');
        assert.deepEqual(save.body, { RecentlyAdded: true, ContinueWatching: true, NextUp: false });
        assert.equal(box.checked, false);
    });

    test('a failed save reverts the checkbox and tells the user', async () => {
        const c = setup({
            routes: {
                'GET HideMedia/Settings': { RecentlyAdded: true, ContinueWatching: true, NextUp: true },
                'POST HideMedia/Settings': fail
            }
        });
        const dialog = await openDialog(c);

        const box = checkbox(dialog, 'RecentlyAdded');
        box.checked = false;
        box.dispatchEvent(new c.window.Event('change'));
        await c.flush();

        assert.equal(box.checked, true);
        assert.deepEqual(c.toasts(), ['Could not save Hide from Home settings']);
    });

    test('a failed load tells the user and opens nothing', async () => {
        const c = setup({ routes: { 'GET HideMedia/Settings': fail } });

        assert.equal(await openDialog(c), null);
        assert.deepEqual(c.toasts(), ['Could not load Hide from Home settings']);
    });

    test('closes with Escape, the close button or a click on the backdrop, but not a click inside', async () => {
        const c = setup({ routes: { 'GET HideMedia/Settings': { RecentlyAdded: true, ContinueWatching: true, NextUp: true } } });

        let dialog = await openDialog(c);
        c.click(dialog.querySelector('.formDialogContent'));
        assert.ok(dialog.isConnected);
        c.document.dispatchEvent(new c.window.KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
        assert.ok(!dialog.isConnected);

        c.click(settingsLink(c));
        await c.flush();
        dialog = c.dialog();
        c.click(dialog.querySelector('.btnCancel .material-icons'));
        assert.ok(!dialog.isConnected);

        c.click(settingsLink(c));
        await c.flush();
        dialog = c.dialog();
        c.click(dialog);
        assert.ok(!dialog.isConnected);
    });

    test('Escape stops being intercepted once the dialog is closed', async () => {
        const c = setup({ routes: { 'GET HideMedia/Settings': { RecentlyAdded: true, ContinueWatching: true, NextUp: true } } });
        const dialog = await openDialog(c);
        c.click(dialog);
        let reachedPage = false;
        c.document.body.addEventListener('keydown', () => { reachedPage = true; });

        c.document.body.dispatchEvent(new c.window.KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));

        assert.ok(reachedPage);
    });
});

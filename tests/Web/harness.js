import { readFileSync } from 'node:fs';
import { JSDOM } from 'jsdom';

const source = readFileSync(new URL('../../Web/hideMedia.js', import.meta.url), 'utf8');
const SERVER = 'http://jellyfin.test/';

/** A route handler that makes the API call fail. */
export function fail() {
    throw new Error('Request failed');
}

/**
 * Boots the plugin script in a fresh jsdom window with a fake Jellyfin ApiClient.
 * `routes` maps "METHOD path" to a response value or a function receiving the request.
 */
export function createClient({ hash = '#/home', routes = {}, storage = {} } = {}) {
    const dom = new JSDOM('<!doctype html><html><body></body></html>', {
        url: SERVER + 'web/' + hash,
        runScripts: 'outside-only',
        pretendToBeVisual: true
    });
    const { window } = dom;
    const { document } = window;
    const requests = [];
    const opened = [];
    const refreshes = [];

    for (const [key, value] of Object.entries(storage)) {
        window.localStorage.setItem(key, value);
    }

    window.XMLHttpRequest.prototype.open = function (method, url) {
        opened.push(url);
    };

    document._callbacks = { REFRESH_NEEDED: [event => refreshes.push(event.type)] };

    window.ApiClient = {
        getUrl: path => SERVER + path,
        ajax(request) {
            const call = {
                method: request.type,
                path: request.url.slice(SERVER.length),
                body: request.data === undefined ? undefined : JSON.parse(request.data),
                request
            };
            requests.push(call);
            const handler = routes[call.method + ' ' + call.path];
            try {
                if (handler === undefined) {
                    throw new Error('Unexpected request ' + call.method + ' ' + call.path);
                }
                return Promise.resolve(typeof handler === 'function' ? handler(call) : handler);
            } catch (error) {
                return Promise.reject(error);
            }
        }
    };

    const client = {
        window,
        document,
        requests,
        routes,
        opened,
        refreshes,

        load() {
            window.eval(source);
        },

        async flush() {
            for (let i = 0; i < 3; i++) {
                await new Promise(resolve => setTimeout(resolve, 0));
            }
        },

        addCard(id, parent = document.body) {
            const card = document.createElement('div');
            card.className = 'card';
            card.setAttribute('data-id', id);
            card.innerHTML = '<div class="cardBox"><img class="cardImage">'
                + '<button class="btnCardOptions" data-action="menu"><span class="material-icons more_vert"></span></button></div>';
            parent.appendChild(card);
            return card;
        },

        addDetailsMoreButton() {
            const button = document.createElement('button');
            button.className = 'btnMoreCommands';
            button.innerHTML = '<span class="material-icons more_vert"></span>';
            document.body.appendChild(button);
            return button;
        },

        /** Mimics the markup of Jellyfin's actionSheet for an item context menu. */
        async openActionSheet(ids = ['addtoplaylist', 'refresh']) {
            const sheet = document.createElement('div');
            sheet.className = 'actionSheet';
            sheet.innerHTML = '<div class="actionSheetContent"><div class="actionSheetScroller">'
                + ids.map(id => '<button is="emby-button" class="listItem actionSheetMenuItem' + (id === 'addtoplaylist' ? ' selected' : '') + '" data-id="' + id + '">'
                    + '<span class="actionsheetMenuItemIcon listItemIcon listItemIcon-transparent material-icons check"></span>'
                    + '<div class="listItemBody actionsheetListItemBody">'
                    + '<div class="listItemBodyText actionSheetItemText">' + id + '</div>'
                    + '<div class="listItemBodyText secondary">Secondary text</div>'
                    + '</div></button>').join('')
                + '</div></div>';
            document.body.appendChild(sheet);
            await client.flush();
            return sheet;
        },

        /** Mimics the modern layout's filter popover with the "Filters" section expanded. */
        async openFilterPopover(checked = []) {
            const popover = document.createElement('div');
            popover.id = 'filter-popover';
            popover.innerHTML = '<div class="MuiPaper-root"><div class="MuiAccordionDetails-root"><div class="MuiFormGroup-root">'
                + ['IsFavorite', 'IsUnplayed', 'IsPlayed', 'IsResumable'].map(value => {
                    const on = checked.includes(value);
                    return '<label class="MuiFormControlLabel-root">'
                        + '<span class="MuiButtonBase-root MuiCheckbox-root' + (on ? ' Mui-checked' : '') + '">'
                        + '<input class="PrivateSwitchBase-input" type="checkbox" value="' + value + '"' + (on ? ' checked' : '') + '>'
                        + '<svg data-testid="' + (on ? 'CheckBoxIcon' : 'CheckBoxOutlineBlankIcon') + '"><path d="' + value + '"></path></svg>'
                        + '</span><span class="MuiTypography-root MuiFormControlLabel-label">' + value + '</span></label>';
                }).join('')
                + '</div></div></div>';
            document.body.appendChild(popover);
            await client.flush();
            return popover;
        },

        hiddenFilter() {
            return document.querySelector('.hidemedia-filter');
        },

        xhr(url) {
            new window.XMLHttpRequest().open('GET', url);
        },

        click(element, type = 'click') {
            element.dispatchEvent(new window.MouseEvent(type, { bubbles: true, cancelable: true }));
        },

        toggleItem(sheet) {
            return sheet.querySelector('[data-id="hidemedia-toggle"]');
        },

        toasts() {
            return Array.from(document.body.children)
                .filter(el => el.style.position === 'fixed' && el.style.zIndex === '999999')
                .map(el => el.textContent);
        },

        dialog() {
            return document.querySelector('.dialogContainer');
        },

        close() {
            window.close();
        }
    };

    client.load();
    return client;
}

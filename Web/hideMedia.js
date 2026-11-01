(function () {
    'use strict';

    if (window.__hideMediaLoaded) {
        return;
    }
    window.__hideMediaLoaded = true;

    const MENU_ITEM_ID = 'hidemedia-toggle';
    const TRIGGER_WINDOW_MS = 3000;
    const SETTINGS_LINK_CLASS = 'lnkHideMediaPreferences';
    const FILTER_CLASS = 'hidemedia-filter';
    const FILTER_STORAGE_KEY = 'hidemedia-hidden-filter';
    const FILTER_PARAM = 'hideMediaHidden';
    const SECTIONS = [
        { key: 'RecentlyAdded', label: 'Recently Added' },
        { key: 'ContinueWatching', label: 'Continue Watching' },
        { key: 'NextUp', label: 'Next Up' }
    ];

    let lastTrigger = null;

    function normalizeId(id) {
        return (id || '').replace(/-/g, '').toLowerCase();
    }

    function hashParam(name) {
        const hash = window.location.hash || '';
        const query = hash.indexOf('?');
        return query === -1 ? null : new URLSearchParams(hash.substring(query + 1)).get(name);
    }

    function recordTrigger(event) {
        const target = event.target;
        if (!(target instanceof Element) || target.closest('.actionSheet')) {
            return;
        }

        if (target.closest('.btnMoreCommands')) {
            const id = hashParam('id');
            if (id) {
                lastTrigger = { id: id, cardEl: null, time: Date.now() };
            }
            return;
        }

        if (event.type === 'click' && !target.closest('[data-action="menu"]')) {
            return;
        }

        const card = target.closest('[data-id]');
        if (card) {
            lastTrigger = { id: card.getAttribute('data-id'), cardEl: card, time: Date.now() };
        }
    }

    document.addEventListener('click', recordTrigger, true);
    document.addEventListener('contextmenu', recordTrigger, true);

    let filterLibraries = readFilterLibraries();

    function readFilterLibraries() {
        try {
            const value = JSON.parse(window.localStorage.getItem(FILTER_STORAGE_KEY));
            return Array.isArray(value) ? value : [];
        } catch (e) {
            return [];
        }
    }

    function isFilterActive(libraryId) {
        return filterLibraries.indexOf(normalizeId(libraryId)) !== -1;
    }

    function setFilterActive(libraryId, active) {
        const id = normalizeId(libraryId);
        filterLibraries = filterLibraries.filter(function (l) { return l !== id; });
        if (active) {
            filterLibraries.push(id);
        }
        try {
            window.localStorage.setItem(FILTER_STORAGE_KEY, JSON.stringify(filterLibraries));
        } catch (e) {
        }
    }

    /** Adds the hidden-only flag to the current library's item list requests while its filter is on. */
    function tagUrl(url) {
        const libraryId = hashParam('topParentId');
        if (!libraryId || !isFilterActive(libraryId)) {
            return url;
        }

        let parsed;
        try {
            parsed = new URL(url, window.location.href);
        } catch (e) {
            return url;
        }
        if (!/\/Items$/i.test(parsed.pathname) || parsed.searchParams.has(FILTER_PARAM)) {
            return url;
        }

        let parentId = null;
        parsed.searchParams.forEach(function (value, key) {
            if (key.toLowerCase() === 'parentid') {
                parentId = value;
            }
        });
        if (normalizeId(parentId) !== normalizeId(libraryId)) {
            return url;
        }

        return url + (url.indexOf('?') === -1 ? '?' : '&') + FILTER_PARAM + '=true';
    }

    const originalOpen = window.XMLHttpRequest.prototype.open;
    window.XMLHttpRequest.prototype.open = function (method, url) {
        const args = Array.prototype.slice.call(arguments);
        args[1] = tagUrl(String(url));
        return originalOpen.apply(this, args);
    };

    /** Fires the web client's REFRESH_NEEDED event, which refetches the library's item queries. */
    function refreshLibrary() {
        const callbacks = document._callbacks && document._callbacks.REFRESH_NEEDED;
        if (!callbacks || !callbacks.length) {
            window.location.reload();
            return;
        }
        callbacks.slice().forEach(function (callback) {
            callback.call(document, { type: 'REFRESH_NEEDED' });
        });
    }

    function api(method, path, body) {
        const client = window.ApiClient;
        const request = {
            type: method,
            url: client.getUrl(path),
            dataType: 'json'
        };
        if (body !== undefined) {
            request.data = JSON.stringify(body);
            request.contentType = 'application/json';
        }
        return client.ajax(request);
    }

    function showToast(text) {
        const toast = document.createElement('div');
        toast.textContent = text;
        Object.assign(toast.style, {
            position: 'fixed',
            left: '50%',
            bottom: '2em',
            transform: 'translateX(-50%)',
            background: '#303030',
            color: '#fff',
            padding: '0.8em 1.4em',
            borderRadius: '0.3em',
            zIndex: 999999,
            boxShadow: '0 2px 6px rgba(0,0,0,.5)'
        });
        document.body.appendChild(toast);
        setTimeout(function () { toast.remove(); }, 2500);
    }

    function removeCardsFromHome(trigger, targetId) {
        if (!(window.location.hash || '').startsWith('#/home')) {
            return;
        }

        const target = normalizeId(targetId);
        document.querySelectorAll('.card[data-id]').forEach(function (card) {
            if (normalizeId(card.getAttribute('data-id')) === target) {
                card.remove();
            }
        });
        if (trigger.cardEl && trigger.cardEl.isConnected) {
            trigger.cardEl.remove();
        }
    }

    function toggle(trigger, status) {
        const hide = !status.Hidden;
        api(hide ? 'POST' : 'DELETE', 'HideMedia/Hidden/' + trigger.id)
            .then(function (result) {
                if (hide) {
                    removeCardsFromHome(trigger, result.TargetId);
                }
                showToast(hide ? 'Hidden from Home' : 'No longer hidden from Home');
            })
            .catch(function () {
                showToast('Could not update Home visibility');
            });
    }

    function buildMenuItem(template, status) {
        const item = template.cloneNode(true);
        item.setAttribute('data-id', MENU_ITEM_ID);
        item.classList.remove('selected');

        const text = item.querySelector('.actionSheetItemText') || item.querySelector('.listItemBodyText');
        if (text) {
            text.textContent = status.Hidden ? 'Unhide from Home' : 'Hide from Home';
        }

        const icon = item.querySelector('.actionsheetMenuItemIcon');
        if (icon) {
            icon.className = 'actionsheetMenuItemIcon listItemIcon listItemIcon-transparent material-icons '
                + (status.Hidden ? 'visibility' : 'visibility_off');
            icon.textContent = '';
        }

        item.querySelectorAll('.listItemBodyText.secondary').forEach(function (el) { el.remove(); });

        return item;
    }

    function enhanceActionSheet(sheet) {
        const trigger = lastTrigger;
        if (!trigger || Date.now() - trigger.time > TRIGGER_WINDOW_MS) {
            return;
        }
        lastTrigger = null;

        if (!sheet.querySelector('.actionSheetMenuItem[data-id="refresh"], .actionSheetMenuItem[data-id="addtoplaylist"], .actionSheetMenuItem[data-id="select"]')) {
            return;
        }

        api('GET', 'HideMedia/Status/' + trigger.id).then(function (status) {
            if (!status.Eligible || !sheet.isConnected || sheet.querySelector('[data-id="' + MENU_ITEM_ID + '"]')) {
                return;
            }

            const anchor = sheet.querySelector('.actionSheetMenuItem[data-id="addtoplaylist"]');
            const template = anchor || sheet.querySelector('.actionSheetMenuItem');
            if (!template) {
                return;
            }

            const item = buildMenuItem(template, status);
            item.addEventListener('click', function () { toggle(trigger, status); });

            if (anchor) {
                anchor.after(item);
            } else {
                (sheet.querySelector('.actionSheetScroller') || template.parentElement).appendChild(item);
            }
        }).catch(function () {
        });
    }

    function openSettingsDialog() {
        api('GET', 'HideMedia/Settings').then(function (settings) {
            const container = document.createElement('div');
            container.className = 'dialogContainer';
            Object.assign(container.style, {
                position: 'fixed',
                inset: '0',
                zIndex: 1000,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                background: 'rgba(0, 0, 0, 0.5)'
            });

            container.innerHTML =
                '<style>:where(.hideMediaSettingsDialog) { background-color: #202020; }</style>'
                + '<div class="focuscontainer dialog formDialog hideMediaSettingsDialog" role="dialog" aria-modal="true"'
                + ' style="position: relative; width: 90%; max-width: 28em; border-radius: 0.3em; margin: 0;">'
                + '  <div class="formDialogHeader">'
                + '    <button is="paper-icon-button-light" class="btnCancel autoSize" title="Close">'
                + '      <span class="material-icons close" aria-hidden="true"></span>'
                + '    </button>'
                + '    <h3 class="formDialogHeaderTitle">Hide from Home</h3>'
                + '  </div>'
                + '  <div class="formDialogContent" style="padding: 0.5em 1.5em 1.5em;">'
                + '    <p class="fieldDescription">Remove hidden shows and movies from these Home sections:</p>'
                + SECTIONS.map(function (section) {
                    return '<div class="checkboxContainer">'
                        + '<label><input type="checkbox" is="emby-checkbox" data-section="' + section.key + '"'
                        + (settings[section.key] ? ' checked' : '') + ' />'
                        + '<span>' + section.label + '</span></label></div>';
                }).join('')
                + '  </div>'
                + '</div>';

            function close() {
                document.removeEventListener('keydown', onKeyDown, true);
                container.remove();
            }

            function onKeyDown(event) {
                if (event.key === 'Escape') {
                    event.stopPropagation();
                    close();
                }
            }

            container.addEventListener('click', function (event) {
                if (event.target === container || event.target.closest('.btnCancel')) {
                    close();
                }
            });
            document.addEventListener('keydown', onKeyDown, true);

            container.querySelectorAll('input[data-section]').forEach(function (checkbox) {
                checkbox.addEventListener('change', function () {
                    const updated = {};
                    container.querySelectorAll('input[data-section]').forEach(function (input) {
                        updated[input.getAttribute('data-section')] = input.checked;
                    });
                    api('POST', 'HideMedia/Settings', updated).catch(function () {
                        checkbox.checked = !checkbox.checked;
                        showToast('Could not save Hide from Home settings');
                    });
                });
            });

            document.body.appendChild(container);
        }).catch(function () {
            showToast('Could not load Hide from Home settings');
        });
    }

    function enhanceSettingsMenu() {
        const homeLink = document.querySelector('.lnkHomePreferences');
        if (!homeLink || homeLink.parentElement.querySelector('.' + SETTINGS_LINK_CLASS)) {
            return;
        }

        const link = homeLink.cloneNode(true);
        link.classList.remove('lnkHomePreferences');
        link.classList.add(SETTINGS_LINK_CLASS);
        link.setAttribute('href', '#');

        const icon = link.querySelector('.listItemIcon');
        if (icon) {
            icon.classList.remove('home');
            icon.classList.add('visibility_off');
        }

        const text = link.querySelector('.listItemBodyText');
        if (text) {
            text.textContent = 'Hide from Home';
        }

        link.addEventListener('click', function (event) {
            event.preventDefault();
            event.stopPropagation();
            openSettingsDialog();
        });

        homeLink.after(link);
    }

    function renderFilterCheckbox(label, checked) {
        const input = label.querySelector('input');
        input.checked = checked;
        input.parentElement.classList.toggle('Mui-checked', checked);

        const icon = label.querySelector('.material-icons');
        icon.classList.toggle('check_box', checked);
        icon.classList.toggle('check_box_outline_blank', !checked);
    }

    function enhanceFilterPopover() {
        const libraryId = hashParam('topParentId');
        const favorite = document.querySelector('#filter-popover input[type="checkbox"][value="IsFavorite"]');
        if (!libraryId || !favorite) {
            return;
        }

        const template = favorite.closest('.MuiFormControlLabel-root');
        const group = template && template.parentElement;
        if (!group || group.querySelector('.' + FILTER_CLASS)) {
            return;
        }

        const label = template.cloneNode(true);
        label.classList.add(FILTER_CLASS);

        const input = label.querySelector('input');
        input.value = 'HideMediaHidden';

        const icon = document.createElement('span');
        icon.className = 'material-icons';
        icon.setAttribute('aria-hidden', 'true');
        const svg = label.querySelector('svg');
        if (svg) {
            svg.replaceWith(icon);
        } else {
            input.after(icon);
        }

        const text = label.querySelector('.MuiFormControlLabel-label');
        if (text) {
            text.textContent = 'Hidden from Home';
        }

        renderFilterCheckbox(label, isFilterActive(libraryId));
        input.addEventListener('change', function () {
            setFilterActive(libraryId, input.checked);
            renderFilterCheckbox(label, input.checked);
            refreshLibrary();
        });

        group.appendChild(label);
    }

    new MutationObserver(function (mutations) {
        enhanceSettingsMenu();
        enhanceFilterPopover();
        mutations.forEach(function (mutation) {
            mutation.addedNodes.forEach(function (node) {
                if (!(node instanceof Element)) {
                    return;
                }
                const sheet = node.classList.contains('actionSheet') ? node : node.querySelector('.actionSheet');
                if (sheet) {
                    enhanceActionSheet(sheet);
                }
            });
        });
    }).observe(document.body, { childList: true, subtree: true });
})();

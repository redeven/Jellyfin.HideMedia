<img src="Images/hide-media.png" alt="Hide Media" width="400">

# Hide Media

Hide shows and movies from your Jellyfin Home page without removing them from your libraries.

## Features

- **Hide from Home** / **Unhide from Home** entry in the context menu of movies, shows, seasons and episodes, available from a card's ⋮ menu, a right-click, or the ⋮ button on an item's details page.
- Hiding an episode or season hides the whole show.
- Hidden items are removed from **Recently Added**, **Continue Watching** and **Next Up**, but still appear when you browse your libraries.
- Hidden items are per user: what you hide doesn't affect anyone else's Home page.
- A **Hide from Home** page in user Settings lets each user choose which of the three sections the hiding applies to.

## Prerequisites

- Jellyfin 12
- The [JavaScript Injector](https://github.com/n00bcodr/Jellyfin-JavaScript-Injector) plugin

> [!IMPORTANT]
> The context menu and Settings entries are delivered to the web client by JavaScript Injector. Without it, items you already hid stay hidden, but you can't hide or unhide anything.

## Installation

1. Install **JavaScript Injector** by following the instructions in [its README](https://github.com/n00bcodr/Jellyfin-JavaScript-Injector).
2. In Jellyfin, go to **Dashboard → Plugins → Repositories** and add a new repository:
   - **Name:** `Hide Media`
   - **URL:**
     ```
     https://raw.githubusercontent.com/redeven/Jellyfin.HideMedia/main/manifest.json
     ```
3. Open the **Catalog**, find **Hide Media** and install it.
4. Restart Jellyfin.
5. Check that **Hide Media** is listed as **Active** under **Dashboard → Plugins**, then reload your browser tab.

## Usage

- Open the ⋮ menu of a movie, show, season or episode and choose **Hide from Home**. Choose **Unhide from Home** on the same item to bring it back.
- Go to **Settings → Hide from Home** to choose which Home sections hidden items are removed from. All three are enabled by default.

## Caveats

- The web client caches Home sections for up to a minute, even across a page refresh, so hiding, unhiding or changing your settings can take up to a minute to show on Home.
- The context menu and Settings entries are only available in the web client. Other apps still respect the hidden list, since filtering happens on the server, but you need the web client to change it.
- Hidden items aren't replaced, so a Home row can show fewer items than usual.

## Running the tests

```sh
dotnet test tests/JellyfinHideMedia.Tests
cd tests/Web && npm ci && npm test
```

## License

Copyright (C) 2026 Redeven

This plugin is licensed under the [GNU General Public License v3.0](LICENSE).

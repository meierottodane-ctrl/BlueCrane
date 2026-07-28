# Crane design system — web port

The Blue Crane browser's design language, translated from WPF to CSS as the
foundation for the social app.

## Source of truth

The system itself is still [`# Crane Browser Design System.txt`](../%23%20Crane%20Browser%20Design%20System.txt)
at the repo root. This folder is a *translation*, not a fork of the rules — if
the two ever disagree, the text file wins and the CSS is wrong.

| File | What it is |
| --- | --- |
| `styles/tokens.css` | 1:1 port of `src/BlueCrane/Theme/Tokens.xaml`. Same values, same comments, same ordering, CSS custom properties instead of `ResourceDictionary` entries. Web-only additions are marked as such. |
| `styles/base.css` | Reset, typography, focus, selection, scrollbar, reduced-motion. The global rules that come from the system rather than from any one component. |
| `styles/components.css` | Rail, top bar, tabs, feed, post, composer, buttons, avatars, post actions, panels, meter. |
| `index.html` | A demo rendering every primitive with placeholder content. No build step, no dependencies, no network requests — it opens straight from the filesystem. |
| `assets/crane-white.png` | The brand illustration, copied from `src/BlueCrane/Assets/` so `web/` stays self-contained. |

Open `index.html` directly, or serve the folder — either works.

## Translation decisions worth knowing

These are the places where "port the browser's design system to a social app"
required an actual judgment call rather than a syntax change:

- **Avatars are square.** Social UI conventionally uses circles. The system
  says `❌ Rounded corners in the UI chrome` and `✓ Square corners`, with the
  app icon as the single stated exception. Circles would be a second exception
  nobody authorized, so avatars stay square.
- **Sky stays the only accent.** A social app wants a red heart, a green
  "online" dot, an unread badge. The system is explicit that Sky is the lone
  accent and must never dominate. Active/liked states use Sky plus a step on
  the value ladder. If a genuine semantic colour is needed later — destructive
  actions, most likely — it should be added to the system deliberately, not
  smuggled in through a component.
- **The composer inherits the omnibox.** "Full-width square band, not an inset
  field," raised surface, no border on the input itself, and a bottom hairline
  that glides to Sky on focus. That pattern transfers to a post composer
  almost unchanged.
- **The rail inherits the sidebar.** "Dark navy. Icons only. Expands on hover."
  is already a social-app nav spec; `--rail-width` / `--rail-width-open` carry it.
- **No shadows means the value ladder does the work.** With no elevation and no
  curves, a post separates from the feed the same way a tab separated from the
  chrome: `--chrome` behind, `--canvas` for the card, hairlines between.
- **The feed column mirrors the browser shell.** Top to bottom it is tab strip
  (`--chrome`, active tab lifting to `--canvas`), then the composer band
  (`--raised`), then the posts (`--canvas`) — the same sequence as tabs, address
  band, page.
- **The rail expands, and that is the one animated layout.** "Expands on hover"
  is in the sidebar spec, so a width has to move. It is contained: the rail is
  fixed and overlays the page, so the top bar, feed and aside never shift, and
  the labels fade rather than slide, so the glyphs do not move either. Nothing
  else in the system animates anything but colour and opacity.
- **A primary button on the composer steps up a rung.** The composer band is
  itself `--raised`, so a `--raised` button on it has no contrast at all. It sits
  on `--hover` instead, and since there is no rung above that, its hairline
  glides to Sky on hover — hover states are the first item on the accent's list
  of sanctioned uses.

## Icons

Drawn as inline SVG in the idiom of `src/BlueCrane/Theme/Icons.xaml`: stroked on
a 16×16 grid at 1.3, round caps and joins, never filled, always `currentColor` so
a parent's colour transition carries into the stroke. `Reload`, `Plus`, `Close`
and `Globe` are that file's path data verbatim.

The shapes follow the Blue Crane icon sheet — its navigation row (back, forward,
home, reload, new tab, bookmark, download, privacy, menu, close tab) and utility
row (search, voice search, settings, account, lock) are all present.

- **Redrawn, not cropped.** The sheet is a raster contact sheet. Slicing it would
  have given blurry icons that cannot take `currentColor`, cannot transition on
  hover, and cannot stay crisp across DPI. Redrawing keeps all three.
- **The crane is the one filled glyph.** Every other icon is a stroke. "Never use
  harsh outlines" is said of the bird specifically, and an outlined crane is
  exactly that; the favicon rule also asks for a *silhouette* with the fine
  feather detail removed. It is traced from `assets/crane-white.png` with the
  feathers closed into a single shape. The full illustration is used where there
  is room for it — the 48px brand avatar — and the silhouette below that.
- **Four sheet icons have no social equivalent** — back, forward, download, close
  tab. They are defined so the set is complete, and unused.

### Where the sheet and the design system disagree

The text file is the authority, so these were **not** applied. They are listed
because they are real differences, not oversights:

- **"Corners are rounded consistently at 16% radius."** The system scopes that
  radius to the app icon, where a platform mask fixes it, and says outright that
  it "does not extend to in-app UI". In-app icon buttons stay square. The rounded
  and circular frames on the sheet also read as contact-sheet presentation rather
  than part of the icons — the same icons are shown in rounded squares in one row
  and circles in the next.
- **The circular profile icon.** Avatars stay square, per the decision above.
- **`#0D2343` and `#1A2F4D`.** Two navies that are not on the derived ladder
  (`--color-surface` `#14293F`, `--color-raised` `#1E3A57`, `--color-hover`
  `#2A4A6B`). Adopting them would shift every surface in the app, so the ladder
  is unchanged. Worth resolving deliberately if the sheet is meant to be
  authoritative on palette.

## Not built yet

- Framework choice. Nothing here assumes one; the tokens, base layer and
  components drop into React/Svelte/Next or plain HTML unchanged.
- A mobile nav. Below 720px the feed goes fluid and the rail stays a 64px
  overlay, which works but is not what a phone should get.

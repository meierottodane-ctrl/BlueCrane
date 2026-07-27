# Crane design system — web port

The Blue Crane browser's design language, translated from WPF to CSS as the
foundation for the social app.

## Source of truth

The system itself is still [`# Crane Browser Design System.txt`](../%23%20Crane%20Browser%20Design%20System.txt)
at the repo root. This folder is a *translation*, not a fork of the rules — if
the two ever disagree, the text file wins and the CSS is wrong.

| File | What it is |
| --- | --- |
| `styles/tokens.css` | 1:1 port of `src/BlueCrane/Theme/Tokens.xaml`. Same values, same comments, same ordering, CSS custom properties instead of `ResourceDictionary` entries. |
| `styles/base.css` | Reset, typography, focus, selection, scrollbar, reduced-motion. The global rules that come from the system rather than from any one component. |

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

## Not built yet

- `styles/components.css` — rail, top bar, feed, post card, composer, buttons,
  avatars, post actions, right rail.
- `index.html` — a demo rendering every primitive so the language can be
  checked at a glance.
- Framework choice. Nothing here assumes one; the tokens and base layer drop
  into React/Svelte/Next or plain HTML unchanged.

# Reference screenshot inventory

The full text specification was read. None of the 19 image files was present in the project, its empty `sources` directory, or the supplied specification's folder at initial inspection. The following inventory is therefore **text-derived, not a completed visual inspection**. Supply the images to perform P01. No screenshot host, credential, command, image or wallpaper is seeded into the product.

| Reference | Spec-established layout/features | Implementation / remaining review |
|---|---|---|
| 1 | Sidebar, toolbar, grouped cards, previews, star and ellipsis | Native library with fallback preview, actions and status; compare proportions when image available |
| 2 | Alternate connection library view | Grid/list, search, sorting and group state; image comparison pending |
| 3 | PC editor General with PC/credentials above tabs | Four-tab editor, General fields; session mouse swapping unavailable |
| 4 | Display settings | Resolution/scaling/resize/full-screen controls where mapped; monitor/DPI capability gaps visible |
| 5 | Devices & Audio | Clipboard off/bidirectional, sound, printer/smart-card/mic requests; camera unavailable |
| 6 | Folders table and switch | Layout preserved with disabled explanation; exact folder/read-only enforcement open |
| 7 | General and reusable credential selector | Ask when required / saved profiles / add; gateway bypass conditional |
| 8 | Settings General | Local diagnostics policy; unsupported proxy/UDP controls explained |
| 9 | General continuation | Cloud/media options deferred; no fake Teams/Shortpath implementation |
| 10 | Personalization | View preferences and tray; screenshots unavailable, cached-preview deletion action; cloud categories deferred |
| 11 | Full Screen Mode | Always-visible toolbar and keyboard return; system/auto-hide parity pending |
| 12 | Notifications | Local tips and actionable errors; new-version service unavailable |
| 13 | Keyboard | Explicit Windows/Alt routing; normal Ctrl input preserved; individual interception/hotkey unavailable |
| 14 | Resolution presets | First part of 23 documented presets; add/remove validation |
| 15 | Resolution continuation | Remaining documented presets and restore; existing PCs retain dimensions |
| 16 | Credential manager | Metadata + vault-backed create/edit; masked password; blank preserves existing password |
| 17 | Credential editing | Stable references, explicit clear and confirmed deletion; vault primitive passed; editor flows and image comparison pending |
| 18 | Gateway manager | Labeled RD Gateway metadata/credentials; remove-to-direct confirmation; remote gateway proof pending |
| 19 | Group manager | Saved Devices protected, custom create/rename/delete/reorder; delete preserves PCs |

Settings order is General, Personalization, Full Screen Mode, Notifications, Keyboard, Resolutions, Credentials, Gateways, Groups. Back/forward navigation is a source requirement.

## Deliberate design choices rather than screenshot claims

- Green active indicator means the control emitted successful logon; a text status accompanies it.
- The context menu provides connect/focus, edit, duplicate, favorite, move, export and delete.
- View Options contains group/card-size preferences.
- New PCs default to prompt credentials, no gateway, automatic display, reconnect on, clipboard bidirectional, local audio, microphone/camera/printer/smart-card/folder sharing off. Screenshot storage is off/unavailable.
- Cloud Apps is an explanatory empty state. Native Windows titlebars, Segoe UI and original geometric fallback art replace platform-specific artwork.

These are source/workflow decisions. Windows render, DPI and accessibility review remains open; no claim of pixel or full feature parity is made.

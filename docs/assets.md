# Application assets

The application embeds the four notification WAV files supplied by the user. Permission uses `mixkit-doorbell-single-press-333.wav`, questions use `mixkit-magic-notification-ring-2344.wav`, completion uses `mixkit-software-interface-remove-2576.wav`, and errors use `mixkit-wrong-answer-fail-notification-946.wav`. The source copies are under `src/BellTower/Assets/Tones/`. They are normalized to playable 16-bit PCM in memory, without accessing Downloads at runtime. More than two queued alerts use the highest-priority reason's tone with the aggregate phrase.

The icon was generated with the built-in image generation tool. The transparent source is `src/BellTower/Assets/bell-tower.png`; `scripts/build-icon.ps1` produces `bell-tower.ico` with 16, 20, 24, 32, 40, 48, 64, 128 and 256 pixel frames. The executable, notification area and Sessions window use this icon.

Generation prompt:

> Use case: logo-brand. Asset type: Windows desktop application and system tray icon for Bell Tower, a personal tool that alerts when coding sessions need attention. Primary request: one distinctive compact bell-tower symbol: a clear bell silhouette within a minimal tower arch, with one small notification dot. Style: polished flat geometric application icon, bold simple silhouette, minimal internal detail, crisp edges, high contrast, readable down to 16 pixels. Composition: square canvas, centered single mark occupying 80% of canvas, no text or lettering, no mockup, no extra symbols, no border, no surrounding tile. Palette: warm amber bell and light ivory tower arch with deep navy inset accents, suitable on dark and light Windows taskbars. Background: genuinely transparent alpha.

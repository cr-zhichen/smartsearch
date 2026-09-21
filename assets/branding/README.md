# Smart Search icon

- `source.png` is the original transparent artwork, supplied on 2026-09-20.
  It is preserved unchanged and used for the sidebar mascot.
- `smart-search.png` is the owner's complete 1024 x 1024 Icon Composer export,
  supplied on 2026-09-21. Preserve its full canvas, Display P3 profile,
  transparency and 16-bit colour.

Regenerate the Windows PNG/ICO, macOS ICNS and embedded Web UI icons:

```sh
mise run desktop:icons
```

The task uses the project Python and a pinned Pillow dependency through `uv` on
macOS and Windows. It copies the exported PNG byte for byte and resizes its full
canvas for smaller icons, without trimming, adding margins or changing the design.
`desktop/scripts/Build-Icons.ps1` delegates to the same task for compatibility.

Packaged builds consume the committed generated resources and do not depend on
the original download location. The transparent sidebar mascot remains separate
from the complete application icon.

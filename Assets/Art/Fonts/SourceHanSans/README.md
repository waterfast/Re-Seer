# Chinese UI font

Source Han Sans CN Regular (思源黑体), from Adobe's official repository:
https://github.com/adobe-fonts/source-han-sans

Source binary: https://raw.githubusercontent.com/adobe-fonts/source-han-sans/release/SubsetOTF/CN/SourceHanSansCN-Regular.otf
License: see LICENSE.txt (SIL Open Font License 1.1).

Unity creates `SourceHanSansCN-Regular SDF.asset` on first script reload.
It uses Dynamic atlas population and multiple atlases, and is registered in
TMP Settings as a global fallback so existing LiberationSans labels gain Chinese
glyphs. The source OTF must remain in the project for dynamic glyph generation.
To use this typeface directly, drag the SDF asset into a TMP label's Font Asset.
Setup can also be run from Tools > Re-Seer > Setup Chinese Font.

## Fifth skill custom names

`SourceHanSansCN-Heavy.otf` comes from the same Adobe repository and uses the
included SIL OFL license. It is a substitute for editable fifth-skill titles,
not an identified official Seer title font. The existing-button setup creates
its dynamic TMP font and orange outline material when Unity imports the scripts.

Source binary: https://raw.githubusercontent.com/adobe-fonts/source-han-sans/release/SubsetOTF/CN/SourceHanSansCN-Heavy.otf

## Reference checked 2026-09-09

https://seer.61.com/ references:
- https://webres.61.com/common/css/game_public.css : body uses "Hiragino Sans GB", arial;
  headings list "Hiragino Sans GB", "Microsoft Yahei", simhei.
- https://webres.61.com/seer/site/css/index.css : FAQ uses 黑体.

These are website CSS declarations, not confirmation of the in-game skill font.
Source Han Sans is a similar Chinese sans-serif substitute, not an extracted Seer font.

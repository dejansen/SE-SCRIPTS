# AGM Screen Style Reference

**Script:** AutoGrid Manager v1.3+
**Author:** RevGamer

Reference for the AGM visual style — colours, fonts, layout rules, and draw helpers. Use this when building new dashboard pages or extending AGM.

---

## Colour Theme

All colours are defined as `readonly Color` fields on the `Program` class.

| Field | Hex | Color | Usage |
|---|---|---|---|
| `COL_BG` | #01080D | `new Color(1,8,13)` | Full screen background |
| `COL_PANEL` | #02121C | `new Color(2,18,28)` | Main panel background |
| `COL_PANEL2` | #033A4E | `new Color(3,58,78)` | Row backgrounds, inner panels |
| `COL_ACCENT` | #26EFFF | `new Color(38,239,255)` | Divider lines, borders, titles |
| `COL_ACCENT2` | #70F7FF | `new Color(112,247,255)` | Main title text |
| `COL_TEXT` | #7EF6FF | `new Color(126,246,255)` | Normal body text |
| `COL_DIM` | #2CB1C3 | `new Color(44,177,195)` | Labels, secondary text, dim info |
| `COL_ROW_TEXT` | #7EF6FF | `new Color(126,246,255)` | Row value text |
| `COL_ROW_DIM` | #3FCFDE | `new Color(63,207,222)` | Row label text |
| `COL_OK` | #61FFD6 | `new Color(97,255,214)` | OK/online/good state |
| `COL_WARN` | #FFCA22 | `new Color(255,202,34)` | Warning state |
| `COL_BAD` | #FF4F42 | `new Color(255,79,66)` | Critical/error state |
| `COL_PROG_BG` | #12301F | `new Color(18,48,32)` | Progress bar background |
| `COL_PROG_FILL` | #FFCC24 | `new Color(255,204,36)` | Progress bar fill |
| `LIGHT_GREEN` | — | `new Color(0,255,0)` | Alert light OK colour |
| `LIGHT_AMBER` | — | `new Color(255,160,0)` | Alert light Warning colour |
| `LIGHT_RED` | — | `new Color(255,0,0)` | Alert light Critical colour |

---

## Font

All text uses `"Monospace"` font. This is the only font that renders consistently across all LCD sizes in SE.

### Font scale reference

| Scale | Usage |
|---|---|
| `0.85f` — `0.95f` | Main page title |
| `0.65f` — `0.75f` | Section headers, grid names |
| `0.44f` — `0.55f` | Normal row text, values |
| `0.34f` — `0.42f` | Small labels, secondary info |
| `0.26f` — `0.32f` | Footer, version text |

---

## Layout Rules

### Viewport and panel

Every draw method starts with:

```csharp
var vp    = VP(s);           // full texture viewport
var panel = Inset(vp, 14f);  // inner panel with border gap
```

`VP()` returns the correct surface rectangle accounting for SE's texture offset. Always use `VP()` — never use raw `s.SurfaceSize` directly.

`Inset()` returns a rectangle inset by a given amount on all sides.

### Standard page structure

```
┌──────────────────────────────────┐  <- border (COL_ACCENT, 3px)
│                                  │
│  PAGE TITLE          v1.3        │  <- title left, version right
│                                  │
│ ─────────────────────────────── │  <- divider (COL_ACCENT, 1px)
│                                  │
│  Label          Value            │  <- row
│  Label          Value            │  <- row
│  ...                             │
│                                  │
│  AGM v1.3  |  RevGamer           │  <- footer (COL_DIM, small)
└──────────────────────────────────┘
```

### Responsive layout detection

| Condition | Layout |
|---|---|
| `panel.Height < 200f` | Compact — small grid PB, pocket LCD |
| `panel.Width > panel.Height * 2.5f` | Wide — nameplate LCD, wide banner |
| Else | Normal — standard LCD, tall LCD |

Always detect surface size and adapt. Never use fixed pixel offsets.

---

## Draw Helpers

### VP — Viewport

```csharp
private RectangleF VP(IMyTextSurface s)
```

Returns the correct drawing rectangle for a surface. Always call this first.

### Inset

```csharp
private RectangleF Inset(RectangleF r, float amount)
```

Returns rectangle inset by `amount` on all sides. Use for panel margins.

### Fill

```csharp
private void Fill(MySpriteDrawFrame fr, RectangleF r, Color c)
```

Fills a rectangle with a solid colour using `SquareSimple` sprite.

### DrawBorder

```csharp
private void DrawBorder(MySpriteDrawFrame fr, RectangleF r, Color c, float thickness)
```

Draws a 4-sided border around a rectangle. Standard thickness is `3f` for main panel, `1f` for rows.

### Txt

```csharp
private void Txt(MySpriteDrawFrame fr, string text, float x, float y, Color c, float scale, TextAlignment align)
```

Draws text at position. Always use `TextAlignment.LEFT`, `TextAlignment.CENTER`, or `TextAlignment.RIGHT`.

### Row

```csharp
private void Row(MySpriteDrawFrame fr, RectangleF panel, float y, string label, string value, Color valueColor)
```

Draws a standard label/value row with `COL_PANEL2` background and `COL_DIM` border. Label is left-aligned in `COL_DIM`, value is right-aligned in `valueColor`. Row height is `24f`.

### SmallRow

```csharp
private void SmallRow(MySpriteDrawFrame fr, RectangleF panel, float y, float height, string label, string value, Color valueColor, float scale)
```

Like `Row` but with configurable height and font scale. Used for compact/small layouts.

### DrawBar (progress bar)

```csharp
private void DrawBar(MySpriteDrawFrame fr, RectangleF panel, float y, string label, double pct, string unit, double warnAt, double badAt)
```

Draws a labelled progress bar row. Bar fill uses `BarColor()` which returns `COL_OK`, `COL_WARN`, or `COL_BAD` based on thresholds. Bar background is `COL_PROG_BG`, fill is the bar colour.

### BarColor

```csharp
private Color BarColor(double pct, double warnAt, double badAt)
```

Returns colour based on percentage thresholds:
- `pct <= badAt` → `COL_BAD`
- `pct <= warnAt` → `COL_WARN`
- else → `COL_OK`

### PrepSurf

```csharp
private void PrepSurf(IMyTextSurface s)
```

Sets `ContentType = SCRIPT`, `ScriptBackgroundColor = COL_BG`, `BackgroundColor = COL_BG`. Call once per surface — do not call every tick or it causes flicker.

---

## Standard Page Template

```csharp
private void DrawMyPage(IMyTextSurface s)
{
    PrepSurf(s);
    var vp    = VP(s);
    var panel = Inset(vp, 14f);
    using (var fr = s.DrawFrame())
    {
        // Background
        Fill(fr, vp, COL_BG);
        Fill(fr, panel, COL_PANEL);
        DrawBorder(fr, panel, COL_ACCENT, 3f);

        float cx = panel.X + panel.Width  * 0.5f;

        // Header
        Txt(fr, "MY PAGE", panel.X + 20f, panel.Y + 20f, COL_ACCENT2, 0.82f, TextAlignment.LEFT);
        Txt(fr, "v" + VERSION, panel.Right - 20f, panel.Y + 20f, COL_DIM, 0.38f, TextAlignment.RIGHT);

        // Divider
        float dy = panel.Y + 54f;
        Fill(fr, new RectangleF(panel.X + 10f, dy, panel.Width - 20f, 1f), COL_ACCENT);
        dy += 12f;

        // Content rows
        Row(fr, panel, dy, "Label", "Value", COL_OK); dy += 30f;
        Row(fr, panel, dy, "Label", "Value", COL_WARN); dy += 30f;

        // Footer
        Txt(fr, "AutoGrid Manager v" + VERSION, cx, panel.Bottom - 18f, COL_DIM, 0.32f, TextAlignment.CENTER);
    }
}
```

---

## Alert Corner LCD Pattern

Corner LCDs with `[AGM-LIGHT]` in Custom Data are drawn by `DrawAlertCornerLcd()` every tick via `DrawAlertLcds()`. They are never added to `_screens`.

Pattern:
- Large centred topic text (e.g. `BATTERY`) in alert colour
- Smaller status text below (`ONLINE` / `WARNING` / `CRITICAL`)
- Coloured border matching alert state
- Tiny `AGM v1.3` footer

Wide LCD variant: topic left, status right on same line.

---

## Adding a New Dashboard Page

1. Add `"MyPage"` to `HasDashboardCmd()` check
2. Add `case "mypage":` routing in `DrawScreen()`
3. Implement `DrawMyPage(IMyTextSurface s)` using the template above
4. Update `AGM_Guide.md` dashboard command table

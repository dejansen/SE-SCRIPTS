# AGM Screen Style Reference

**Script:** AutoGrid Manager v1.5
**Author:** RevGamer

Reference for AGM visual style -- colours, fonts, layout rules, and draw helpers.

---

## Colour Theme

| Field | Hex | Color() | Usage |
|-------|-----|---------|-------|
| `COL_BG` | #01080D | `new Color(1,8,13)` | Full screen background |
| `COL_PANEL` | #02121C | `new Color(2,18,28)` | Main panel background |
| `COL_PANEL2` | #033A4E | `new Color(3,58,78)` | Row backgrounds, inner panels |
| `COL_ACCENT` | #26EFFF | `new Color(38,239,255)` | Borders, dividers, titles |
| `COL_ACCENT2` | #70F7FF | `new Color(112,247,255)` | Main title text |
| `COL_TEXT` | #7EF6FF | `new Color(126,246,255)` | Normal body text |
| `COL_DIM` | #2CB1C3 | `new Color(44,177,195)` | Labels, secondary text |
| `COL_ROW_TEXT` | #7EF6FF | `new Color(126,246,255)` | Row value text |
| `COL_ROW_DIM` | #3FCFDE | `new Color(63,207,222)` | Row label text |
| `COL_OK` | #61FFD6 | `new Color(97,255,214)` | OK / online / good |
| `COL_WARN` | #FFCA22 | `new Color(255,202,34)` | Warning |
| `COL_BAD` | #FF4F42 | `new Color(255,79,66)` | Critical / error |
| `COL_PROG_BG` | #12301F | `new Color(18,48,32)` | Progress bar background |
| `COL_PROG_FILL` | #FFCC24 | `new Color(255,204,36)` | Progress bar fill |
| `LIGHT_GREEN` | -- | `new Color(0,255,0)` | Alert light OK |
| `LIGHT_AMBER` | -- | `new Color(255,160,0)` | Alert light Warning |
| `LIGHT_RED` | -- | `new Color(255,0,0)` | Alert light Critical |

---

## Font

All text uses `"Monospace"` font. Only font that renders consistently across all LCD sizes in SE.

### Font Scale Reference

| Scale | Usage |
|-------|-------|
| 0.85f -- 0.95f | Main page title |
| 0.65f -- 0.75f | Section headers |
| 0.44f -- 0.55f | Normal row text, values |
| 0.34f -- 0.42f | Small labels, secondary info |
| 0.26f -- 0.32f | Footer, version text |

---

## Layout Rules

### Viewport and Panel

```csharp
var vp    = VP(s);           // full texture viewport
var panel = Inset(vp, 14f);  // inner panel with border gap
```

Always use `VP()` -- never raw `s.SurfaceSize`.

### Standard Page Structure

```
+----------------------------------+  <- border (COL_ACCENT, 3px)
|                                  |
|  PAGE TITLE            v1.5      |  <- title left, version right
|                                  |
|  Label              Value        |  <- row
|  Label              Value        |
|  ...                             |
|                                  |
|  AutoGrid Manager v1.5           |  <- footer
+----------------------------------+
```

### Responsive Detection

| Condition | Layout |
|-----------|--------|
| `panel.Height < 200f` | Compact -- small grid PB |
| `panel.Width > panel.Height * 2.5f` | Wide -- nameplate LCD |
| Else | Normal |

---

## Draw Helpers

```csharp
private RectangleF VP(IMyTextSurface s)
private RectangleF Inset(RectangleF r, float amount)
private void Fill(MySpriteDrawFrame fr, RectangleF r, Color c)
private void DrawBorder(MySpriteDrawFrame fr, RectangleF r, Color c, float thickness)
private void Txt(MySpriteDrawFrame fr, string text, float x, float y, Color c, float scale, TextAlignment align)
private void FitTxt(MySpriteDrawFrame fr, string text, float x, float y, Color c, float scale, TextAlignment align, float width)
private void Row(MySpriteDrawFrame fr, RectangleF panel, float y, string label, string value, Color valueColor)
private void SmallRow(MySpriteDrawFrame fr, RectangleF panel, float y, float height, string label, string value, Color valueColor, float scale)
private void PrepSurf(IMyTextSurface s)
```

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
        Fill(fr, vp, COL_BG);
        Fill(fr, panel, COL_PANEL);
        DrawBorder(fr, panel, COL_ACCENT, 3f);

        float cx = panel.X + panel.Width * 0.5f;

        Txt(fr, "MY PAGE", panel.X + 20f, panel.Y + 20f, COL_ACCENT2, 0.82f, TextAlignment.LEFT);
        Txt(fr, "v" + VERSION, panel.Right - 20f, panel.Y + 20f, COL_DIM, 0.38f, TextAlignment.RIGHT);

        float y = panel.Y + 72f;
        Row(fr, panel, y, "Label", "Value", COL_OK);   y += 32f;
        Row(fr, panel, y, "Label", "Value", COL_WARN); y += 32f;

        Txt(fr, "AutoGrid Manager v" + VERSION, cx, panel.Bottom - 18f, COL_DIM, 0.32f, TextAlignment.CENTER);
    }
}
```

---

## Adding a New Dashboard Page

1. Add `"MyPage"` to `HasDashboardCmd()`
2. Add routing in `DrawScreen()`
3. Implement `DrawMyPage(IMyTextSurface s)` using template above
4. Update `AGM_Guide.md` dashboard command table
5. Update `AGM_Tag_Guide.md` table

# -*- coding: utf-8 -*-
"""
ChainRiposte 포트폴리오 PPT 생성기 (14장)

- 원본 ChainRiposte.pptx를 복사해 슬라이드만 전부 지우고 새로 짓는다.
  → 임베드된 Pretendard 폰트 · 슬라이드 마스터 · 테마가 그대로 살아 있다.
- 내용은 Docs/ppt대본.md 대본을 그대로 따른다(문장을 지어내지 않는다).
- 세로 배치는 V 배분기가 잡는다. 크기가 음수가 되면 그 자리에서 빌드가 죽는다
  (전에 음수 크기 도형 때문에 PowerPoint가 파일을 아예 못 열었다).
"""
import re, shutil, sys, io, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

from pptx import Presentation
from pptx.util import Inches, Pt, Emu
from pptx.dml.color import RGBColor
from pptx.enum.text import PP_ALIGN, MSO_ANCHOR
from pptx.enum.shapes import MSO_SHAPE, MSO_CONNECTOR
from pptx.oxml.ns import qn
from lxml import etree

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = r"C:\Study\Unity\ChainRiposte\Docs\ChainRiposte.pptx"
BASE = os.path.join(HERE, "_theme_src.pptx")
TMP = os.path.join(HERE, "_build.pptx")

# ─────────────────────────────────────────── 디자인 토큰 (원본 실측값)
INK    = RGBColor(0x00, 0x00, 0x00)
BODY   = RGBColor(0x21, 0x21, 0x21)
MUTED  = RGBColor(0x5C, 0x5C, 0x5C)
DARK   = RGBColor(0x1D, 0x1D, 0x1D)
BLUE   = RGBColor(0x00, 0x73, 0xFF)
BLUE_D = RGBColor(0x00, 0x4C, 0xB3)
BLUE_L = RGBColor(0x7A, 0xB8, 0xFF)
BLUE_T = RGBColor(0xEC, 0xF3, 0xFF)
CARD   = RGBColor(0xEB, 0xEB, 0xEB)
SOFT   = RGBColor(0xF5, 0xF5, 0xF5)
LINE   = RGBColor(0xDD, 0xDD, 0xDD)
RULE   = RGBColor(0x3F, 0x3F, 0x3F)
WHITE  = RGBColor(0xFF, 0xFF, 0xFF)
GREY   = RGBColor(0x6A, 0x6A, 0x6A)
GREY_L = RGBColor(0x99, 0x99, 0x99)
RED    = RGBColor(0xD1, 0x36, 0x2B)
RED_T  = RGBColor(0xFD, 0xF0, 0xEF)

F_B, F_L, F_SB, F_EB, F_C = ("Pretendard Bold", "Pretendard Light",
                             "Pretendard SemiBold", "Pretendard ExtraBold", "Consolas")

# 레이아웃 (inch)
PANEL_X, PANEL_W = 0.78, 25.12
HDR_Y, HDR_H     = 0.74, 0.93
BODY_Y, BODY_H   = 1.74, 12.53
CX, CW           = 2.24, 22.19
TITLE_Y, TITLE_H = 2.06, 1.35
RULE1_Y, SUB_Y, RULE2_Y = 3.64, 4.02, 4.93
TOP_SUB, TOP_NOSUB = 5.40, 4.14
BOT_RULE_Y, BOTTOM = 13.62, 13.45

DATE, BRAND = "2026-08", "ChainRiposte"
TOKEN = re.compile(r"\*\*(.+?)\*\*|`(.+?)`")
MIN = 0.12  # 이보다 작은 도형은 만들지 않는다


class V:
    """세로 공간 배분기. 남은 공간을 넘겨 쓰면 즉시 죽는다."""

    def __init__(self, top, bottom, label=""):
        self.y, self.bottom, self.label = top, bottom, label

    def take(self, h, gap=0.0):
        assert h >= MIN, f"{self.label}: 높이 {h:.2f} 가 너무 작다"
        assert self.y + h <= self.bottom + 1e-6, (
            f"{self.label}: 세로 공간 초과 — {h:.2f} 를 넣으려는데 {self.bottom - self.y:.2f} 남음")
        y = self.y
        self.y = y + h + gap
        return y

    def rest(self):
        r = self.bottom - self.y
        assert r >= MIN, f"{self.label}: 남은 공간 없음 ({r:.2f})"
        return r

    def take_rest(self):
        r = self.rest()
        return self.take(r), r


# ─────────────────────────────────────────── 저수준 헬퍼
def _set_font(run, name, size, color, bold=False):
    f = run.font
    f.name, f.bold = name, bold
    f.size = Pt(size)
    f.color.rgb = color
    rPr = run._r.get_or_add_rPr()
    for tag in ("a:ea", "a:cs"):
        el = rPr.find(qn(tag))
        if el is None:
            el = rPr.makeelement(qn(tag), {})
            rPr.append(el)
        el.set("typeface", name)


def _effect(shape, xml):
    spPr = shape._element.spPr
    old = spPr.find(qn("a:effectLst"))
    if old is not None:
        spPr.remove(old)
    spPr.append(etree.fromstring(xml))


NO_SHADOW = '<a:effectLst xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"/>'
SHADOW = ('<a:effectLst xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">'
          '<a:outerShdw blurRad="76200" dist="25400" dir="5400000" rotWithShape="0">'
          '<a:srgbClr val="000000"><a:alpha val="9000"/></a:srgbClr></a:outerShdw></a:effectLst>')


def rect(sl, x, y, w, h, fill=None, line=None, lw=1.0, radius=None,
         shape=MSO_SHAPE.ROUNDED_RECTANGLE, shadow=False, dash=False):
    assert w >= MIN and h >= MIN, f"도형 크기 비정상 w={w:.2f} h={h:.2f} (x={x:.2f} y={y:.2f})"
    s = sl.shapes.add_shape(shape, Inches(x), Inches(y), Inches(w), Inches(h))
    if radius is not None and shape == MSO_SHAPE.ROUNDED_RECTANGLE:
        s.adjustments[0] = max(0.0, min(0.5, radius / min(w, h)))
    if fill is None:
        s.fill.background()
    else:
        s.fill.solid()
        s.fill.fore_color.rgb = fill
    if line is None:
        s.line.fill.background()
    else:
        s.line.color.rgb = line
        s.line.width = Pt(lw)
        if dash:
            s.line.dash_style = 4
    s.shadow.inherit = False
    _effect(s, SHADOW if shadow else NO_SHADOW)
    s.text_frame.word_wrap = True
    return s


def hline(sl, x, y, w, color=RULE, lw=1.25):
    c = sl.shapes.add_connector(MSO_CONNECTOR.STRAIGHT, Inches(x), Inches(y),
                                Inches(x + w), Inches(y))
    c.line.color.rgb, c.line.width = color, Pt(lw)
    return c


def vline(sl, x, y, h, color=RULE, lw=1.25):
    c = sl.shapes.add_connector(MSO_CONNECTOR.STRAIGHT, Inches(x), Inches(y),
                                Inches(x), Inches(y + h))
    c.line.color.rgb, c.line.width = color, Pt(lw)
    return c


def _fill_tf(tf, paras, size, font, color, align, spacing, em, space_after):
    if isinstance(paras, str):
        paras = [paras]
    first = True
    for item in paras:
        opt = dict(item) if isinstance(item, dict) else {}
        s = opt.pop("t", "") if isinstance(item, dict) else item
        p = tf.paragraphs[0] if first else tf.add_paragraph()
        first = False
        p.alignment = opt.get("align", align)
        p.line_spacing = opt.get("spacing", spacing)
        if opt.get("space_before"):
            p.space_before = Pt(opt["space_before"])
        sa = opt.get("space_after", space_after)
        if sa:
            p.space_after = Pt(sa)
        f_, sz = opt.get("font", font), opt.get("size", size)
        co, emc = opt.get("color", color), opt.get("em", em)
        if not s:
            r = p.add_run(); r.text = " "; _set_font(r, f_, sz, co); continue
        pos = 0
        for m in TOKEN.finditer(s):
            if m.start() > pos:
                r = p.add_run(); r.text = s[pos:m.start()]; _set_font(r, f_, sz, co)
            if m.group(1) is not None:
                r = p.add_run(); r.text = m.group(1); _set_font(r, F_B, sz, emc)
            else:
                r = p.add_run(); r.text = m.group(2); _set_font(r, F_C, sz * 0.92, BLUE_D)
            pos = m.end()
        if pos < len(s):
            r = p.add_run(); r.text = s[pos:]; _set_font(r, f_, sz, co)


def text(sl, x, y, w, h, paras, size=27, font=F_B, color=BODY, align=PP_ALIGN.LEFT,
         anchor=MSO_ANCHOR.TOP, spacing=1.22, em=BLUE_D, space_after=0.0, box=None,
         fit=True):
    assert w >= MIN and h >= MIN, f"텍스트 상자 비정상 w={w:.2f} h={h:.2f} (y={y:.2f})"
    tb = box or sl.shapes.add_textbox(Inches(x), Inches(y), Inches(w), Inches(h))
    if not fit:
        tb.name = "FREE " + tb.name
    tf = tb.text_frame
    tf.word_wrap = True
    tf.vertical_anchor = anchor
    tf.margin_left = tf.margin_right = tf.margin_top = tf.margin_bottom = 0
    _fill_tf(tf, paras, size, font, color, align, spacing, em, space_after)
    return tb


def label_in(shape, s, size, font=F_B, color=WHITE, align=PP_ALIGN.CENTER,
             anchor=MSO_ANCHOR.MIDDLE, em=None, spacing=1.15, pad=0.18):
    tf = shape.text_frame
    tf.word_wrap = True
    tf.vertical_anchor = anchor
    tf.margin_left = tf.margin_right = Inches(pad)
    tf.margin_top = tf.margin_bottom = 0
    _fill_tf(tf, s, size, font, color, align, spacing, em or color, 0.0)
    return shape


# ─────────────────────────────────────────── 슬라이드 골격
def chrome(prs, page):
    sl = prs.slides.add_slide(prs.slide_layouts[6])
    rect(sl, PANEL_X, BODY_Y, PANEL_W, BODY_H, fill=WHITE, radius=0.14, shadow=True)
    rect(sl, PANEL_X, HDR_Y, PANEL_W, HDR_H, fill=DARK, radius=0.12)
    text(sl, PANEL_X + 0.71, HDR_Y + 0.20, 6.0, 0.55, BRAND, size=25.5, color=WHITE)
    text(sl, PANEL_X + PANEL_W - 7.0, HDR_Y + 0.26, 6.3, 0.45, DATE, size=20,
         color=WHITE, align=PP_ALIGN.RIGHT)
    hline(sl, 1.46, BOT_RULE_Y, 23.26, RULE, 1.1)
    text(sl, PANEL_X + PANEL_W - 6.5, BOT_RULE_Y + 0.13, 5.9, 0.45, str(page),
         size=18.5, color=DARK, align=PP_ALIGN.RIGHT)
    return sl


def head(sl, title, sub=None):
    text(sl, 4.21, TITLE_Y, 18.25, TITLE_H, title, size=67.5, color=INK,
         align=PP_ALIGN.CENTER, anchor=MSO_ANCHOR.MIDDLE, fit=False)
    hline(sl, 1.44, RULE1_Y, 23.79, RULE, 1.25)
    if sub:
        text(sl, 2.6, SUB_Y, 21.5, 0.62, sub, size=23, font=F_L, color=BODY,
             align=PP_ALIGN.CENTER, anchor=MSO_ANCHOR.MIDDLE, fit=False)
        hline(sl, 1.44, RULE2_Y, 23.79, RULE, 1.25)
        return V(TOP_SUB, BOTTOM, title)
    return V(TOP_NOSUB, BOTTOM, title)


# ─────────────────────────────────────────── 컴포넌트
def badge(sl, x, y, w, h, s, size=25, fill=BLUE, color=WHITE):
    return label_in(rect(sl, x, y, w, h, fill=fill, radius=0.10), s, size, F_B, color)


def row_card(sl, y, h, tag, title, desc, tag_w=3.2, title_w=6.2, fill=SOFT,
             size_title=31, size_desc=26.5):
    rect(sl, CX, y, CW, h, fill=fill, radius=0.13, line=LINE, lw=0.75)
    badge(sl, CX + 0.55, y + (h - 0.72) / 2, tag_w, 0.72, tag, size=24)
    tx = CX + 0.55 + tag_w + 0.55
    text(sl, tx, y + 0.2, title_w, h - 0.4, title, size=size_title, color=INK,
         anchor=MSO_ANCHOR.MIDDLE)
    dx = tx + title_w + 0.5
    text(sl, dx, y + 0.2, CX + CW - 0.6 - dx, h - 0.4, desc, size=size_desc,
         color=BODY, anchor=MSO_ANCHOR.MIDDLE)


def col_card(sl, x, y, w, h, kicker, title, lines, kicker_fill=BLUE,
             size_title=34, size_body=26, fill=SOFT, pad=0.45):
    rect(sl, x, y, w, h, fill=fill, radius=0.13, line=LINE, lw=0.75)
    cy = y + pad
    if kicker:
        badge(sl, x + pad, cy, min(4.2, w - 2 * pad), 0.64, kicker, size=22, fill=kicker_fill)
        cy += 0.64 + 0.18
    text(sl, x + pad, cy, w - 2 * pad, 0.72, title, size=size_title, color=INK)
    cy += 0.72 + 0.10
    bh = (y + h - pad) - cy
    text(sl, x + pad, cy, w - 2 * pad, bh, lines, size=size_body, color=BODY,
         spacing=1.30, space_after=7)


def callout(sl, y, h, quote, note=None, fill=BLUE_T, bar=BLUE, size_q=33, size_n=26,
            qcolor=BLUE_D):
    rect(sl, CX, y, CW, h, fill=fill, radius=0.13)
    rect(sl, CX, y, 0.17, h, fill=bar, shape=MSO_SHAPE.RECTANGLE)
    paras = [{"t": quote, "size": size_q, "color": qcolor}]
    if note:
        paras.append({"t": note, "size": size_n, "color": BODY, "space_before": 10})
    text(sl, CX + 0.85, y + 0.22, CW - 1.7, h - 0.44, paras,
         anchor=MSO_ANCHOR.MIDDLE, em=qcolor)


def table(sl, y, h, cols, header, rows, hdr_h=0.80, gap=0.13, highlight=None,
          size_h=24, size_r=26.5):
    """h 안에 머리행 + 본문행을 균등 배분한다."""
    n = len(rows)
    row_h = (h - hdr_h - gap * n) / n
    assert row_h >= 0.5, f"표 행 높이가 너무 작다 ({row_h:.2f})"
    tot = sum(cols)
    xs, acc = [], CX
    for c in cols:
        xs.append((acc, CW * c / tot))
        acc += CW * c / tot
    rect(sl, CX, y, CW, hdr_h, fill=DARK, radius=0.09)
    for (x, w), t in zip(xs, header):
        text(sl, x + 0.55, y, w - 1.0, hdr_h, t, size=size_h, color=WHITE,
             anchor=MSO_ANCHOR.MIDDLE)
    yy = y + hdr_h + gap
    for i, row in enumerate(rows):
        hot = (highlight is not None and i == highlight)
        rect(sl, CX, yy, CW, row_h,
             fill=(BLUE_T if hot else (SOFT if i % 2 == 0 else CARD)),
             radius=0.09, line=(BLUE if hot else None), lw=1.5)
        for j, ((x, w), t) in enumerate(zip(xs, row)):
            text(sl, x + 0.55, yy + 0.06, w - 1.0, row_h - 0.12, t, size=size_r,
                 color=(BLUE_D if hot and j == 0 else (INK if j == 0 else BODY)),
                 anchor=MSO_ANCHOR.MIDDLE, em=(BLUE_D if hot else BLUE_D))
        yy += row_h + gap


def chips(sl, y, h, items, gap=0.30, fill=SOFT, color=INK, size=25, arrows=False,
          accent_last=False):
    n = len(items)
    aw = 0.62 if arrows else 0.0
    w = (CW - gap * (n - 1) - aw * (n - 1)) / n
    x = CX
    for i, it in enumerate(items):
        hot = accent_last and i == n - 1
        b = rect(sl, x, y, w, h, fill=(BLUE if hot else fill),
                 radius=0.11, line=(None if hot else LINE), lw=0.75)
        label_in(b, it, size, F_B, WHITE if hot else color)
        x += w
        if arrows and i < n - 1:
            text(sl, x, y, aw, h, "→", size=30, color=BLUE, align=PP_ALIGN.CENTER,
                 anchor=MSO_ANCHOR.MIDDLE)
            x += aw
        x += gap


def media(sl, x, y, w, h, caption, sub=None):
    s = rect(sl, x, y, w, h, fill=SOFT, radius=0.12,
             line=RGBColor(0xB8, 0xB8, 0xB8), lw=1.6, dash=True)
    paras = [{"t": caption, "size": 26, "color": GREY}]
    if sub and h > 0.95:
        paras.append({"t": sub, "size": 21, "color": GREY_L, "font": F_L, "space_before": 7})
    label_in(s, paras, 26, F_B, GREY)
    return s


def stat(sl, x, y, w, h, big, cap, color=BLUE):
    rect(sl, x, y, w, h, fill=SOFT, radius=0.13, line=LINE, lw=0.75)
    text(sl, x + 0.3, y + 0.30, w - 0.6, h * 0.50, big, size=56, font=F_EB, color=color,
         align=PP_ALIGN.CENTER, anchor=MSO_ANCHOR.MIDDLE)
    text(sl, x + 0.3, y + h * 0.55, w - 0.6, h * 0.38, cap, size=23, color=BODY,
         align=PP_ALIGN.CENTER, anchor=MSO_ANCHOR.MIDDLE)


def cols3(v, h, gap=0.44):
    """3열 카드 자리 (x, y, w, h) 목록 + 배정된 y."""
    w = (CW - gap * 2) / 3
    y = v.take(h, gap)
    return [(CX + (w + gap) * i, y, w, h) for i in range(3)], y


def cols2(v, h, gap=0.55):
    w = (CW - gap) / 2
    y = v.take(h, gap)
    return [(CX, y, w, h), (CX + w + gap, y, w, h)], y


# ═══════════════════════════════════════════ 슬라이드
def s01_cover(prs):
    sl = prs.slides.add_slide(prs.slide_layouts[6])
    rect(sl, 0.76, 0.74, 1.11, 13.53, fill=BLUE, radius=0.12)
    tb = sl.shapes.add_textbox(Inches(-4.39), Inches(6.6), Inches(5.25), Inches(0.9))
    text(sl, 0, 0, 1, 1, "Portfolio", size=44, color=WHITE, align=PP_ALIGN.CENTER,
         anchor=MSO_ANCHOR.MIDDLE, box=tb)
    tb.rotation = 270
    tb2 = sl.shapes.add_textbox(Inches(-2.10), Inches(11.9), Inches(3.4), Inches(0.5))
    text(sl, 0, 0, 1, 1, DATE, size=20, color=WHITE, align=PP_ALIGN.CENTER,
         anchor=MSO_ANCHOR.MIDDLE, box=tb2)
    tb2.rotation = 270

    rect(sl, 1.96, 0.74, 23.93, 13.53, fill=WHITE, radius=0.14, shadow=True)
    hline(sl, 2.79, 1.39, 20.29, RULE, 1.1)
    badge(sl, 23.20, 1.06, 1.90, 0.66, "1", size=20, fill=DARK)

    text(sl, 4.6, 2.35, 18.6, 0.5, "신입 게임 클라이언트 프로그래머 포트폴리오",
         size=22, font=F_L, color=MUTED, align=PP_ALIGN.CENTER)
    text(sl, 4.4, 4.18, 19.0, 2.30, "ChainRiposte", size=110, font=F_L, color=INK,
         align=PP_ALIGN.CENTER, anchor=MSO_ANCHOR.MIDDLE, fit=False)
    text(sl, 4.4, 6.08, 19.0, 2.30, "고쳐도 안 무너지는 구조", size=100, font=F_EB,
         color=INK, align=PP_ALIGN.CENTER, anchor=MSO_ANCHOR.MIDDLE, fit=False)
    hline(sl, 12.51, 8.75, 2.87, BLUE, 3.0)
    chips(sl, 9.45, 1.05, ["Unity 6", "C#", "1인 개발", "3주 (2026.07.14 ~ 08.03)"], size=27)

    hline(sl, 2.78, 11.55, 22.18, RULE, 1.1)
    text(sl, 2.65, 12.05, 9.74, 0.5, "PROJECT", size=24, font=F_SB, color=BLUE,
         align=PP_ALIGN.CENTER)
    text(sl, 2.67, 12.67, 9.72, 0.9, "Unity 퍼즐 + 패링 전투 · 매치3 → 보스전 반복",
         size=22, font=F_L, color=BODY, align=PP_ALIGN.CENTER)
    vline(sl, 13.33, 12.00, 1.35, LINE, 1.2)
    text(sl, 14.26, 12.05, 9.74, 0.5, "SCALE", size=24, font=F_SB, color=BLUE,
         align=PP_ALIGN.CENTER)
    text(sl, 14.28, 12.67, 9.72, 0.9,
         "170파일 / 25,546줄 · 157커밋 · EditMode 테스트 167개 전부 통과",
         size=22, font=F_L, color=BODY, align=PP_ALIGN.CENTER)


def s02_game(prs):
    sl = chrome(prs, 2)
    v = head(sl, "두 장르를 한 자원으로 묶었다", "퍼즐은 준비 구간, 보스전은 실력 구간.")

    y = v.take(0.42, 0.12)
    text(sl, CX, y, CW, 0.42, "한 판의 흐름", size=26, color=BLUE)
    y = v.take(0.95, 0.34)
    chips(sl, y, 0.95, ["월드맵", "퍼즐 (매치3)", "준비 (스탯 분배)", "보스전 (패링)", "다음 고리"],
          arrows=True, size=24, accent_last=True)

    boxes, y = cols2(v, 2.85, 0.55)
    col_card(sl, *boxes[0], None, "퍼즐 → 보스전",
             ["퍼즐에서 캔 소울로 레벨업하고, **퍼즐에서 맞은 HP가 그대로 보스전으로 넘어간다.**"],
             size_title=38, size_body=27)
    col_card(sl, *boxes[1], None, "성장은 남는다",
             ["죽으면 **사슬만 끊기고 성장은 남는다.** 빌드를 잃지 않으므로 다시 도전할 이유가 생긴다."],
             size_title=38, size_body=27)

    y = v.take(1.60, 0.30)
    callout(sl, y, 1.60,
            "난이도는 **실행(패링)**에서 오고, 성장은 **모서리만 깎는다.**",
            "성장이 난이도를 지우면 퍼즐이 무의미해지고, 아무것도 안 하면 성장이 무의미해진다.",
            size_q=34, size_n=25)

    y, h = v.take_rest()
    media(sl, CX, y, CW, h, "스크린샷 — 퍼즐 화면 + 보스전 화면 나란히")


def s03_layers(prs):
    sl = chrome(prs, 3)
    v = head(sl, "의존 방향을 컴파일러가 강제한다",
             "`noEngineReferences: true` 한 줄이 「약속」을 「빌드 에러」로 만들었다.")

    y = v.take(6.06, 0.36)
    table(sl, y, 6.06, [1.35, 1.7, 4.2], ["계층", "규모", "무엇"],
          [["Editor", "19파일 / 4,920줄", "채보 · 보드 · 현지화 툴 (커스텀 메뉴 34종)"],
           ["Game (어댑터)", "74파일 / 12,464줄", "MonoBehaviour · UI · VFX · Audio"],
           ["Core (순수 C#)", "59파일 / 5,025줄", "★ **UnityEngine 참조 불가 — 컴파일 강제** ★"],
           ["Core.Tests", "18파일 / 3,137줄", "EditMode 167개 · 에디터 없이 실행"]],
          highlight=2)

    y, h = v.take_rest()
    rect(sl, CX, y, CW, h, fill=SOFT, radius=0.13, line=LINE, lw=0.75)
    text(sl, CX + 0.85, y + 0.22, CW - 1.7, h - 0.44,
         [{"t": "왜 규칙을 코드로 강제했나", "size": 30, "color": INK},
          {"t": "\"Core에 Unity를 쓰지 말자\"는 **지켜지지 않는다.** 그것이 컴파일 에러가 된 순간부터 규칙이 진짜가 됐다. → 이 결정이 값을 한 순간은 **10장**에 있다.",
           "size": 26, "color": BODY, "space_before": 9}],
         anchor=MSO_ANCHOR.MIDDLE)


def s04_boundary(prs):
    sl = chrome(prs, 4)
    v = head(sl, "경계를 넘는 방법이 3가지뿐이다",
             "배선이 전부 같은 패턴을 따르면, 새 기능도 그 패턴에 붙는다.")

    for tag, title, desc in [
        ("규칙 1", "ScriptableObject",
         "밸런스 수치는 전부 SO. 코드에 상수로 박힌 밸런스가 **0**이다 (SO 10종)."),
        ("규칙 2", "Func<> 로 감싼다",
         "`AnimationCurve` → `Func<float,float>`. Core는 커브를 \"값을 돌려주는 함수\"로만 안다."),
        ("규칙 3", "C# event 25종",
         "Core는 이벤트로만 말한다. **역방향 참조 없이** 화면이 반응한다."),
    ]:
        row_card(sl, v.take(1.30, 0.22), 1.30, tag, title, desc)
    v.y += 0.08

    y = v.take(1.85, 0.28)
    callout(sl, y, 1.85, "코드에 고유명사를 적지 않는다",
            "`if (stageName == \"2-1\")` 같은 코드가 없다. 난이도도 표가 아니라 **고리 깊이 × 증가율로 계산**한다 — 표였다면 갱신을 잊어 **새 판만 조용히 물렁해진다.**",
            size_q=31, size_n=25)

    y, h = v.take_rest()
    row_card(sl, y, h, "확장 비용", "붙이는 값이 1개다",
             "스테이지 = 에셋 1개  ·  캐릭터 = 에셋 1개  ·  기믹 = `StageGimmick` 상속 1개  ·  언어 = CSV 열 1개",
             tag_w=3.4, title_w=5.4)


def s05_combat(prs):
    sl = chrome(prs, 5)
    v = head(sl, "어필 기술 ① 리듬 패링 전투",
             "프레임레이트와 무관하게, 같은 입력이면 같은 결과가 나온다.")

    boxes, y = cols3(v, 4.60, 0.36)
    col_card(sl, *boxes[0], "채보 기반", "박(beat) 단위 노트",
             ["보스 공격을 타격 시점 · 예비동작 길이 · 피해 배율로 찍는다.",
              "**연속기는 별도 타입이 아니다** — 노트를 촘촘히 찍으면 그게 연속기다."])
    col_card(sl, *boxes[1], "판정 = 뷰", "보이는 것이 곧 판정",
             ["다가오는 흰 원의 **안쪽 테두리**가 노트의 위치이고, 회색 띠의 두께가 **판정 폭 그 자체**다.",
              "로직과 뷰가 같은 수치에서 파생되므로 어긋날 수 없다."])
    col_card(sl, *boxes[2], "결정적", "경계까지만 진행",
             ["프레임 단위로 흘리지 않고 **다음 상태 경계**(타격 시점 · 윈도우 만료)까지만 진행한다.",
              "→ 엔진 없이 순수 C#으로 **전투 전체를 테스트**할 수 있다."])

    y = v.take(1.40, 0.30)
    callout(sl, y, 1.40, "PARRY 스탯을 올리면 띠가 **눈에 띄게 굵어진다.**", size_q=32)

    y, h = v.take_rest()
    media(sl, CX, y, CW, h, "▶ 영상 5초 — 패링 판정 링",
          "정지 이미지로는 설명이 안 된다 · 인살 컷씬도 함께")


def s06_puzzle(prs):
    sl = chrome(prs, 6)
    v = head(sl, "어필 기술 ② 퍼즐 엔진",
             "모델은 완결된 결과를 한 번에 내고, 뷰는 그 기록을 재생만 한다.")

    dh = 2.15
    y = v.take(dh, 0.34)
    rect(sl, CX, y, CW, dh, fill=SOFT, radius=0.13, line=LINE, lw=0.75)
    bw, bh, ar = 6.30, 1.10, 1.15
    bx = CX + 0.70
    b = rect(sl, bx, y + 0.32, bw, bh, fill=WHITE, radius=0.10, line=BLUE, lw=1.8)
    label_in(b, "PuzzleEngine.Swap(a, b)", 25, F_C, BLUE_D)
    text(sl, bx + bw, y + 0.32, ar, bh, "→", size=32, color=BLUE,
         align=PP_ALIGN.CENTER, anchor=MSO_ANCHOR.MIDDLE)
    bx2 = bx + bw + ar
    b = rect(sl, bx2, y + 0.32, bw, bh, fill=BLUE, radius=0.10)
    label_in(b, "SwapResult", 25, F_C, WHITE)
    text(sl, bx2 + bw, y + 0.32, ar, bh, "→", size=32, color=BLUE,
         align=PP_ALIGN.CENTER, anchor=MSO_ANCHOR.MIDDLE)
    b = rect(sl, bx2 + bw + ar, y + 0.32, bw, bh, fill=WHITE, radius=0.10, line=BLUE, lw=1.8)
    label_in(b, "BoardView 재생", 25, F_C, BLUE_D)
    text(sl, CX + 0.70, y + 1.52, CW - 1.4, 0.50,
         "**SwapResult** = 캐스케이드 · 낙하 · 기믹이 전부 담긴 완결 기록. 연출이 도는 **중에도 모델은 이미 최종 상태**다.",
         size=25, color=BODY)

    boxes, y = cols2(v, 3.35, 0.50)
    col_card(sl, *boxes[0], None, "비정형 보드를 버티는 규칙",
             ["하트 · 해골 모양 보드 · 중력 + 대각 슬라이드 · **데드락 검출 후 턴 미소모 리롤**",
              "매치 없는 스왑은 턴을 안 먹으므로, 수가 없으면 게임이 그냥 멈춘다."],
             size_title=35, size_body=25)
    x2, y2, w2, h2 = boxes[1]
    rect(sl, x2, y, w2, 3.35, fill=BLUE_T, radius=0.13)
    rect(sl, x2, y, 0.17, 3.35, fill=BLUE, shape=MSO_SHAPE.RECTANGLE)
    text(sl, x2 + 0.80, y + 0.40, w2 - 1.55, 2.55,
         [{"t": "핵심 불변식", "size": 23, "color": BLUE},
          {"t": "화면에 보이는 빈칸은 **의도한 구멍뿐**이다.", "size": 33, "color": BLUE_D,
           "space_before": 6},
          {"t": "갇힌 칸은 옆 타일이 미끄러져 들어와 메운다 — 실측 **99.08%.**",
           "size": 25, "color": BODY, "space_before": 10}], anchor=MSO_ANCHOR.MIDDLE)

    y, h = v.take_rest()
    media(sl, CX, y, CW, h, "▶ 영상 5초 — 퍼즐 낙하 · 연쇄",
          "대각 슬라이드 · 캐스케이드가 한눈에 보인다")


def s07_tools(prs):
    sl = chrome(prs, 7)
    v = head(sl, "어필 기술 ③ 에디터 툴 34종",
             "기획 · 아트 반복 작업을 코드로 지웠다. 전체 코드의 19%가 툴이다.")

    y = v.take(4.70, 0.30)
    table(sl, y, 4.70, [2.0, 5.0], ["툴", "해결한 것"],
          [["채보 에디터", "타임라인 클릭 / 드래그 · 스냅(1 · 1/2 · 1/4박) · 예비동작 시각화 · **▶ 미리듣기**"],
           ["보드 그리드 에디터", "문자열 대신 **칠하는 격자.** 인스펙터 + 전용 창"],
           ["현지화 툴", "구글 시트 → CSV 동기화 · **씬 내 누락 키 검사**"],
           ["지도 노드 편집", "씬 뷰 핸들 드래그 · 테마별 레이아웃 저장 / 불러오기"]],
          hdr_h=0.78, gap=0.12, size_r=26)

    y = v.take(2.05, 0.26)
    rect(sl, CX, y, CW, 2.05, fill=SOFT, radius=0.13, line=LINE, lw=0.75)
    text(sl, CX + 0.85, y + 0.18, CW - 1.7, 1.69,
         ["**툴도 데이터 주도다** — 눌림 스프라이트 배선은 목록을 코드에 적지 않고 **이름 규칙**(`btn_wide` → `btn_wide_pressed`)으로 찾는다. 아트가 늘어도 툴은 안 고친다.",
          "**에셋 생성 툴은 빈 슬롯만 채운다** — 다시 눌러도 손으로 고쳐 둔 값이 안 지워진다. 사람은 반드시 두 번 누른다."],
         size=25, color=BODY, spacing=1.26, space_after=5, anchor=MSO_ANCHOR.MIDDLE)

    y, h = v.take_rest()
    media(sl, CX, y, CW, h, "스크린샷 — 채보 에디터 타임라인")


def s08_toolrules(prs):
    sl = chrome(prs, 8)
    v = head(sl, "툴을 만들며 세운 규칙", "계속 쓸 도구여야 하므로, 툴에도 설계가 필요했다.")

    def block(h, num, title, lines, gap=0.30):
        y = v.take(h, gap)
        rect(sl, CX, y, CW, h, fill=SOFT, radius=0.13, line=LINE, lw=0.75)
        badge(sl, CX + 0.55, y + 0.34, 0.68, 0.68, num, size=25)
        text(sl, CX + 1.45, y + 0.28, CW - 2.2, 0.58, title, size=30, color=INK)
        text(sl, CX + 1.45, y + 0.94, CW - 2.2, h - 1.24, lines, size=25,
             color=BODY, spacing=1.26, space_after=6)

    block(2.35, "①", "미리듣기가 아낀 시간",
          ["없을 때: 찍기 → 플레이 → 퍼즐 클리어 → 보스전 도달 → 확인 **(2~3분)**",
           "있을 때: 찍기 → **▶ 클릭 (2초).** 채보 튜닝은 수십 번 반복하는 일이라 여기서 시간이 가장 많이 샌다."])
    block(3.45, "②", "그리는 코드는 한 벌, 편집 상태는 호스트마다",
          ["보드 편집은 **인스펙터**와 **전용 창** 두 곳에서 한다(인스펙터는 폭이 좁아 20×20이 잘린다). 그리는 코드를 각자 들면 브러시 하나 늘릴 때 두 곳을 고쳐야 하고 **한쪽은 반드시 잊는다** → `BoardGridGUI` 한 벌.",
           "반대로 편집 상태를 static으로 두면 **둘을 같이 열었을 때 서로 덮어써 드래그가 끊긴다** → 호스트마다 하나씩.",
           {"t": "공유할 것(그리는 방법)과 나눌 것(지금 누가 끌고 있는가)을 가르는 것이 툴 코드의 절반이다.",
            "size": 26, "color": BLUE_D, "space_before": 8}])

    y, h = v.take_rest()
    rect(sl, CX, y, CW, h, fill=SOFT, radius=0.13, line=LINE, lw=0.75)
    badge(sl, CX + 0.55, y + 0.34, 0.68, 0.68, "③", size=25)
    text(sl, CX + 1.45, y + 0.28, CW - 2.2, 0.58, "두 번 돌려도 무해해야 한다", size=30, color=INK)
    text(sl, CX + 1.45, y + 0.94, CW - 2.2, h - 1.24,
         "일괄 교체 툴이 두 번째 실행에서 껍데기를 한 겹 더 씌운 버그를 겪고, **조상 노드 제외 + 이미 처리된 것 건너뛰기** 2중 방어를 넣었다.",
         size=25, color=BODY, spacing=1.26)


def s09_loc(prs):
    sl = chrome(prs, 9)
    v = head(sl, "어필 기술 ④ 현지화", "원천을 한 곳으로 묶으면, 성능 최적화까지 정확해진다.")

    boxes, y = cols3(v, 4.75, 0.36)
    col_card(sl, *boxes[0], "구조", "CSV 한 장이 원천",
             ["구글 시트 → CSV(**139키 × 2언어**). 코드 · 씬에 문자열 하드코딩 **0**.",
              "정적 문구는 `LocalizedText` 바인더 + 키, 동적 문구(HP · 턴)는 `Loc.GetText(key, args)`."])
    col_card(sl, *boxes[1], "함정", "값이 아니라 키를 바꾼다",
             ["동적 문구에 바인더를 붙이면 언어 전환 시 **코드와 컴포넌트가 서로 덮어쓴다.**",
              "같은 칸을 여러 문구가 돌려 쓸 때는 값이 아니라 **키를 갈아 끼운다**(`SetKey`)."])
    col_card(sl, *boxes[2], "성능", "구울 글자를 안다",
             ["화면 글씨의 원천이 CSV 한 장뿐이라 **거기 없는 글자는 게임에도 안 나온다.**",
              "실측 **343자**(한글 274)를 시작 로딩에서 한 번에 구워 런타임 끊김을 없앴다."])

    y, h = v.take_rest()
    gap, gw = 0.44, (CW - 0.88) / 3
    stat(sl, CX, y, gw, h, "139키 × 2언어", "원천 = CSV 한 장")
    stat(sl, CX + gw + gap, y, gw, h, "0", "코드 · 씬 하드코딩 문자열")
    stat(sl, CX + (gw + gap) * 2, y, gw, h, "343자", "시작 로딩에 미리 굽는 글자")


def s10_hero(prs):
    sl = chrome(prs, 10)
    v = head(sl, "아키텍처 결정이 3주 뒤에 값을 한 순간",
             "Core에 UnityEngine이 없어서, 에디터를 켜지 않고 보드 60,000개를 검증할 수 있었다.")

    boxes, y = cols3(v, 5.55, 0.40)
    col_card(sl, *boxes[0], "상황", "불변식이 거짓이었다",
             ["코드 주석은 \"벽과 구멍 이외의 모든 칸이 항상 채워진다\"고 **주장**했고, 비정형 보드 퍼즈를 포함한 **테스트 166개가 통과 중이었다.**",
              "그런데 실기에서 벽 밑에 빈 칸이 남았다."], size_body=25)
    col_card(sl, *boxes[1], "방법", "Unity 없이 돌린다",
             ["`.csproj`에 Core 소스를 넣는 **한 줄** → Unity 없이 콘솔 앱으로 컴파일.",
              "무작위 보드 **60,000개**를 훑어 반례를 수집했다. 같은 방식으로 테스트도 **`dotnet test` 0.3초** 완주."], size_body=25)
    col_card(sl, *boxes[2], "결과", "퍼즈가 전부 잡았다",
             ["이번에 나온 버그 **3개를 전부 퍼즈가 잡았다.**",
              "손으로 짠 테스트 166개는 **하나도 못 잡았다.**"], size_body=25)

    y, h = v.take_rest()
    rect(sl, CX, y, CW, h, fill=DARK, radius=0.13)
    rect(sl, CX, y, 0.17, h, fill=BLUE, shape=MSO_SHAPE.RECTANGLE)
    text(sl, CX + 0.90, y + 0.20, CW - 1.8, h - 0.40,
         [{"t": "**불변식은 주장이지 보장이 아니다.** 테스트가 있다는 사실이 오히려 안심시켰다.",
           "size": 32, "color": WHITE, "em": BLUE_L},
          {"t": "`noEngineReferences` 한 줄이 없었다면 이 반례를 찾을 방법 자체가 없었다.",
           "size": 25, "color": RGBColor(0xC9, 0xC9, 0xC9), "space_before": 8}],
         anchor=MSO_ANCHOR.MIDDLE)


def s11_trouble(prs):
    sl = chrome(prs, 11)
    v = head(sl, "트러블슈팅 대표 3건", "재발하는 버그는 주의가 아니라 코드로 막는다.")

    h = v.rest()
    boxes, y = cols3(v, h, 0.44)
    col_card(sl, *boxes[0], "① 3번 재발", "게이지가 안 줄어든다",
             ["`Image.type`이 `Filled`가 아니면 `fillAmount`가 **아무 일도 안 한다.**",
              "UI 스프라이트를 꽂을 때 Unity가 9슬라이스를 감지해 `Sliced`로 되돌려 놓고 있었다.",
              "→ `Awake`에서 게이지 3종을 강제. **세 번 재발했다는 것은 코드 레벨 방어가 필요했다는 뜻이다.**"],
             size_body=25)
    col_card(sl, *boxes[1], "② 불변식 위반", "벽 밑에 빈 칸",
             ["대각 슬라이드는 **한 칸짜리 이동**이라 출처가 정확히 대각선 위여야 하는데,",
              "그 자리가 벽 · 보드 밖 · 구멍이면 **도달 가능한 타일이 물리적으로 없다**(가장 흔한 경우는 부서진 벽이 남긴 자리).",
              "→ 구멍을 리필 입구까지 **걸어 올라가게** 해 옆 타일로 메운다. **뷰는 한 줄도 안 고쳤다.**"],
             size_body=25)
    col_card(sl, *boxes[2], "③ 확률의 단위", "보스 타일이 도배된다",
             ["확률이 **리필 타일 하나하나에** 굴려지고 있었다. 한 웨이브 10칸이면 주사위를 10번 던진다.",
              "**\"5%\"가 실제로는 `1 − 0.95¹⁰ = 40%`였다.** → 동시 상한 + 웨이브 누적 카운터.",
              "**확률은 \"무엇 당(per)\"인지 같이 적는다.**"],
             size_body=25)


def s12_opt(prs):
    sl = chrome(prs, 12)
    v = head(sl, "최적화 & 런타임 안정성", "설정 하나가 게임의 핵심 손맛을 갉아먹고 있었다.")

    y = v.take(2.05, 0.34)
    rect(sl, CX, y, CW, 2.05, fill=RED_T, radius=0.13)
    rect(sl, CX, y, 0.17, 2.05, fill=RED, shape=MSO_SHAPE.RECTANGLE)
    text(sl, CX + 0.90, y + 0.22, CW - 1.8, 1.61,
         [{"t": "30fps 상한 — 성능만의 문제가 아니었다", "size": 32, "color": RED},
          {"t": "아무도 `Application.targetFrameRate`를 안 정했다. 기본값 −1 = 플랫폼 기본값이고 **모바일에서 그건 30**이다.",
           "size": 25, "color": BODY, "space_before": 8},
          {"t": "한 프레임 33ms는 **Lv0 패링 판정 폭(0.25초)의 13%** — 정확히 눌러도 프레임 경계에 걸려 놓친다. → **60 고정.**",
           "size": 25, "color": BODY, "space_before": 5}],
         anchor=MSO_ANCHOR.MIDDLE, em=RED)

    y = v.take(3.66, 0.32)
    table(sl, y, 3.66, [1.6, 2.0, 3.4], ["항목", "조치", "이유"],
          [["BGM 6곡", "Streaming / Vorbis", "원본 PCM이 통째로 빌드에 들어가던 것 차단"],
           ["단발 효과음", "DecompressOnLoad", "스트리밍하면 **눌린 뒤에** 소리가 난다"],
           ["도트 스프라이트", "Point · 무압축 · 밉맵 끔", "이중선형 필터로 흐려지는 것 방지"]],
          hdr_h=0.76, gap=0.12, size_r=25.5)

    y, h = v.take_rest()
    rect(sl, CX, y, CW, h, fill=SOFT, radius=0.13, line=LINE, lw=0.75)
    text(sl, CX + 0.85, y + 0.18, CW - 1.7, h - 0.36,
         [{"t": "1인 개발에 특히 필요했던 안정성", "size": 29, "color": INK},
          {"t": "**널 세이프 슬롯**(클립 · 스프라이트가 비어도 안 멈춘다)  ·  **도메인 리로드 OFF 대응** `ResetStatics()`  ·  세이브 **v1→v2→v3** 마이그레이션  ·  정적 서비스 19종이 `static class`라 **`DontDestroyOnLoad`가 단 2개**",
           "size": 24.5, "color": BODY, "space_before": 7}],
         anchor=MSO_ANCHOR.MIDDLE)


def s13_future(prs):
    sl = chrome(prs, 13)
    v = head(sl, "Future Work: 정직하게 남은 것", "시스템은 완성했고, 콘텐츠와 검증이 남았다.")

    y = v.take(4.95, 0.30)
    table(sl, y, 4.95, [2.2, 4.8], ["항목", "상태"],
          [["튜토리얼 2종", "설계 · 구현 완료 — 검증 · 영상 녹화 전이라 **현재 비활성화**"],
           ["효과음 3종", "슬롯 배선 완료, 클립 선정만 남음"],
           ["잡몹 밸런스 수치", "시스템 완료, 수치 입력 전 (공용값으로 동작 중)"],
           ["콘텐츠 볼륨", "월드 2개(6판). 시스템은 확장 대비 완료"]],
          hdr_h=0.78, gap=0.12)

    y = v.take(1.70, 0.26)
    rect(sl, CX, y, CW, 1.70, fill=SOFT, radius=0.13, line=LINE, lw=0.75)
    text(sl, CX + 0.85, y + 0.16, CW - 1.7, 1.38,
         [{"t": "남은 것이 적은 이유", "size": 29, "color": INK},
          {"t": "전부 **데이터를 채우는 일**이고 코드를 고칠 일이 아니다. 스테이지 · 캐릭터 · 언어 · 기믹 어느 쪽으로 늘려도 **에셋 1개 또는 상속 1개**로 붙는다.",
           "size": 25, "color": BODY, "space_before": 7}],
         anchor=MSO_ANCHOR.MIDDLE)

    y, h = v.take_rest()
    callout(sl, y, h,
            "튜토리얼이 꺼져 있다는 사실을 **먼저** 말한다 — 데모 중에 들통나는 것보다 낫다.",
            size_q=30)


def s14_retro(prs):
    sl = chrome(prs, 14)
    v = head(sl, "회고: 가장 크게 배운 것 3가지")

    def block(num, title, lines):
        h = 2.55
        y = v.take(h, 0.26)
        rect(sl, CX, y, CW, h, fill=SOFT, radius=0.13, line=LINE, lw=0.75)
        badge(sl, CX + 0.55, y + (h - 0.80) / 2, 0.80, 0.80, num, size=28)
        text(sl, CX + 1.65, y + 0.30, CW - 2.4, 0.58, title, size=31, color=INK)
        text(sl, CX + 1.65, y + 0.96, CW - 2.4, h - 1.26, lines, size=25,
             color=BODY, spacing=1.26, space_after=5)

    block("①", "아키텍처는 약속이 아니라 강제여야 한다",
          ["\"Core에 Unity를 쓰지 말자\"는 지켜지지 않는다. `noEngineReferences: true` 한 줄이 그것을 **컴파일 에러**로 만든 순간부터 규칙이 진짜가 됐다. → 그 덕에 **10장의 반례**를 찾았다."])
    block("②", "같은 숫자를 두 곳에 적으면 반드시 어긋난다",
          ["패링 띠(로직 vs 뷰) · 튜토리얼의 밝은 칸과 누를 수 있는 칸 · 지도의 길 두께와 노드 크기 — 전부 같은 원인이었다. **한 곳에서 파생시키면 어긋날 수가 없다.**"])
    block("③", "기획 의도를 코드가 배신하는 지점을 찾는 것이 진짜 일",
          ["잡몹 위협을 **턴**으로 세었더니 가만히 있는 것이 최적해가 됐다 → 실시간으로 변경.",
           "소울을 **클리어한 판에서 0**으로 했더니 빨리 깬 사람이 손해를 봤다 → 스테이지별 매장량으로."])

    y, h = v.take_rest()
    rect(sl, CX, y, CW, h, fill=DARK, radius=0.13)
    rect(sl, CX, y, 0.17, h, fill=BLUE, shape=MSO_SHAPE.RECTANGLE)
    text(sl, CX + 0.90, y, CW - 1.8, h,
         "돌아가게 만드는 것과 **「의도대로 돌아가게」** 만드는 것은 다른 일이었다.",
         size=35, color=WHITE, anchor=MSO_ANCHOR.MIDDLE, em=BLUE_L)


# ═══════════════════════════════════════════ 실행
KEEP_FONTS = {"Pretendard Light", "Pretendard Bold", "Pretendard ExtraBold",
              "Pretendard SemiBold"}


def wipe_slides(prs):
    lst = prs.slides._sldIdLst
    for sld in list(lst):
        prs.part.drop_rel(sld.get(qn("r:id")))
        lst.remove(sld)


def strip_unused_fonts(prs):
    """AI 템플릿에서 딸려 온 일본어 폰트 3종(13MB)을 걷어낸다. 이 덱은 안 쓴다."""
    lst = prs.part._element.find(qn("p:embeddedFontLst"))
    if lst is None:
        return 0
    n = 0
    for ef in list(lst):
        fnt = ef.find(qn("p:font"))
        if fnt is not None and fnt.get("typeface") in KEEP_FONTS:
            continue
        for child in ef:
            rid = child.get(qn("r:id"))
            if rid:
                try:
                    prs.part.drop_rel(rid)
                except Exception:
                    pass
        lst.remove(ef)
        n += 1
    return n


SLIDES = [s01_cover, s02_game, s03_layers, s04_boundary, s05_combat, s06_puzzle,
          s07_tools, s08_toolrules, s09_loc, s10_hero, s11_trouble, s12_opt,
          s13_future, s14_retro]


def fit_text(prs, floor=21.0, tol=1.02):
    """상자를 넘치는 글자를 줄인다. 'FREE ' 로 표시한 상자(주변이 여백인 큰 글씨)는 건드리지 않는다.
       PowerPoint 의 normAutofit 에 맡기지 않는 이유: 열어 보기 전에는 결과를 알 수 없다."""
    from validate import est_height
    shrunk = []
    for i, s in enumerate(prs.slides, 1):
        for sh in s.shapes:
            if not sh.has_text_frame or sh.name.startswith("FREE "):
                continue
            tf = sh.text_frame
            if not tf.text.strip():
                continue
            w, h = Emu(sh.width).inches, Emu(sh.height).inches
            runs = [r for p in tf.paragraphs for r in p.runs if r.text]
            if not runs:
                continue
            start = max(r.font.size.pt for r in runs)
            scale = 1.0
            while est_height(tf, w) > h * tol and scale > 0.45:
                if max(r.font.size.pt for r in runs) <= floor:
                    break
                scale *= 0.95
                for r in runs:
                    r.font.size = Pt(max(floor, r.font.size.pt * 0.95))
                for p in tf.paragraphs:
                    if p.space_before:
                        p.space_before = Pt(p.space_before.pt * 0.95)
                    if p.space_after:
                        p.space_after = Pt(p.space_after.pt * 0.95)
            if scale < 1.0:
                shrunk.append((i, sh.name, start, max(r.font.size.pt for r in runs)))
    return shrunk


def build(out=TMP, slides=None, verbose=True):
    shutil.copyfile(SRC, BASE)
    prs = Presentation(BASE)
    wipe_slides(prs)
    strip_unused_fonts(prs)
    for fn in (slides or SLIDES):
        fn(prs)
    shrunk = fit_text(prs)
    if verbose and shrunk:
        print(f"자동 축소 {len(shrunk)}개 상자:")
        for i, n, a, b in shrunk:
            print(f"   슬라이드{i:2} {n:22} {a:.0f}pt → {b:.1f}pt")
    prs.save(out)
    return out


if __name__ == "__main__":
    print("built:", build(), f"({len(SLIDES)}장)")

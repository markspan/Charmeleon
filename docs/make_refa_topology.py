"""Generate refa_topology.svg: the EEG input stage of the TMSi reference amplifier.

The drawing follows the EEG channel block diagram in Clots, Mulder & Hoekstra,
"A PC-based physiological measurement and data acquisition system" (the figure
printed there as "Figure 1b"; the captions of 1a and 1b are swapped in print).

Each channel row is drawn by the same function, so all rows stay aligned.
Run:  python3 make_refa_topology.py > refa_topology.svg
"""

# ---- Palette -------------------------------------------------------------
SIGNAL = "#1f6fb2"   # electrode signal path
EAV = "#d9822b"      # common average (E_av) network
GUARD = "#7a7a7a"    # guarding, isolation, ground
INK = "#222222"      # component outlines and text

# ---- Geometry ------------------------------------------------------------
WIDTH, HEIGHT = 990, 640
ROWS = [("IN 1", "OUT 1", 120), ("IN 2", "OUT 2", 230), ("IN n", "OUT n", 360)]
Y_FIRST = ROWS[0][2]
RAIL_PRE_X = 175      # E_av rail to the - inputs of the 50x preamplifiers
RAIL_DIFF_X = 340     # E_av rail to the - inputs of the 1x differential stages
BUS_X = 295           # bus collecting the preamplifier outputs for the average
BUS_TURN_Y = 430      # where the bus turns towards the averaging block
SUM_BOX = (200, 450, 250, 500)   # x0, y0, x1, y1 of the Σ block
EAV_Y = 475           # y of the buffered E_av output
EAV_LOOP_Y = 525      # y of the loop feeding the preamplifier rail
ISO_X = 650           # isolation barrier
GROUND_Y = 590

out = []


def emit(s):
    out.append(s)


def line(x1, y1, x2, y2, color, width=2, dash=None):
    d = f' stroke-dasharray="{dash}"' if dash else ""
    emit(f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{color}" '
         f'stroke-width="{width}"{d}/>')


def polyline(points, color, width=2, dash=None):
    d = f' stroke-dasharray="{dash}"' if dash else ""
    pts = " ".join(f"{x},{y}" for x, y in points)
    emit(f'<polyline points="{pts}" fill="none" stroke="{color}" '
         f'stroke-width="{width}"{d}/>')


def dot(x, y, color):
    emit(f'<circle cx="{x}" cy="{y}" r="3.5" fill="{color}"/>')


def text(x, y, s, size=13, color=INK, anchor="middle", weight="normal"):
    emit(f'<text x="{x}" y="{y}" font-size="{size}" fill="{color}" '
         f'text-anchor="{anchor}" font-weight="{weight}">{s}</text>')


def box(x0, y0, x1, y1, label, size=12):
    emit(f'<rect x="{x0}" y="{y0}" width="{x1 - x0}" height="{y1 - y0}" '
         f'fill="#ffffff" stroke="{INK}" stroke-width="1.8"/>')
    lines = label.split("\n")
    for i, s in enumerate(lines):
        dy = (i - (len(lines) - 1) / 2) * (size + 2)
        text((x0 + x1) / 2, (y0 + y1) / 2 + dy + size / 3, s, size)


def amp(x_base, y_mid, half_h, x_tip, label, inputs=False):
    """Triangle amplifier symbol; optionally marks + (upper) and - (lower) inputs."""
    emit(f'<polygon points="{x_base},{y_mid - half_h} {x_base},{y_mid + half_h} '
         f'{x_tip},{y_mid}" fill="#ffffff" stroke="{INK}" stroke-width="1.8"/>')
    text(x_base + (x_tip - x_base) * 0.42, y_mid + 4, label, 12)
    if inputs:
        text(x_base + 8, y_mid - 11, "+", 12)
        text(x_base + 8, y_mid + 20, "−", 12)


def channel_row(in_label, out_label, y):
    """One EEG channel: guarded input, 50x preamp, 1x differential stage, chain."""
    y_in = y - 15            # + input of the preamplifier
    y_ch = y + 15            # signal level after the differential stage

    # Guarded input lead: shield drawn as dashed lines, driven by a 1x guard buffer.
    text(12, y_in + 4, in_label, 13, anchor="start", weight="bold")
    line(55, y_in, 200, y_in, SIGNAL)
    line(62, y_in - 6, 135, y_in - 6, GUARD, 1.5, "4 3")
    line(62, y_in + 6, 135, y_in + 6, GUARD, 1.5, "4 3")
    dot(150, y_in, SIGNAL)
    polyline([(150, y_in), (150, y_in - 30)], GUARD, 1.5)
    emit(f'<polygon points="150,{y_in - 40} 150,{y_in - 20} 128,{y_in - 30}" '
         f'fill="#ffffff" stroke="{GUARD}" stroke-width="1.5"/>')
    polyline([(128, y_in - 30), (110, y_in - 30), (110, y_in - 6)], GUARD, 1.5)

    # 50x preamplifier; its - input is driven by E_av.
    amp(200, y, 35, 270, "50x", inputs=True)
    line(RAIL_PRE_X, y + 15, 200, y + 15, EAV)
    dot(RAIL_PRE_X, y + 15, EAV)

    # Preamplifier output: tapped for the average and fed to the 1x stage.
    line(270, y, 360, y, SIGNAL)
    dot(BUS_X, y, SIGNAL)

    # 1x differential stage: output = preamp output - E_av.
    amp(360, y_ch, 35, 420, "1x", inputs=True)
    line(RAIL_DIFF_X, y + 30, 360, y + 30, EAV)
    dot(RAIL_DIFF_X, y + 30, EAV)

    # Time constant (tau = 10 s): series capacitor.
    line(420, y_ch, 455, y_ch, SIGNAL)
    line(455, y_ch - 12, 455, y_ch + 12, INK, 2.5)
    line(463, y_ch - 12, 463, y_ch + 12, INK, 2.5)
    line(463, y_ch, 490, y_ch, SIGNAL)

    # 100x amplifier with low-pass, then pulse width modulator.
    amp(490, y_ch, 26, 545, "100x")
    line(545, y_ch, 565, y_ch, SIGNAL)
    box(565, y_ch - 20, 625, y_ch + 20, "MOD")

    # Optical link across the isolation barrier.
    line(625, y_ch, 675, y_ch, SIGNAL, 2, "6 4")
    box(675, y_ch - 20, 735, y_ch + 20, "DEMOD", 11)
    line(735, y_ch, 755, y_ch, SIGNAL)
    box(755, y_ch - 20, 830, y_ch + 20, "LP\n200 Hz", 11)
    line(830, y_ch, 850, y_ch, SIGNAL)
    amp(850, y_ch, 20, 895, "A")
    line(895, y_ch, 925, y_ch, SIGNAL)
    text(930, y_ch + 4, out_label, 13, anchor="start", weight="bold")


def averaging_network():
    """Sum of preamp outputs, buffered to E_av and fed back to both rails."""
    y_last = ROWS[-1][2]
    # Bus collecting every preamplifier output (one line per channel in reality).
    polyline([(BUS_X, Y_FIRST), (BUS_X, BUS_TURN_Y), (225, BUS_TURN_Y),
              (225, SUM_BOX[1] - 6)], SIGNAL, 1.8, "6 4")
    emit(f'<polygon points="219,{SUM_BOX[1] - 8} 231,{SUM_BOX[1] - 8} '
         f'225,{SUM_BOX[1]}" fill="{SIGNAL}"/>')
    text(BUS_X + 8, BUS_TURN_Y + 16, "all preamp outputs", 11, SIGNAL, "start")

    x0, y0, x1, y1 = SUM_BOX
    box(x0, y0, x1, y1, "Σ", 20)
    amp(x1, EAV_Y, 25, 290, "1x")

    # E_av to the differential stages.
    polyline([(290, EAV_Y), (RAIL_DIFF_X, EAV_Y), (RAIL_DIFF_X, Y_FIRST + 30)], EAV)
    # E_av back to the preamplifier - inputs.
    dot(310, EAV_Y, EAV)
    polyline([(310, EAV_Y), (310, EAV_LOOP_Y), (RAIL_PRE_X, EAV_LOOP_Y),
              (RAIL_PRE_X, Y_FIRST + 15)], EAV)
    text(RAIL_DIFF_X + 8, EAV_Y + 4, "E<tspan baseline-shift=\"sub\" font-size=\"10\">av</tspan>",
         14, EAV, "start", "bold")
    text(RAIL_PRE_X - 8, EAV_LOOP_Y + 4, "E<tspan baseline-shift=\"sub\" font-size=\"10\">av</tspan>",
         14, EAV, "end", "bold")


def frame():
    """Headers, ellipsis, isolation barrier, COMMON electrode and legend."""
    # Column headers on two staggered rows so neighbouring labels never collide.
    row_a, row_b = 36, 54
    text(105, row_a, "SIGNAL GUARDING", 11, GUARD)
    text(235, row_a, "PREAMP", 11)
    text(390, row_b, "SUBTRACT E<tspan baseline-shift=\"sub\" font-size=\"8\">av</tspan>", 11)
    text(459, row_a, "TIME CONST.", 11)
    text(459, row_b + 14, "\u03c4 = 10 s", 11)
    text(517, row_b, "100x + LP", 11)
    text(595, row_a, "PWM", 11)
    text(ISO_X, row_b, "ISOLATION (OPTO)", 11, GUARD)
    text(790, row_b, "ANTI-ALIAS", 11)
    text(872, row_a, "GAIN", 11)

    for dy in (-10, 0, 10):                       # channels between 2 and n
        dot(30, 295 + dy, INK)
        dot(605, 310 + dy, INK)

    line(ISO_X, 72, ISO_X, 560, GUARD, 2, "8 5")
    text(ISO_X - 10, 555, "floating, subject side", 11, GUARD, "end")
    text(ISO_X + 10, 555, "output side", 11, GUARD, "start")

    text(872, 432, "50 / 100 / 200 / 500", 11, anchor="middle")
    text(872, 446, "µV/V, selectable", 11, anchor="middle")

    # COMMON electrode to isolated ground.
    text(12, GROUND_Y + 4, "COMMON", 13, anchor="start", weight="bold")
    line(80, GROUND_Y, 140, GROUND_Y, GUARD)
    line(140, GROUND_Y, 140, GROUND_Y + 14, GUARD)
    line(128, GROUND_Y + 14, 152, GROUND_Y + 14, GUARD)
    line(133, GROUND_Y + 19, 147, GROUND_Y + 19, GUARD)
    line(138, GROUND_Y + 24, 142, GROUND_Y + 24, GUARD)
    text(160, GROUND_Y + 4, "isolated amplifier ground", 11, GUARD, "start")

    # Legend.
    lx, ly = 690, 582
    line(lx, ly, lx + 30, ly, SIGNAL)
    text(lx + 38, ly + 4, "electrode signal path", 11, anchor="start")
    line(lx, ly + 18, lx + 30, ly + 18, EAV)
    text(lx + 38, ly + 22, "common average E<tspan baseline-shift=\"sub\" font-size=\"8\">av</tspan>",
         11, anchor="start")
    line(lx, ly + 36, lx + 30, ly + 36, GUARD, 1.5, "4 3")
    text(lx + 38, ly + 40, "guarding / isolation", 11, anchor="start")


def main():
    emit(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {WIDTH} {HEIGHT}" '
         f'width="{WIDTH}" height="{HEIGHT}" font-family="Helvetica, Arial, sans-serif" '
         f'role="img" aria-labelledby="t d">')
    emit('<title id="t">TMSi reference amplifier: common average EEG input stage</title>')
    emit('<desc id="d">Each electrode drives the + input of a 50x preamplifier. The '
         'preamplifier outputs are summed and buffered to form the common average E_av, '
         'which is fed back to the - input of every preamplifier and to the - input of a '
         '1x differential stage per channel, giving E_i - E_av. Each channel then passes a '
         '10 s time constant, a 100x amplifier with low-pass, a pulse width modulator, an '
         'optical isolation barrier, a demodulator, a 200 Hz low-pass filter and an output '
         'amplifier. After Clots, Mulder and Hoekstra.</desc>')
    emit(f'<rect width="{WIDTH}" height="{HEIGHT}" fill="#ffffff"/>')
    frame()
    averaging_network()
    for in_label, out_label, y in ROWS:
        channel_row(in_label, out_label, y)
    emit("</svg>")
    print("\n".join(out))


if __name__ == "__main__":
    main()

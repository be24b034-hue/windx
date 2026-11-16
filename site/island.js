/* WindX island demo: builds the island in HTML and scripts its morphs on one seekable timeline.
   Everything is driven by the Web Animations API so a page can either let it loop (website)
   or seek it frame by frame (the promo reel renderer). No network, no dependencies. */
(function () {
  "use strict";

  const SPRING = "cubic-bezier(.3,1.45,.45,1)";
  const SMOOTH = "cubic-bezier(.2,.8,.2,1)";

  const THEMES = {
    Midnight: { bg: "#0c0c0e", bg2: "#0c0c0e", border: "#1f1f23", text: "#f5f5f7", sub: "#9a9aa3", track: "#2a2a30", chip: "#1c1c20", accent: "#f5f5f7", on: "#0c0c0e" },
    Graphite: { bg: "#1d1d20", bg2: "#26262a", border: "#34343a", text: "#ffffff", sub: "#a1a1aa", track: "#3a3a41", chip: "#2d2d32", accent: "#0a84ff", on: "#ffffff" },
    Aurora:   { bg: "#071a2b", bg2: "#0b3b3c", border: "#15474a", text: "#eafbf7", sub: "#8fb8b5", track: "#174a50", chip: "#0f3238", accent: "#5eead4", on: "#06202a" },
    Sunset:   { bg: "#2a0f2e", bg2: "#541a2c", border: "#63283f", text: "#fff1ec", sub: "#d8a9b0", track: "#5c2944", chip: "#40183a", accent: "#ff9f5a", on: "#2a0f1e" },
    Ocean:    { bg: "#0a1128", bg2: "#11215a", border: "#22347a", text: "#eef2ff", sub: "#9aa6d6", track: "#22336f", chip: "#162455", accent: "#7aa2ff", on: "#0a1128" },
    Sakura:   { bg: "#26131d", bg2: "#3a1a30", border: "#52263f", text: "#ffeff6", sub: "#cba0b6", track: "#4d2842", chip: "#3b1c2f", accent: "#ff7eb6", on: "#2a0f1e" },
    Emerald:  { bg: "#06170f", bg2: "#0d2e1e", border: "#17432e", text: "#ecfdf5", sub: "#8db8a3", track: "#184735", chip: "#0f2f21", accent: "#34d399", on: "#052014" },
    Gold:     { bg: "#14110a", bg2: "#221c0e", border: "#3d3319", text: "#fff8e7", sub: "#bfb08a", track: "#3b321b", chip: "#272111", accent: "#f5c451", on: "#1a1407" },
    Neon:     { bg: "#05050a", bg2: "#05050a", border: "#00c8e0", text: "#e6fbff", sub: "#7aa8b5", track: "#12222b", chip: "#0d1820", accent: "#00e5ff", on: "#00161c", glow: "#00e5ff" },
    Snow:     { bg: "#fdfdfe", bg2: "#efeff4", border: "#d9d9e0", text: "#1c1c1e", sub: "#6e6e76", track: "#d8d8df", chip: "#e7e7ed", accent: "#0a84ff", on: "#ffffff" },
  };

  function themeVars(name) {
    const t = THEMES[name] || THEMES.Midnight;
    return { "--bg": t.bg, "--bg2": t.bg2, "--border": t.border, "--text": t.text, "--sub": t.sub,
             "--track": t.track, "--chip": t.chip, "--accent": t.accent, "--on": t.on, "--glow": t.glow || "#000000" };
  }
  function applyTheme(el, name) {
    const v = themeVars(name);
    for (const k in v) el.style.setProperty(k, v[k]);
  }

  const bars = '<span class="wx-bars">' + "<i></i>".repeat(5) + "</span>";
  const ring = (label, id) =>
    `<div class="wx-ring"><svg width="62" height="62"><circle class="bg" cx="31" cy="31" r="28"/><circle class="fg" data-ring="${id}" cx="31" cy="31" r="28" stroke-dasharray="176" stroke-dashoffset="176"/><text x="31" y="31" data-ringtext="${id}"></text></svg>${label}</div>`;
  const tabs = (on) => `<div class="wx-mark">WindX</div><div class="wx-tabs">${[0, 1, 2].map((i) =>
    `<span class="${i === on ? "on" : ""}"><svg class="wx-ic" width="15" height="15" viewBox="0 0 24 24">${
      ["<path d='M4 11l8-7 8 7v9h-5v-6H9v6H4z'/>", "<rect x='4' y='5' width='16' height='15' rx='3'/><path d='M4 10h16M9 3v4M15 3v4'/>", "<rect x='3' y='4' width='18' height='16' rx='3'/><path d='M7 13h2l2-4 2 7 2-3h2'/>"][i]
    }</svg></span>`).join("")}</div>`;

  function calendar() {
    let h = "SMTWTFS".split("").map((d) => `<span class="h">${d}</span>`).join("");
    for (let d = 1; d <= 30; d++) h += `<span${d === 15 ? ' class="t"' : ""}>${d}</span>`;
    for (let d = 1; d <= 5; d++) h += `<span class="o">${d}</span>`;
    return h;
  }

  const HTML = `
    <div class="wx-layer wx-compact" data-l="compact">
      <span>9:41</span>
      <span class="wx-row"><span style="color:#30d158;font-size:12px">100%</span><span class="wx-batt"></span></span>
    </div>
    <div class="wx-layer wx-music" data-l="music">
      <span class="wx-art" style="width:24px;height:24px"></span>${bars}
    </div>
    <div class="wx-layer wx-vol" data-l="vol">
      <svg class="wx-ic" width="17" height="17" viewBox="0 0 24 24"><path d="M4 9v6h4l5 4V5L8 9z"/><path d="M16.5 8.5a5 5 0 010 7M19 6a8.5 8.5 0 010 12"/></svg>
      <span class="wx-bar"><b data-a="volfill"></b></span><span style="font-size:12px" data-a="voltext">62%</span>
    </div>
    <div class="wx-layer wx-panel wx-media" data-l="media">
      <div class="wx-row" style="gap:14px">
        <span class="wx-art"></span>
        <div style="flex:1"><div class="wx-title">Night Drive</div><div class="wx-artist wx-sub">Aurora Lane</div></div>
        <span style="transform:scale(1.5);margin-right:10px">${bars}</span>
      </div>
      <div class="wx-prog wx-sub"><span>1:12</span><span class="wx-bar"><b data-a="prog"></b></span><span>-2:31</span></div>
      <div class="wx-ctl">
        <svg width="17" height="17" viewBox="0 0 24 24"><path d="M6 5h2v14H6zM20 5v14L9 12z"/></svg>
        <svg width="24" height="24" viewBox="0 0 24 24"><rect x="6" y="5" width="4" height="14" rx="1"/><rect x="14" y="5" width="4" height="14" rx="1"/></svg>
        <svg width="17" height="17" viewBox="0 0 24 24"><path d="M16 5h2v14h-2zM4 5v14l11-7z"/></svg>
      </div>${tabs(0)}
    </div>
    <div class="wx-layer wx-panel wx-day" data-l="day">
      <div class="wx-cal"><h5>November 2026</h5><div class="wx-grid">${calendar()}</div></div>
      <div class="wx-wx">
        <div class="wx-sun"></div><div class="big">28°</div><div class="d wx-sub">Clear · Chennai</div><hr>
        <div class="f"><span class="wx-sub">Mon</span><span>31° 24°</span></div>
        <div class="f"><span class="wx-sub">Tue</span><span>30° 25°</span></div>
        <div class="f"><span class="wx-sub">Wed</span><span>29° 24°</span></div>
      </div>${tabs(1)}
    </div>
    <div class="wx-layer wx-panel wx-sys" data-l="sys">
      <div class="wx-rings">${ring("CPU", "cpu")}${ring("Memory", "mem")}${ring("Disk", "disk")}${ring("Battery", "bat")}</div>
      <div class="wx-line wx-sub">9.8 of 16 GB in use · up 3h 12m · WindX 34 MB</div>${tabs(2)}
    </div>`;

  function create(host, theme) {
    const el = document.createElement("div");
    el.className = "wx-island";
    el.innerHTML = HTML;
    applyTheme(el, theme || "Midnight");
    host.appendChild(el);
    const layers = {};
    el.querySelectorAll("[data-l]").forEach((l) => (layers[l.dataset.l] = l));
    return { el, layers };
  }

  /* ---- Timeline: collect keyframe tracks, then build one WAAPI animation per track ---- */
  class Timeline {
    constructor(total) { this.total = total; this.anims = []; this.pending = []; }
    /** kfs: [[time, {css props}, easing?], ...] */
    track(el, kfs) { if (el && kfs.length) this.pending.push([el, kfs]); return this; }
    build(loop) {
      for (const [el, raw] of this.pending) {
        const kfs = raw.slice().sort((a, b) => a[0] - b[0]);
        if (kfs[0][0] > 0) kfs.unshift([0, kfs[0][1]]);
        if (kfs[kfs.length - 1][0] < this.total) kfs.push([this.total, kfs[kfs.length - 1][1]]);
        const frames = kfs.map(([t, props, ease]) => Object.assign({ offset: Math.min(1, Math.max(0, t / this.total)), easing: ease || SMOOTH }, props));
        this.anims.push(el.animate(frames, { duration: this.total * 1000, fill: "both", iterations: loop ? Infinity : 1 }));
      }
      this.pending = [];
      return this;
    }
    /** Jump every animation to an exact time (seconds). Used to render the reel frame by frame. */
    seek(t) { for (const a of this.anims) { a.pause(); a.currentTime = t * 1000; } }
  }

  function rand(seed) { return () => (seed = (seed * 16807) % 2147483647) / 2147483647; }

  const SIZES = { compact: [170, 36, 18], music: [200, 36, 18], vol: [280, 36, 18], media: [400, 220, 42], day: [400, 292, 42], sys: [400, 188, 42] };

  /** Adds the island's whole show to a timeline. scenes: [[name, startTime], ...] in order; end = when the last scene finishes. */
  function script(tl, isl, scenes, end) {
    const shape = [], vis = {};
    Object.keys(isl.layers).forEach((k) => (vis[k] = []));

    scenes.forEach(([name, at], i) => {
      const [w, h, r] = SIZES[name];
      const next = i + 1 < scenes.length ? scenes[i + 1][1] : end;
      const props = { width: w + "px", height: h + "px", borderRadius: r + "px" };
      if (i === 0) shape.push([at, props, SPRING]);
      else shape.push([at + 0.62, props, SPRING]);
      shape.push([next, props, SPRING]); // hold until the next morph begins

      const hidden = { opacity: 0, transform: "translateX(-50%) scale(.9)" }, shown = { opacity: 1, transform: "translateX(-50%) scale(1)" };
      const v = vis[name];
      if (i === 0) v.push([at, shown]);
      else { v.push([at + 0.1, hidden], [at + 0.45, shown]); }
      v.push([next, shown], [next + 0.1, hidden]);
    });

    tl.track(isl.el, shape);
    for (const k in vis) {
      if (!vis[k].length) { tl.track(isl.layers[k], [[0, { opacity: 0 }]]); continue; }
      if (vis[k][0][0] > 0) vis[k].unshift([0, { opacity: 0, transform: "translateX(-50%) scale(.9)" }]);
      tl.track(isl.layers[k], vis[k]);
    }

    // Visualizer bars dance for the whole show (they are only visible in the music scenes).
    const rnd = rand(7);
    isl.el.querySelectorAll(".wx-bars i").forEach((bar) => {
      const k = [];
      for (let t = 0; t <= tl.total; t += 0.14) k.push([t, { height: (4 + rnd() * 12).toFixed(1) + "px" }, "ease-in-out"]);
      tl.track(bar, k);
    });

    const at = (name) => { const s = scenes.find((x) => x[0] === name); return s ? s[1] : null; };
    const span = (name) => { const i = scenes.findIndex((x) => x[0] === name); return i < 0 ? 0 : (i + 1 < scenes.length ? scenes[i + 1][1] : end) - scenes[i][1]; };

    if (at("vol") != null) {
      const t = at("vol");
      tl.track(isl.el.querySelector('[data-a="volfill"]'), [[t, { width: "34%" }], [t + 0.5, { width: "34%" }], [t + 1.3, { width: "72%" }]]);
    }
    if (at("media") != null) {
      const t = at("media");
      tl.track(isl.el.querySelector('[data-a="prog"]'), [[t, { width: "30%" }, "linear"], [t + span("media"), { width: "44%" }, "linear"]]);
    }
    if (at("sys") != null) {
      const t = at("sys"), values = { cpu: 0.27, mem: 0.61, disk: 0.72, bat: 1 };
      for (const id in values) {
        const full = 176, target = full * (1 - values[id]);
        tl.track(isl.el.querySelector(`[data-ring="${id}"]`), [[t, { strokeDashoffset: full }], [t + 0.45, { strokeDashoffset: full }], [t + 1.5, { strokeDashoffset: target }]]);
        isl.el.querySelector(`[data-ringtext="${id}"]`).textContent = Math.round(values[id] * 100) + "%";
      }
    }
    return tl;
  }

  /** Website: a looping demo. */
  function loop(isl) {
    const scenes = [["compact", 0], ["music", 1.7], ["vol", 3.9], ["media", 5.9], ["day", 9.1], ["sys", 12.3], ["compact", 15.2]];
    const tl = new Timeline(17);
    script(tl, isl, scenes, 17).build(true);
    return tl;
  }

  window.WindXIsland = { THEMES, themeVars, applyTheme, create, Timeline, script, loop, SPRING, SMOOTH };
})();

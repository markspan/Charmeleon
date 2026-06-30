// Electrode layout — populated dynamically from the SSE stream sent by Charmeleon.
// Until the first message arrives the canvas shows an empty head.
let ELEC = [];

const canvas = document.getElementById("c");
const ctx    = canvas.getContext("2d");
const status = document.getElementById("status");
const dpr    = window.devicePixelRatio || 1;

let cssW = 0, cssH = 0;    // canvas size in logical CSS pixels (full viewport)
let state   = {};          // label -> {kOhm, active}

// View transform in CSS pixel space (zoom centred on any point, pan freely)
let vScale = 1, vTx = 0, vTy = 0;

// ---- colour -----------------------------------------------------------------
// COLORMAP is Charmeleon's exact 256-entry table (fetched from /colormap), so the
// web view matches Charmeleon including any custom Resources/heat.map.
let COLORMAP = null;

function impedanceColor(kOhm, active) {
  if (!active) return "#d3d3d3";                 // Color.LightGray, as in Charmeleon
  const v = Math.max(0, Math.min(255, Math.floor(kOhm)));   // (int) truncation, as in Charmeleon
  if (COLORMAP) return COLORMAP[v];
  // Fallback gradient until the colour table has loaded.
  return v === 0 ? "rgba(1,254,0,0)" : `rgb(${v},${255-v},0)`;
}

// ---- draw -------------------------------------------------------------------
// All coordinates are in CSS pixel space; ctx transform handles DPR + pan/zoom.
function draw() {
  const W = cssW, H = cssH;

  ctx.save();
  ctx.clearRect(0, 0, canvas.width, canvas.height);

  // Apply DPR scaling then user zoom/pan.
  // A CSS-space point (cx, cy) maps to physical pixel (cx*vScale+vTx, cy*vScale+vTy)*dpr.
  ctx.setTransform(dpr*vScale, 0, 0, dpr*vScale, vTx*dpr, vTy*dpr);

  const cx = W/2, cy = H/2, full = Math.min(W,H)*0.43, unit = full/5;

  // Guide rings
  ctx.strokeStyle="#808080"; ctx.lineWidth=1/vScale; ctx.setLineDash([4/vScale,4/vScale]);
  [4,3,2,1].forEach(r=>{ctx.beginPath();ctx.arc(cx,cy,r*unit,0,Math.PI*2);ctx.stroke();});
  ctx.setLineDash([]);

  // Head outline
  ctx.strokeStyle="#000"; ctx.lineWidth=2/vScale;
  ctx.beginPath(); ctx.arc(cx,cy,full,0,Math.PI*2); ctx.stroke();

  // Cross-hairs
  ctx.strokeStyle="#808080"; ctx.lineWidth=1/vScale; ctx.setLineDash([2/vScale,4/vScale]);
  ctx.beginPath(); ctx.moveTo(cx-full*1.1,cy); ctx.lineTo(cx+full*1.1,cy); ctx.stroke();
  ctx.beginPath(); ctx.moveTo(cx,cy-full);      ctx.lineTo(cx,cy+full);      ctx.stroke();
  ctx.setLineDash([]);

  // Electrodes
  const rel = unit*0.28;
  ELEC.forEach(e=>{
    // Head-map electrodes use a polar position; AUX (Left/Right/Top/Bottom)
    // use a fractional screen position.
    let ex, ey;
    if (e.angle != null) {
      ex = cx + Math.cos(e.angle)*e.radius*unit;
      ey = cy - Math.sin(e.angle)*e.radius*unit;
    } else {
      // AUX cluster (Left/Right/Top/Bottom): anchor bottom-right and space by
      // electrode size, wider horizontally and tighter vertically, so they do
      // not touch on the (full-screen) web canvas regardless of aspect ratio.
      const acx = 0.88 * W, acy = 0.80 * H;
      ex = acx + (e.fx >= 0.875 ? rel * 2.0 : -rel * 2.0);
      ey = acy + (e.fy >= 0.76  ? rel * 2.0 : -rel * 2.0);
    }
    const s  = state[e.name]||{kOhm:-1, active:false};

    ctx.beginPath(); ctx.arc(ex,ey,rel,0,Math.PI*2);
    ctx.fillStyle = impedanceColor(s.kOhm,s.active); ctx.fill();
    ctx.strokeStyle="#000"; ctx.lineWidth=1/vScale; ctx.stroke();

    const iv  = Math.min(255, Math.floor(s.kOhm));
    const val = s.active?(iv>=255?"Inf":iv.toString()):"";
    ctx.fillStyle = (s.active&&iv>220)?"#fff":"#111";
    ctx.font = `bold ${Math.max(8,rel*0.65)}px "Segoe UI",sans-serif`;
    ctx.textAlign="center"; ctx.textBaseline="middle";
    ctx.fillText(val,ex,ey);

    const lfs  = Math.max(7, rel*0.55);
    const lby  = ey + rel + lfs*0.7;
    ctx.font = `${lfs}px "Segoe UI",sans-serif`;
    const lw = ctx.measureText(e.name).width;
    const pad = 2/vScale;
    ctx.fillStyle = "#f0f0f0";
    ctx.beginPath();
    ctx.roundRect(ex-lw/2-pad, lby-lfs/2-pad, lw+pad*2, lfs+pad*2, 2/vScale);
    ctx.fill();
    ctx.fillStyle = "#111";
    ctx.textAlign = "center"; ctx.textBaseline = "middle";
    ctx.fillText(e.name, ex, lby);
  });

  ctx.restore();
}

// ---- layout -----------------------------------------------------------------
function resize() {
  cssW = window.innerWidth;
  cssH = window.innerHeight;
  canvas.style.width  = cssW + "px";
  canvas.style.height = cssH + "px";
  canvas.width  = Math.round(cssW * dpr);
  canvas.height = Math.round(cssH * dpr);
  resetView();
}
window.addEventListener("resize", resize);

// ---- zoom helpers -----------------------------------------------------------
// All coordinates passed to these functions are in CSS pixels relative to the
// top-left of the canvas element (obtained via getBoundingClientRect).

function canvasPos(clientX, clientY) {
  const r = canvas.getBoundingClientRect();
  return {x: clientX-r.left, y: clientY-r.top};
}

function zoomAt(cx, cy, ds) {            // cx,cy = canvas-relative CSS px
  const ns = Math.max(0.5, Math.min(16, vScale*ds));
  const a  = ns/vScale;
  vTx = cx-(cx-vTx)*a;
  vTy = cy-(cy-vTy)*a;
  vScale = ns;
}

function resetView() { vScale=1; vTx=0; vTy=0; draw(); }

// ---- touch ------------------------------------------------------------------
let pinching=false, dragging=false;
let lastDist=0, lastMp={x:0,y:0}, lastTouch={x:0,y:0}, lastTap=0;

const tdist = (a,b)=>Math.hypot(b.clientX-a.clientX, b.clientY-a.clientY);
const tmid  = (a,b,r)=>({x:(a.clientX+b.clientX)/2-r.left, y:(a.clientY+b.clientY)/2-r.top});

canvas.style.touchAction="none";
canvas.style.cursor="grab";

canvas.addEventListener("touchstart", e=>{
  e.preventDefault();
  const r = canvas.getBoundingClientRect();
  if (e.touches.length===2) {
    pinching=true; dragging=false;
    lastDist=tdist(e.touches[0],e.touches[1]);
    lastMp=tmid(e.touches[0],e.touches[1],r);
  } else if (e.touches.length===1) {
    const now=Date.now();
    if (now-lastTap<280) resetView();
    lastTap=now;
    pinching=false; dragging=true;
    lastTouch=canvasPos(e.touches[0].clientX,e.touches[0].clientY);
  }
},{passive:false});

canvas.addEventListener("touchmove", e=>{
  e.preventDefault();
  const r=canvas.getBoundingClientRect();
  if (pinching&&e.touches.length===2) {
    const nd=tdist(e.touches[0],e.touches[1]);
    const mp=tmid(e.touches[0],e.touches[1],r);
    // pan
    vTx+=mp.x-lastMp.x; vTy+=mp.y-lastMp.y;
    // zoom at new midpoint
    zoomAt(mp.x,mp.y,nd/lastDist);
    lastDist=nd; lastMp=mp;
  } else if (dragging&&e.touches.length===1) {
    const p=canvasPos(e.touches[0].clientX,e.touches[0].clientY);
    vTx+=p.x-lastTouch.x; vTy+=p.y-lastTouch.y;
    lastTouch=p;
  }
  draw();
},{passive:false});

canvas.addEventListener("touchend",e=>{
  if (e.touches.length<2) pinching=false;
  if (e.touches.length===0) dragging=false;
});

// ---- mouse ------------------------------------------------------------------
let mouseDown=false, lastMouse={x:0,y:0};

canvas.addEventListener("mousedown",e=>{
  mouseDown=true; lastMouse=canvasPos(e.clientX,e.clientY);
  canvas.style.cursor="grabbing";
});
window.addEventListener("mousemove",e=>{
  if (!mouseDown) return;
  const p=canvasPos(e.clientX,e.clientY);
  vTx+=p.x-lastMouse.x; vTy+=p.y-lastMouse.y;
  lastMouse=p; draw();
});
window.addEventListener("mouseup",()=>{mouseDown=false;canvas.style.cursor="grab";});

canvas.addEventListener("wheel",e=>{
  e.preventDefault();
  const p=canvasPos(e.clientX,e.clientY);
  zoomAt(p.x,p.y,e.deltaY<0?1.1:0.9);
  draw();
},{passive:false});

canvas.addEventListener("dblclick",resetView);

// ---- SSE --------------------------------------------------------------------
function connect(){
  const es=new EventSource("/stream");
  es.onmessage=ev=>{
    try {
      const d=JSON.parse(ev.data);
      state={};
      (d.electrodes||[]).forEach(e=>{ state[e.label]={kOhm:e.kOhm,active:e.active}; });
      // Rebuild the layout: head-map electrodes carry angle/radius, AUX carry x/y.
      ELEC=(d.electrodes||[]).map(e=>{
        if(e.angle!=null && e.radius!=null) return {name:e.label, angle:e.angle*Math.PI/180, radius:e.radius};
        if(e.x!=null && e.y!=null)          return {name:e.label, fx:e.x, fy:e.y};
        return null;
      }).filter(x=>x);
      draw();
      status.textContent="Live";
    } catch{}
  };
  es.onerror=()=>{status.textContent="Reconnecting...";};
  es.onopen=()=>{status.textContent="Connected";};
}

// ---- wake lock --------------------------------------------------------------
if ("wakeLock" in navigator) {
  const lock=()=>navigator.wakeLock.request("screen").catch(()=>{});
  lock();
  document.addEventListener("visibilitychange",()=>{if(!document.hidden)lock();});
}

// Fetch Charmeleon's colour table once, then redraw with the exact colours.
fetch("/colormap")
  .then(r => r.json())
  .then(a => { COLORMAP = a; draw(); })
  .catch(() => {});

resize();
connect();


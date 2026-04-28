const { useState, useEffect, useRef, useCallback, createContext, useContext } = React;

// ─── Tokens ───────────────────────────────────────────────────────────────────
const C = {
  bg:        'var(--c-bg)',
  surface:   'var(--c-surface)',
  surface2:  'var(--c-surface2)',
  surface3:  'var(--c-surface3)',
  surface4:  'var(--c-surface4)',
  border:    'var(--c-border)',
  borderMid: 'var(--c-borderMid)',
  text:      'var(--c-text)',
  text2:     'var(--c-text2)',
  text3:     'var(--c-text3)',
  amber:     'oklch(72% 0.14 68)',
  amberDim:  'oklch(55% 0.10 68)',
  amberBg:   'var(--c-amberBg)',
  blue:      'oklch(65% 0.14 245)',
  blueBg:    'var(--c-blueBg)',
  rose:      'oklch(65% 0.14 15)',
  roseBg:    'var(--c-roseBg)',
  violet:    'oklch(62% 0.14 285)',
  violetBg:  'var(--c-violetBg)',
  emerald:   'oklch(65% 0.13 160)',
  emeraldBg: 'var(--c-emeraldBg)',
};

// ─── Theme toggler ────────────────────────────────────────────────────────────
const THEME_OPTIONS = [
  { key: 'light',  icon: 'sun',     title: 'Light' },
  { key: 'system', icon: 'monitor', title: 'System' },
  { key: 'dark',   icon: 'moon',    title: 'Dark' },
];

// sun / monitor / moon paths not in PATHS yet — inline them here
const THEME_ICONS = {
  sun:     'M12 17a5 5 0 1 0 0-10 5 5 0 0 0 0 10zM12 1v2M12 21v2M4.22 4.22l1.42 1.42M18.36 18.36l1.42 1.42M1 12h2M21 12h2M4.22 19.78l1.42-1.42M18.36 5.64l1.42-1.42',
  monitor: 'M2 3h20a1 1 0 0 1 1 1v13a1 1 0 0 1-1 1H2a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1zM8 21h8M12 18v3',
  moon:    'M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z',
};

function ThemeIcon({ name, size = 13, color = 'currentColor' }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none"
      stroke={color} strokeWidth={1.75} strokeLinecap="round" strokeLinejoin="round"
      style={{ flexShrink: 0 }}>
      <path d={THEME_ICONS[name]} />
    </svg>
  );
}

function ThemeToggle() {
  const [theme, setTheme] = useState(() => localStorage.getItem('agentRpTheme') || 'system');

  function pick(t) {
    setTheme(t);
    localStorage.setItem('agentRpTheme', t);
    window.__applyTheme(t);
  }

  return (
    <div style={{
      display: 'inline-flex', borderRadius: 8,
      background: 'var(--c-surface3)',
      border: '1px solid var(--c-border)',
      padding: 2, gap: 1,
    }}>
      {THEME_OPTIONS.map(({ key, icon, title }) => {
        const active = theme === key;
        return (
          <button key={key} title={title} onClick={() => pick(key)}
            style={{
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              width: 26, height: 22, borderRadius: 6, border: 'none',
              cursor: 'pointer', transition: 'background 0.15s, color 0.15s',
              background: active ? 'var(--c-surface)' : 'transparent',
              color: active ? 'var(--c-text)' : 'var(--c-text3)',
              boxShadow: active ? '0 1px 3px rgba(0,0,0,0.18)' : 'none',
            }}>
            <ThemeIcon name={icon} size={12} color="currentColor" />
          </button>
        );
      })}
    </div>
  );
}

// ─── Character palette ────────────────────────────────────────────────────────
const CHAR_PAL = {
  B: ['oklch(60% 0.13 68)',  'oklch(48% 0.10 55)'],
  G: ['oklch(60% 0.13 15)',  'oklch(48% 0.10 5)'],
  J: ['oklch(56% 0.13 245)', 'oklch(46% 0.10 255)'],
  T: ['oklch(58% 0.13 160)', 'oklch(46% 0.10 150)'],
  N: ['oklch(56% 0.11 285)', 'oklch(46% 0.09 275)'],
  D: ['oklch(56% 0.11 200)', 'oklch(46% 0.09 210)'],
};
function charColors(name) {
  const k = (name || '?')[0].toUpperCase();
  return CHAR_PAL[k] || ['oklch(52% 0.07 200)', 'oklch(42% 0.05 210)'];
}

// ─── Avatar ───────────────────────────────────────────────────────────────────
function Avatar({ name = '?', size = 28 }) {
  const [from, to] = charColors(name);
  return (
    <div style={{
      width: size, height: size, borderRadius: '50%', flexShrink: 0,
      background: `linear-gradient(135deg, ${from}, ${to})`,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      color: 'rgba(255,255,255,0.88)', fontSize: size * 0.38,
      fontWeight: 600, letterSpacing: '-0.02em', userSelect: 'none',
    }}>
      {name[0].toUpperCase()}
    </div>
  );
}

// ─── Icon (Lucide-style inline SVG) ──────────────────────────────────────────
const PATHS = {
  plus:           'M12 5v14M5 12h14',
  x:              'M18 6L6 18M6 6l12 12',
  'chevron-down': 'M6 9l6 6 6-6',
  'chevron-right':'M9 6l6 6-6 6',
  'chevron-up':   'M18 15l-6-6-6 6',
  'chevron-left': 'M15 18l-6-6 6-6',
  settings:       'M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z',
  sliders:        'M4 6h3M11 6h9M4 12h9M17 12h3M4 18h5M13 18h7M7 4v4M17 10v4M9 16v4',
  edit:           'M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z',
  trash:          'M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2M10 11v6M14 11v6',
  image:          'M19 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V5a2 2 0 0 0-2-2zM8.5 10a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zM21 15l-5-5L5 21',
  user:           'M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2M12 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8z',
  'map-pin':      'M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0zM12 10a2 2 0 1 0 0-4 2 2 0 0 0 0 4z',
  box:            'M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16zM3.27 6.96L12 12.01l8.73-5.05M12 22.08V12',
  clock:          'M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20zM12 6v6l4 2',
  refresh:        'M23 4v6h-6M1 20v-6h6M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15',
  copy:           'M20 9h-9a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h9a2 2 0 0 0 2-2v-9a2 2 0 0 0-2-2zM5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1',
  star:           'M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z',
  eye:            'M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8zM12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z',
  send:           'M22 2L11 13M22 2l-7 20-4-9-9-4 20-7z',
  sparkle:        'M12 3l1.5 4.5L18 9l-4.5 1.5L12 15l-1.5-4.5L6 9l4.5-1.5L12 3zM5 17l.75 2.25L8 20l-2.25.75L5 23l-.75-2.25L2 20l2.25-.75L5 17z',
  check:          'M20 6L9 17l-5-5',
  download:       'M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3',
  upload:         'M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M17 8l-5-5-5 5M12 3v12',
  'message-sq':   'M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z',
  branch:         'M6 3v12M18 9a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM6 21a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM18 9a9 9 0 0 1-9 9',
  'more-h':       'M12 13a1 1 0 1 0 0-2 1 1 0 0 0 0 2zM19 13a1 1 0 1 0 0-2 1 1 0 0 0 0 2zM5 13a1 1 0 1 0 0-2 1 1 0 0 0 0 2z',
  zap:            'M13 2L3 14h9l-1 8 10-12h-9l1-8z',
  layers:         'M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5',
  'git-branch':   'M6 3v12M6 21a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM6 9a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM18 21a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM6 15a6 6 0 0 0 6 6h6',
  'align-left':   'M17 10H3M21 6H3M21 14H3M17 18H3',
  camera:         'M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2zM12 17a4 4 0 1 0 0-8 4 4 0 0 0 0 8z',
  'filter':       'M22 3H2l8 9.46V19l4 2V12.46L22 3z',
  search:         'M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16zM21 21l-4.35-4.35',
  'book-open':    'M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2zM22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z',
  flag:           'M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1zM4 22v-7',
  users:          'M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2M9 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8zM23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75',
  'pin':          'M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z',
  'shuffle':      'M16 3l4 4-4 4M8 21l-4-4 4-4M20 7H9.5a5.5 5.5 0 0 0 0 11H10M4 7h7',
  'feather':      'M20.24 12.24a6 6 0 0 0-8.49-8.49L5 10.5V19h8.5zM16 8L2 22M17.5 15H9',
  'wrench':       'M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z',
};

function Icon({ name, size = 14, color = 'currentColor', style: extraStyle }) {
  const d = PATHS[name];
  if (!d) return <span style={{ display: 'inline-block', width: size, height: size }} />;
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none"
      stroke={color} strokeWidth={1.75} strokeLinecap="round" strokeLinejoin="round"
      style={{ flexShrink: 0, ...extraStyle }}>
      <path d={d} />
    </svg>
  );
}

// ─── Button ───────────────────────────────────────────────────────────────────
function Btn({ children, variant = 'ghost', sz = 'sm', onClick, active, disabled, style: extra, title, as: Tag = 'button' }) {
  const [hov, setHov] = useState(false);
  const V = {
    ghost:   { bg: hov||active ? 'rgba(255,255,255,0.07)' : 'transparent', color: hov||active ? C.text : C.text2, border: 'none' },
    primary: { bg: hov ? 'oklch(74% 0.14 68)' : C.amber, color: '#0d0b09', border: 'none' },
    secondary:{ bg: hov ? C.surface4 : C.surface3, color: C.text, border: `1px solid ${C.border}` },
    blue:    { bg: hov ? 'oklch(67% 0.14 245)' : C.blue, color: 'white', border: 'none' },
    rose:    { bg: hov ? 'oklch(67% 0.14 15)' : C.rose, color: 'white', border: 'none' },
    outline: { bg: hov ? C.surface3 : 'transparent', color: C.text2, border: `1px solid ${C.border}` },
    danger:  { bg: hov ? 'oklch(28% 0.08 15)' : 'transparent', color: hov ? C.rose : C.text3, border: 'none' },
  };
  const S = {
    xs:   { padding: '2px 6px',  fontSize: 11, borderRadius: 5, gap: 3 },
    sm:   { padding: '4px 9px',  fontSize: 12, borderRadius: 6, gap: 4 },
    md:   { padding: '6px 13px', fontSize: 13, borderRadius: 7, gap: 5 },
    lg:   { padding: '8px 18px', fontSize: 14, borderRadius: 8, gap: 6 },
    icon: { padding: '5px',      fontSize: 12, borderRadius: 6, gap: 4 },
  };
  const v = V[variant] || V.ghost;
  const s = S[sz] || S.sm;
  return (
    <Tag onClick={onClick} disabled={disabled} title={title}
      onMouseEnter={() => setHov(true)} onMouseLeave={() => setHov(false)}
      style={{
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        background: v.bg, color: v.color, border: v.border,
        cursor: disabled ? 'not-allowed' : 'pointer',
        opacity: disabled ? 0.4 : 1,
        transition: 'background 0.12s, color 0.12s',
        fontFamily: 'inherit', fontWeight: 500, whiteSpace: 'nowrap',
        ...s, ...extra,
      }}>
      {children}
    </Tag>
  );
}

// ─── Modal shell ──────────────────────────────────────────────────────────────
function Modal({ children, onClose, maxW = 960 }) {
  useEffect(() => {
    const h = e => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [onClose]);
  return (
    <div onClick={e => e.target === e.currentTarget && onClose()}
      style={{
        position: 'fixed', inset: 0, zIndex: 1000,
        background: 'rgba(0,0,0,0.72)', backdropFilter: 'blur(6px)',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        padding: 20,
      }}>
      <div style={{
        background: C.surface, border: `1px solid ${C.borderMid}`,
        borderRadius: 14, width: '100%', maxWidth: maxW,
        maxHeight: 'calc(100vh - 40px)',
        display: 'flex', flexDirection: 'column',
        boxShadow: '0 32px 100px rgba(0,0,0,0.7)',
        overflow: 'hidden',
      }}>
        {children}
      </div>
    </div>
  );
}

function ModalHeader({ title, subtitle, onClose, children }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      padding: '15px 20px', borderBottom: `1px solid ${C.border}`, flexShrink: 0,
    }}>
      <div style={{ display: 'flex', alignItems: 'baseline', gap: 10 }}>
        <span style={{ fontSize: 15, fontWeight: 600 }}>{title}</span>
        {subtitle && <span style={{ fontSize: 12, color: C.text3 }}>{subtitle}</span>}
        {children}
      </div>
      <Btn variant="ghost" sz="icon" onClick={onClose}><Icon name="x" size={15} /></Btn>
    </div>
  );
}

// ─── Field ────────────────────────────────────────────────────────────────────
function Field({ label, hint, children, onAI }) {
  return (
    <div style={{ marginBottom: 18 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 5 }}>
        <span style={{ fontSize: 11.5, fontWeight: 600, color: C.text2, letterSpacing: '0.02em' }}>{label}</span>
        {onAI && (
          <Btn variant="ghost" sz="xs" onClick={onAI} style={{ color: C.violet, gap: 3 }}>
            <Icon name="sparkle" size={11} color={C.violet} />AI
          </Btn>
        )}
      </div>
      {children}
      {hint && <div style={{ fontSize: 10.5, color: C.text3, marginTop: 4 }}>{hint}</div>}
    </div>
  );
}

function FInput({ value, onChange, placeholder }) {
  const [f, setF] = useState(false);
  return (
    <input value={value} onChange={e => onChange(e.target.value)} placeholder={placeholder}
      onFocus={() => setF(true)} onBlur={() => setF(false)}
      style={{
        width: '100%', background: C.surface3,
        border: `1px solid ${f ? C.amberDim : C.border}`,
        borderRadius: 7, padding: '7px 10px', color: C.text,
        fontSize: 13, outline: 'none', transition: 'border-color 0.15s',
      }} />
  );
}

function FTextarea({ value, onChange, placeholder, rows = 3 }) {
  const [f, setF] = useState(false);
  return (
    <textarea value={value} onChange={e => onChange(e.target.value)} placeholder={placeholder}
      rows={rows} onFocus={() => setF(true)} onBlur={() => setF(false)}
      style={{
        width: '100%', background: C.surface3,
        border: `1px solid ${f ? C.amberDim : C.border}`,
        borderRadius: 7, padding: '8px 10px', color: C.text,
        fontSize: 12.5, lineHeight: 1.6, outline: 'none',
        transition: 'border-color 0.15s',
      }} />
  );
}

function FSelect({ value, onChange, options }) {
  return (
    <select value={value} onChange={e => onChange(e.target.value)}
      style={{
        background: C.surface3, border: `1px solid ${C.border}`,
        borderRadius: 7, padding: '6px 10px', color: C.text,
        fontSize: 12.5, outline: 'none', width: '100%',
        appearance: 'none', cursor: 'pointer',
      }}>
      {options.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
    </select>
  );
}

// ─── Divider ─────────────────────────────────────────────────────────────────
function Divider({ margin = '4px 0' }) {
  return <div style={{ height: 1, background: C.border, margin }} />;
}

// ─── Tag ─────────────────────────────────────────────────────────────────────
function Tag({ children, color }) {
  const c = color || C.text3;
  return (
    <span style={{
      fontSize: 10, fontWeight: 700, padding: '1px 6px', borderRadius: 4,
      background: `${c}25`, color: c, letterSpacing: '0.06em', textTransform: 'uppercase',
    }}>{children}</span>
  );
}

// ─── Mock data ────────────────────────────────────────────────────────────────
const CHARS = [
  { id:'c1', name:'Bella',  inScene:true,  summary:"A warm, accomplished surgeon who brings steadiness and affection to the tense dynamic.",  personality:"Sweet, emotionally intelligent, remarkably steady under pressure. Balances clinical composure with genuine warmth.", appearance:"Short brunette around 5'4\". Approachable presence, quietly confident, polished air softened by warmth.", relationships:"- Jake: boyfriend of three years; deeply affectionate\n- Gemma: best friend since college freshman year", backstory:"Completed her surgical residency last year. Moved in with Jake six months ago.", voice:"Measured and warm. Asks before telling. Touches people she cares about.", notes:"" },
  { id:'c2', name:'Gemma',  inScene:true,  summary:"A striking, sharp-mouthed woman who masks deep vulnerability with bold confidence.", personality:"Sharp-tongued, quick-witted, magnetic. Uses humor and provocation as defense mechanisms. Fiercely loyal once trust is earned.", appearance:"Tall blonde, model's posture. Wardrobe that never lets anyone forget she knows it.", relationships:"- Bella: best friend she trusts more than she admits\n- Jake: complicated history, unresolved tension", backstory:"Works in brand consulting. Has lived in the Devonshire with Jake for two years.", voice:"Fast, dry, a bit cutting. Switches to soft when caught off guard.", notes:"" },
  { id:'c3', name:'Jake',   inScene:true,  summary:"Gemma's polite, work-from-home roommate with a quiet intensity.", personality:"Reserved and thoughtful. Observes more than he speaks. Dry wit surfaces rarely. Internally more complicated than he presents.", appearance:"6'1\", athletic. Usually in casual clothes. Clean-cut with a slightly guarded expression.", relationships:"- Bella: girlfriend of three years\n- Gemma: roommate with unresolved tension", backstory:"Remote software architect. Moved to the Devonshire two years ago. Met Gemma through mutual friends.", voice:"Measured, few words. When he does speak it lands.", notes:"" },
  { id:'c4', name:'Tyler',  inScene:false, summary:"Gemma's on-again-off-again friend who drifts in and out of the scene.", personality:"Easygoing and charming on the surface, harder to read underneath.", appearance:"Broad-shouldered, sandy hair. Always looks like he just got back from somewhere interesting.", relationships:"- Gemma: complicated friendship\n- Jake: casual acquaintance", backstory:"", voice:"", notes:"" },
];

const LOCS = [
  { id:'l1', name:'Devonshire Apartment 822', isActive:true, summary:"Main gathering space. Well-appointed open-plan apartment in the Devonshire building.", description:"Open-plan living space with modern furnishings and floor-to-ceiling windows.", atmosphere:"Charged and comfortable. Claustrophobic when tensions run high.", features:"- Open living area with dining table\n- Kitchen with island\n- Two bedrooms (Jake, Gemma)\n- Balcony" },
  { id:'l2', name:'City Park',                isActive:false, summary:"The park across from the Devonshire. Open, neutral ground.", description:"A leafy urban park with benches and winding paths.", atmosphere:"Neutral, open, relieving.", features:"- Benches\n- Winding pathway\n- Trees providing shade" },
];

const ITEMS = [
  { id:'i1', name:'Tesla Model S Plaid', inScene:false, summary:"Jake's car. Silver, sleek, impractical for the city.", description:"A modern silver electric sedan. Clean interior, dark tints.", history:"Jake bought it two years ago. Gemma borrowed it once and still owes him the charging fee.", properties:"Color: Silver\nModel: Tesla Model S Plaid\nLocation: Street outside Devonshire" },
];

const TIMELINE = [
  { id:'t1', title:'Bella arrives at the apartment', date:'Apr 26, 2026', description:'Bella lets herself in and finds Jake and Gemma in a tense standoff at the dining table.', characters:['Bella','Jake','Gemma'], significance:'Opens Act 1 of the Devonshire Games arc.' },
  { id:'t2', title:'Jake and Gemma move in together', date:'Two years ago', description:'Jake and Gemma become roommates at the Devonshire through mutual friends.', characters:['Jake','Gemma'], significance:'Establishes the baseline tension.' },
];

const GALLERY_IMAGES = [
  { id:'g1', name:'Devonshire Apt 822',  entity:'Devonshire Apartment 822', entityType:'location',  date:'Apr 25',  hue:210 },
  { id:'g2', name:'Gemma',               entity:'Gemma',                    entityType:'character', date:'Apr 25',  hue:15  },
  { id:'g3', name:'Tyler',               entity:'Tyler',                    entityType:'character', date:'Apr 24',  hue:160 },
  { id:'g4', name:'Jake (desk)',          entity:'Jake',                     entityType:'character', date:'Apr 24',  hue:245 },
  { id:'g5', name:'Jake (standing)',      entity:'Jake',                     entityType:'character', date:'Apr 24',  hue:245 },
  { id:'g6', name:'Gemma (glam)',         entity:'Gemma',                    entityType:'character', date:'Apr 24',  hue:15  },
  { id:'g7', name:'Bella',               entity:'Bella',                    entityType:'character', date:'Apr 24',  hue:68  },
  { id:'g8', name:'Tesla Model S Plaid', entity:'Tesla Model S Plaid',      entityType:'item',      date:'Apr 25',  hue:200 },
];

const PROCESS_STEPS_P1 = [
  {
    id: 'appearance', label: 'Appearance', icon: 'eye',
    tokensIn: 1840, tokensOut: 312, totalTokens: 2152, dur: '1.1s',
    systemPrompt: `You are a precise scene-state tracker for a collaborative fiction tool. Your job is to maintain an accurate, present-tense description of every character's physical position, posture, expression, clothing, and proximity to others. Be specific and concrete. Do not interpret subtext — only record observable facts.`,
    userPrompt: `Current scene: Devonshire Apartment 822.\nCharacters present: Bella, Jake, Gemma.\n\nPrevious appearance state:\n(none — scene start)\n\nNarrator wrote:\n"Bella knocks once and lets herself into the apartment's open living space, her gaze sweeping the charged silence at the table where Jake sits rigidly in sweats and hoodie, arms crossed in obstinate simmer, while Gemma lounges opposite in a loose crop top and miniskirt hiked daringly high…"\n\nUpdate the appearance state for all characters.`,
    output: `BELLA: Standing just inside the apartment entrance. Coat still on. Gaze moving across the room toward the table. Expression: composed with a slight cautious smile.\n\nJAKE: Seated at the dining table. Posture rigid, arms crossed. Wearing grey sweatshirt and sweatpants. Expression: tense, guarded, looking toward the door.\n\nGEMMA: Seated across the table from Jake. Posture relaxed, almost exaggeratedly so. Wearing a loose crop top and a miniskirt hiked high. Expression: deadpan, chin level, gaze flat.`,
  },
  {
    id: 'responder', label: 'Responder', icon: 'users',
    tokensIn: 2210, tokensOut: 88, totalTokens: 2298, dur: '0.7s',
    systemPrompt: `You determine which character should respond next in a collaborative fiction scene. Consider dramatic momentum, who has been addressed, who has unspoken motivation, and whose silence would be most conspicuous. Output only the character name and a one-sentence reason.`,
    userPrompt: `Scene: Devonshire Apartment 822.\nPresent: Bella, Jake, Gemma.\nLast turn: Narrator (Guided AI)\nTurn shape requested: Brief\n\nWho should respond next?`,
    output: `Responder: Bella\nReason: She has just entered the scene and her gaze has already registered the tension — she is the active arrival and the natural next voice.`,
  },
  {
    id: 'planning', label: 'Planning', icon: 'sliders',
    tokensIn: 3104, tokensOut: 441, totalTokens: 3545, dur: '1.4s',
    systemPrompt: `You are a dramaturgical planner for a collaborative fiction session. Given the scene state, character profiles, and recent turns, produce a structured plan for the next character's response. Include: narrative beat, intent, immediate goal, why now, and what change this turn introduces. Do not write the prose itself.`,
    userPrompt: `Character: Bella (warm, steady, emotionally intelligent surgeon)\nScene: Devonshire Apt 822 — she has just entered to find Jake and Gemma in visible tension.\nTurn shape: Brief\nPrior turns: Narrator set the scene.\n\nProduce a turn plan.`,
    output: `BEAT: Arrival and de-escalation attempt.\nINTENT: Bella wants to acknowledge both people simultaneously and signal that her presence is neutral, warm, and grounding — not an intrusion or a judgment.\nIMMEDIATE GOAL: Physical contact with both to claim her place in the room without choosing sides.\nWHY NOW: She has read the room instantly. Waiting would let the tension calcify.\nCHANGE INTRODUCED: The triangle is now complete. The dynamic shifts from a two-person standoff to a three-way scene with Bella as a new variable.\nGUARDRAILS: Do not have Bella ask what's wrong. Do not have her look at Jake first. The touch should feel natural, not pointed.`,
  },
  {
    id: 'writing', label: 'Writing', icon: 'edit',
    tokensIn: 4820, tokensOut: 67, totalTokens: 4887, dur: '0.8s',
    systemPrompt: `You are a skilled prose writer for a collaborative fiction tool, writing in the style of contemporary literary fiction. Write in third-person limited from the perspective of the active character. Be economical — a Brief turn is 1–3 sentences of action and dialogue. Use italics for action beats (*like this*). Stay tightly in character voice.`,
    userPrompt: `Character: Bella\nTurn shape: Brief\nPlan: Cross to the table, touch both Jake and Gemma, speak a warm greeting that claims no sides.\nAppearance state: Bella standing at entrance; Jake rigid at table; Gemma lounging opposite.\n\nWrite the turn.`,
    output: `Bella crosses to the table, rests a gentle hand on Jake's shoulder then Gemma's bare midriff. "Hey, you two… missed you both." *smiles warmly*`,
  },
];

const PROCESS_STEPS_P2 = [
  {
    id: 'appearance', label: 'Appearance', icon: 'eye',
    tokensIn: 2104, tokensOut: 298, totalTokens: 2402, dur: '0.9s',
    systemPrompt: `You are a precise scene-state tracker for a collaborative fiction tool. Maintain an accurate, present-tense description of every character's physical position, posture, expression, clothing, and proximity to others.`,
    userPrompt: `Previous state: Bella at entrance. Jake rigid at table. Gemma lounging.\n\nBella wrote: "Bella crosses to the table, rests a gentle hand on Jake's shoulder then Gemma's bare midriff."\n\nUpdate appearance state.`,
    output: `BELLA: Standing at the dining table between Jake and Gemma. Right hand on Jake's shoulder, left hand on Gemma's midriff. Expression: warm, open smile. Coat still on.\n\nJAKE: Seated. Bella's hand on his shoulder. Posture still tense but slightly less rigid. Eyes on Bella.\n\nGEMMA: Seated. Bella's hand on her bare midriff. Expression unchanged — deadpan — but gaze has shifted to Bella.`,
  },
  {
    id: 'responder', label: 'Responder', icon: 'users',
    tokensIn: 2380, tokensOut: 74, totalTokens: 2454, dur: '0.5s',
    systemPrompt: `You determine which character should respond next in a collaborative fiction scene. Output only the character name and a one-sentence reason.`,
    userPrompt: `Last turn: Bella (Automatic AI) — touched both Jake and Gemma, spoke a greeting.\nWho responds next?`,
    output: `Responder: Gemma\nReason: Bella's touch landed on her last and the greeting hangs in the air — Gemma's response (or pointed non-response) carries the most dramatic weight.`,
  },
  {
    id: 'planning', label: 'Planning', icon: 'sliders',
    tokensIn: 3340, tokensOut: 388, totalTokens: 3728, dur: '1.2s',
    systemPrompt: `You are a dramaturgical planner for a collaborative fiction session. Produce a structured plan for the next character's response.`,
    userPrompt: `Character: Gemma (sharp, guarded, uses wit as armor)\nBella has just arrived and touched Gemma warmly. Jake is watching.\nTurn shape: Brief`,
    output: `BEAT: Deflection through warmth — Gemma returns Bella's greeting but uses it as a vector to land a barb at Jake.\nINTENT: Accept Bella's affection without appearing vulnerable. Use the moment to score a point.\nIMMEDIATE GOAL: Respond to Bella; make Jake register he is being excluded from her warmth.\nWHY NOW: The opening is right there — Bella said "missed you both" and Gemma can split that cleanly.\nCHANGE INTRODUCED: Jake is now the odd one out in the room he shares with Gemma.\nGUARDRAILS: Don't make Gemma cruel — the barb should have plausible deniability. Keep the smile.`,
  },
  {
    id: 'writing', label: 'Writing', icon: 'edit',
    tokensIn: 4210, tokensOut: 59, totalTokens: 4269, dur: '0.6s',
    systemPrompt: `You are a skilled prose writer for a collaborative fiction tool, writing in contemporary literary fiction style. Brief turn: 1–3 sentences. Use italics for action beats.`,
    userPrompt: `Character: Gemma\nPlan: Return Bella's greeting warmly, land a quiet dig at Jake.\nAppearance: Gemma seated, Bella's hand on her midriff, Jake watching.\n\nWrite the turn.`,
    output: `Gemma tips her chin up toward Bella without turning, a slow half-smile pulling at the corner of her mouth. "Missed you too, Bell." *her gaze slides briefly to Jake* "Glad someone did."`,
  },
];

const MESSAGES = [
  { id:'p1', type:'process', summary:'Guided AI · Narrator · Appearance → Responder → Planning → Writing', status:'completed', dur:'4.0s', ts:'yesterday', steps: PROCESS_STEPS_P1 },
  { id:'n1', type:'narrative', author:'Narrator', mode:'Guided AI',   ts:'yesterday',
    body:"Bella knocks once and lets herself into the apartment's open living space, her gaze sweeping the charged silence at the table where Jake sits rigidly in sweats and hoodie, arms crossed in obstinate simmer, while Gemma lounges opposite in a loose crop top and miniskirt hiked daringly high, her deadpan expression a mask of defiant nonchalance; the roommates' mismatched energies hang thick, postures locked in pre-arrival friction, as Bella's warm eyes linger a beat too long on Gemma's revealing hem, a subtle thrill flickering beneath her composed smile.",
    branch: '5/5',
  },
  { id:'a1', type:'appearance', summary:'Bella has entered the living space and stands at the table; Jake sits rigidly with arms crossed; Gemma lounges opposite.', chars:3, ts:'yesterday' },
  { id:'p2', type:'process', summary:'Automatic AI · Bella · Appearance → Responder → Planning → Writing', status:'completed', dur:'4.0s', ts:'yesterday', steps: PROCESS_STEPS_P1 },
  { id:'n2', type:'narrative', author:'Bella',   mode:'Automatic AI', ts:'yesterday',
    body:'Bella crosses to the table, rests a gentle hand on Jake\'s shoulder then Gemma\'s bare midriff. "Hey, you two… missed you both." *smiles warmly*',
  },
  { id:'a2', type:'appearance', summary:'Bella stands at the table with hands resting on Jake\'s shoulder and Gemma\'s bare midriff; Jake and Gemma are seated opposite each other.', chars:3, ts:'yesterday' },
  { id:'p3', type:'process', summary:'Automatic AI · Gemma · Appearance → Responder → Planning → Writing', status:'completed', dur:'3.3s', ts:'yesterday', steps: PROCESS_STEPS_P2 },
  { id:'n3', type:'narrative', author:'Gemma',   mode:'Automatic AI', ts:'yesterday',
    body:'Gemma tips her chin up toward Bella without turning, a slow half-smile pulling at the corner of her mouth. "Missed you too, Bell." *her gaze slides briefly to Jake* "Glad someone did."',
  },
];

const CHATS = [
  { id:'ch1', title:'Devonshire Games',    updated:'Apr 26', starred:true,  messages:4,  location:'Devonshire Apartment 822' },
  { id:'ch2', title:'Park Encounter',      updated:'Apr 23', starred:false, messages:8,  location:'City Park' },
  { id:'ch3', title:'Morning After',       updated:'Apr 21', starred:true,  messages:12, location:'Devonshire Apartment 822' },
  { id:'ch4', title:'The Drive',           updated:'Apr 19', starred:false, messages:3,  location:'Tesla Model S Plaid' },
];

// ─── Confirm Delete Dialog ────────────────────────────────────────────────────
function ConfirmDeleteDialog({ title, body, onConfirm, onCancel }) {
  useEffect(() => {
    const h = e => e.key === 'Escape' && onCancel();
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [onCancel]);

  return (
    <div onClick={e => e.target === e.currentTarget && onCancel()}
      style={{
        position: 'fixed', inset: 0, zIndex: 2000,
        background: 'rgba(0,0,0,0.55)', backdropFilter: 'blur(3px)',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        padding: 20,
      }}>
      <div style={{
        background: C.surface, border: `1px solid ${C.borderMid}`,
        borderRadius: 12, width: '100%', maxWidth: 380,
        padding: '22px 22px 18px',
        boxShadow: '0 28px 80px rgba(0,0,0,0.65)',
        display: 'flex', flexDirection: 'column', gap: 12,
        animation: 'cdConfirmIn 0.15s ease',
      }}>
        <style>{`@keyframes cdConfirmIn { from { opacity:0; transform:scale(0.95) translateY(6px); } to { opacity:1; transform:none; } }`}</style>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <div style={{
            width: 36, height: 36, borderRadius: 9, flexShrink: 0,
            background: `${C.rose}22`,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
          }}>
            <Icon name="trash" size={16} color={C.rose} />
          </div>
          <span style={{ fontSize: 14, fontWeight: 600, color: C.text, lineHeight: 1.35 }}>{title}</span>
        </div>
        {body && (
          <p style={{ fontSize: 12.5, color: C.text2, lineHeight: 1.65, margin: 0, paddingLeft: 48 }}>{body}</p>
        )}
        <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 4 }}>
          <Btn variant="secondary" sz="sm" onClick={onCancel}>Cancel</Btn>
          <Btn variant="rose" sz="sm" onClick={onConfirm}>
            <Icon name="trash" size={11} color="white" />Delete
          </Btn>
        </div>
      </div>
    </div>
  );
}

function useConfirmDelete() {
  const [pending, setPending] = useState(null);

  function confirmDelete({ title, body, onConfirm }) {
    setPending({ title, body, onConfirm });
  }

  function close() { setPending(null); }

  const dialog = pending ? (
    <ConfirmDeleteDialog
      title={pending.title}
      body={pending.body}
      onConfirm={() => { pending.onConfirm(); close(); }}
      onCancel={close}
    />
  ) : null;

  return { confirmDelete, dialog };
}

// Expose globals
Object.assign(window, {
  C, Avatar, Icon, Btn, Modal, ModalHeader, ThemeToggle,
  Field, FInput, FTextarea, FSelect, Divider, Tag,
  CHARS, LOCS, ITEMS, TIMELINE, GALLERY_IMAGES, MESSAGES, CHATS,
  charColors,
  ConfirmDeleteDialog, useConfirmDelete,
});

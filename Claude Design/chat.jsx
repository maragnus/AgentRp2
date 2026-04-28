// chat.jsx

function ProcessStepDetail({ step, open }) {
  const monoStyle = {
    fontFamily: "'DM Mono', monospace",
    fontSize: 11.5,
    lineHeight: 1.65,
    color: C.text2,
    whiteSpace: 'pre-wrap',
    wordBreak: 'break-word',
    margin: 0,
  };
  const labelStyle = {
    fontSize: 10,
    fontWeight: 700,
    letterSpacing: '0.08em',
    textTransform: 'uppercase',
    color: C.text3,
    marginBottom: 6,
    display: 'block',
  };
  const blockStyle = {
    background: C.surface,
    border: `1px solid ${C.border}`,
    borderRadius: 7,
    padding: '10px 12px',
    marginBottom: 8,
  };

  if (!open) return null;
  return (
    <div style={{ padding: '0 12px 12px 12px', borderTop: `1px solid ${C.border}`, marginTop: 0 }}>
      <div style={{ marginTop: 10 }}>
        <div style={blockStyle}>
          <span style={labelStyle}>System Prompt</span>
          <pre style={monoStyle}>{step.systemPrompt}</pre>
        </div>
        <div style={blockStyle}>
          <span style={labelStyle}>User Prompt</span>
          <pre style={monoStyle}>{step.userPrompt}</pre>
        </div>
        <div style={{ ...blockStyle, marginBottom: 0, borderColor: C.borderMid }}>
          <span style={{ ...labelStyle, color: C.emerald }}>Output</span>
          <pre style={{ ...monoStyle, color: C.text }}>{step.output}</pre>
        </div>
      </div>
    </div>
  );
}

function ProcessStep({ step }) {
  const [open, setOpen] = useState(false);

  const iconColors = {
    appearance: C.violet,
    responder:  C.blue,
    planning:   C.amber,
    writing:    C.emerald,
  };
  const accentColor = iconColors[step.id] || C.text3;

  return (
    <div style={{
      borderRadius: 7,
      border: `1px solid ${open ? C.borderMid : C.border}`,
      background: open ? C.surface2 : 'transparent',
      overflow: 'hidden',
      transition: 'background 0.15s, border-color 0.15s',
    }}>
      {/* Step header */}
      <div
        onClick={() => setOpen(v => !v)}
        style={{
          display: 'flex', alignItems: 'center', gap: 8,
          padding: '7px 10px', cursor: 'pointer',
        }}
      >
        {/* Step icon */}
        <div style={{
          width: 22, height: 22, borderRadius: 5, flexShrink: 0,
          background: `${accentColor}18`,
          display: 'flex', alignItems: 'center', justifyContent: 'center',
        }}>
          <Icon name={step.icon} size={11} color={accentColor} />
        </div>

        {/* Label */}
        <span style={{ fontSize: 12, fontWeight: 600, color: C.text2, flex: 1 }}>{step.label}</span>

        {/* Token stats */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexShrink: 0 }}>
          <span style={{ fontSize: 10.5, color: C.text3, fontFamily: "'DM Mono', monospace" }}>
            <span style={{ color: C.blue }}>{step.tokensIn.toLocaleString()}</span>
            {' in '}
          </span>
          <span style={{ fontSize: 10.5, color: C.text3, fontFamily: "'DM Mono', monospace" }}>
            <span style={{ color: C.emerald }}>{step.tokensOut.toLocaleString()}</span>
            {' out '}
          </span>
          <span style={{ fontSize: 10.5, color: C.text3, fontFamily: "'DM Mono', monospace" }}>
            <span style={{ color: C.text2 }}>{step.totalTokens.toLocaleString()}</span>
            {' total '}
          </span>
          <span style={{
            fontSize: 10.5, color: C.text3,
            fontFamily: "'DM Mono', monospace",
            paddingLeft: 6,
            borderLeft: `1px solid ${C.border}`,
          }}>{step.dur}</span>
          <Icon name={open ? 'chevron-up' : 'chevron-down'} size={10} color={C.text3} />
        </div>
      </div>

      <ProcessStepDetail step={step} open={open} />
    </div>
  );
}

function ProcessTrace({ msg }) {
  const [open, setOpen] = useState(false);
  const steps = msg.steps || [];

  return (
    <div style={{ margin: '2px 0' }}>
      {/* Top-level summary row */}
      <div
        onClick={() => setOpen(v => !v)}
        style={{
          padding: '6px 16px', display: 'flex', alignItems: 'center',
          gap: 8, cursor: 'pointer',
        }}
      >
        <div style={{
          width: 18, height: 18, borderRadius: '50%',
          background: C.surface3,
          display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0,
        }}>
          <Icon name="zap" size={9} color={C.text3} />
        </div>
        <span style={{ flex: 1, fontSize: 11.5, color: C.text3 }}>{msg.summary}</span>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexShrink: 0 }}>
          {msg.status === 'completed' && (
            <span style={{ fontSize: 11, color: C.emerald }}>Completed · {msg.dur}</span>
          )}
          <Icon name={open ? 'chevron-up' : 'chevron-down'} size={11} color={C.text3} />
        </div>
      </div>

      {/* Expanded steps */}
      {open && steps.length > 0 && (
        <div style={{
          margin: '0 16px 6px',
          background: C.surface2,
          border: `1px solid ${C.border}`,
          borderRadius: 9,
          padding: 6,
          display: 'flex',
          flexDirection: 'column',
          gap: 4,
        }}>
          {steps.map(step => (
            <ProcessStep key={step.id} step={step} />
          ))}
        </div>
      )}
    </div>
  );
}



// ─── Turn shapes ──────────────────────────────────────────────────────────────
const TURN_SHAPES = ['Compact', 'Brief', 'Extended', 'Monologue', 'Silent', 'Silent Extended'];

// ─── Edit Plan Modal ──────────────────────────────────────────────────────────
function EditPlanModal({ msg, chars, onClose }) {
  const [shape,         setShape]         = useState(msg.plan?.shape         || 'Brief');
  const [beat,          setBeat]          = useState(msg.plan?.beat          || '');
  const [intent,        setIntent]        = useState(msg.plan?.intent        || '');
  const [goal,          setGoal]          = useState(msg.plan?.goal          || '');
  const [whyNow,        setWhyNow]        = useState(msg.plan?.whyNow        || '');
  const [changeIntro,   setChangeIntro]   = useState(msg.plan?.changeIntro   || '');
  const [guardrails,    setGuardrails]    = useState(msg.plan?.guardrails    || '');
  const [privateIntent, setPrivateIntent] = useState(msg.plan?.privateIntent || '');
  const [appearances,   setAppearances]   = useState(() => {
    const a = {};
    (chars || []).forEach(c => { a[c.id] = msg.plan?.appearances?.[c.id] || ''; });
    return a;
  });

  const fieldStyle = {
    width: '100%', background: C.surface2, border: `1px solid ${C.border}`,
    borderRadius: 7, padding: '8px 10px', color: C.text, fontSize: 13,
    fontFamily: "'DM Sans', sans-serif", outline: 'none', resize: 'none',
    lineHeight: 1.5,
  };
  const labelStyle = { fontSize: 11, fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase', color: C.text3, marginBottom: 5, display: 'block' };

  function Field({ label, value, onChange, rows = 1, placeholder = '' }) {
    return (
      <div style={{ marginBottom: 14 }}>
        <label style={labelStyle}>{label}</label>
        {rows > 1
          ? <textarea rows={rows} value={value} onChange={e => onChange(e.target.value)} placeholder={placeholder} style={fieldStyle} />
          : <input type="text" value={value} onChange={e => onChange(e.target.value)} placeholder={placeholder} style={fieldStyle} />}
      </div>
    );
  }

  function SectionHead({ children }) {
    return (
      <div style={{ fontSize: 10, fontWeight: 700, letterSpacing: '0.1em', textTransform: 'uppercase', color: C.text3, padding: '6px 0 10px', borderBottom: `1px solid ${C.border}`, marginBottom: 16 }}>
        {children}
      </div>
    );
  }

  return (
    <div style={{
      position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.6)', zIndex: 500,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
    }} onClick={e => { if (e.target === e.currentTarget) onClose(); }}>
      <div style={{
        background: C.surface, border: `1px solid ${C.borderMid}`,
        borderRadius: 14, width: 560, maxHeight: '88vh',
        display: 'flex', flexDirection: 'column',
        boxShadow: '0 24px 80px rgba(0,0,0,0.7)',
      }}>
        {/* Header */}
        <div style={{ display: 'flex', alignItems: 'center', padding: '16px 20px', borderBottom: `1px solid ${C.border}`, flexShrink: 0 }}>
          <Icon name="sliders" size={14} color={C.text3} style={{ marginRight: 9 }} />
          <span style={{ flex: 1, fontSize: 14, fontWeight: 600, color: C.text }}>Edit Saved Plan</span>
          <span style={{ fontSize: 12, color: C.text3, marginRight: 12 }}>{msg.author} · {msg.ts}</span>
          <Btn variant="ghost" sz="icon" onClick={onClose}><Icon name="x" size={14} /></Btn>
        </div>

        {/* Body */}
        <div style={{ overflowY: 'auto', padding: '20px 24px', flex: 1 }}>

          {/* ── Plan ── */}
          <SectionHead>Plan</SectionHead>

          {/* Turn shape */}
          <div style={{ marginBottom: 14 }}>
            <label style={labelStyle}>Turn Shape</label>
            <div style={{ display: 'flex', gap: 5, flexWrap: 'wrap' }}>
              {TURN_SHAPES.map(s => (
                <button key={s} onClick={() => setShape(s)} style={{
                  padding: '5px 12px', borderRadius: 6, fontSize: 12, fontWeight: 500,
                  cursor: 'pointer', border: `1px solid ${shape === s ? C.amber : C.border}`,
                  background: shape === s ? C.amberBg : C.surface2,
                  color: shape === s ? C.amber : C.text2,
                  transition: 'all 0.12s',
                }}>{s}</button>
              ))}
            </div>
          </div>

          <Field label="Beat"              value={beat}        onChange={setBeat}        placeholder="What story beat does this turn serve?" rows={2} />
          <Field label="Intent"            value={intent}      onChange={setIntent}      placeholder="What does the character intend to do?" rows={2} />
          <Field label="Immediate Goal"    value={goal}        onChange={setGoal}        placeholder="What does the character want right now?" />
          <Field label="Why Now"           value={whyNow}      onChange={setWhyNow}      placeholder="Why is the character acting at this moment?" rows={2} />
          <Field label="Change Introduced" value={changeIntro} onChange={setChangeIntro} placeholder="What shifts as a result of this turn?" rows={2} />
          <Field label="Guardrails"        value={guardrails}  onChange={setGuardrails}  placeholder="What should the AI avoid or preserve?" rows={2} />

          {/* ── Private Intent ── */}
          <SectionHead>Private Intent</SectionHead>
          <Field label="Private Intent" value={privateIntent} onChange={setPrivateIntent} placeholder="Hidden motivation, known only to the narrator…" rows={3} />

          {/* ── Appearance ── */}
          {(chars || []).length > 0 && (
            <>
              <SectionHead>Appearance</SectionHead>
              {(chars || []).map(c => (
                <Field key={c.id} label={c.name}
                  value={appearances[c.id] || ''}
                  onChange={v => setAppearances(prev => ({ ...prev, [c.id]: v }))}
                  placeholder={`How does ${c.name} appear in this turn?`}
                  rows={2}
                />
              ))}
            </>
          )}
        </div>

        {/* Footer */}
        <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', padding: '14px 20px', borderTop: `1px solid ${C.border}`, flexShrink: 0 }}>
          <Btn variant="ghost" sz="sm" onClick={onClose}>Cancel</Btn>
          <Btn variant="primary" sz="sm" onClick={onClose}>Save Plan</Btn>
        </div>
      </div>
    </div>
  );
}

function MessageDeleteDialog({ onDeleteSingle, onDeleteBranch, onCancel, subsequentCount }) {
  const [step, setStep] = useState('choose'); // 'choose' | 'branch-confirm'

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
        borderRadius: 12, width: '100%', maxWidth: 400,
        padding: '22px 22px 18px',
        boxShadow: '0 28px 80px rgba(0,0,0,0.65)',
        display: 'flex', flexDirection: 'column', gap: 14,
        animation: 'cdConfirmIn 0.15s ease',
      }}>
        {step === 'choose' ? (
          <>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
              <div style={{ width: 36, height: 36, borderRadius: 9, flexShrink: 0, background: `${C.rose}22`, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <Icon name="trash" size={16} color={C.rose} />
              </div>
              <div>
                <div style={{ fontSize: 14, fontWeight: 600, color: C.text }}>Delete message</div>
                <div style={{ fontSize: 12, color: C.text3, marginTop: 2 }}>Choose what to remove</div>
              </div>
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              <button onClick={onDeleteSingle}
                style={{
                  display: 'flex', alignItems: 'center', gap: 12, padding: '12px 14px',
                  background: C.surface2, border: `1px solid ${C.border}`, borderRadius: 9,
                  cursor: 'pointer', textAlign: 'left', transition: 'border-color 0.12s',
                }}
                onMouseEnter={e => e.currentTarget.style.borderColor = C.borderMid}
                onMouseLeave={e => e.currentTarget.style.borderColor = C.border}>
                <Icon name="trash" size={14} color={C.text2} />
                <div>
                  <div style={{ fontSize: 13, fontWeight: 500, color: C.text }}>This message only</div>
                  <div style={{ fontSize: 11.5, color: C.text3, marginTop: 1 }}>Removes just this turn from the transcript</div>
                </div>
              </button>
              {subsequentCount > 0 && (
                <button onClick={() => setStep('branch-confirm')}
                  style={{
                    display: 'flex', alignItems: 'center', gap: 12, padding: '12px 14px',
                    background: `${C.rose}0d`, border: `1px solid ${C.rose}44`, borderRadius: 9,
                    cursor: 'pointer', textAlign: 'left', transition: 'border-color 0.12s',
                  }}
                  onMouseEnter={e => e.currentTarget.style.borderColor = `${C.rose}88`}
                  onMouseLeave={e => e.currentTarget.style.borderColor = `${C.rose}44`}>
                  <Icon name="git-branch" size={14} color={C.rose} />
                  <div>
                    <div style={{ fontSize: 13, fontWeight: 500, color: C.rose }}>Delete branch from here</div>
                    <div style={{ fontSize: 11.5, color: C.text3, marginTop: 1 }}>This message + {subsequentCount} following turn{subsequentCount !== 1 ? 's' : ''}</div>
                  </div>
                </button>
              )}
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
              <Btn variant="secondary" sz="sm" onClick={onCancel}>Cancel</Btn>
            </div>
          </>
        ) : (
          <>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
              <div style={{ width: 36, height: 36, borderRadius: 9, flexShrink: 0, background: `${C.rose}22`, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <Icon name="git-branch" size={16} color={C.rose} />
              </div>
              <span style={{ fontSize: 14, fontWeight: 600, color: C.text }}>Delete branch from here?</span>
            </div>
            <p style={{ fontSize: 12.5, color: C.text2, lineHeight: 1.65, margin: 0, paddingLeft: 48 }}>
              This will permanently remove this message and the <strong style={{ color: C.text }}>{subsequentCount}</strong> turn{subsequentCount !== 1 ? 's' : ''} that follow. The story cannot be recovered from this point.
            </p>
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 2 }}>
              <Btn variant="secondary" sz="sm" onClick={() => setStep('choose')}>Back</Btn>
              <Btn variant="rose" sz="sm" onClick={onDeleteBranch}>
                <Icon name="trash" size={11} color="white" />Delete {subsequentCount + 1} messages
              </Btn>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function NarrativeMessage({ msg, chars, onDeleteMsg, onDeleteBranch, subsequentCount }) {
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [hov, setHov] = useState(false);
  const [authorOpen, setAuthorOpen] = useState(false);
  const [shapeOpen,  setShapeOpen]  = useState(false);
  const [planOpen,   setPlanOpen]   = useState(false);
  const authorRef = useRef(null);
  const shapeRef  = useRef(null);
  const isNarrator = msg.author === 'Narrator';

  useEffect(() => {
    if (!authorOpen) return;
    function onDown(e) { if (authorRef.current && !authorRef.current.contains(e.target)) setAuthorOpen(false); }
    document.addEventListener('mousedown', onDown);
    return () => document.removeEventListener('mousedown', onDown);
  }, [authorOpen]);

  useEffect(() => {
    if (!shapeOpen) return;
    function onDown(e) { if (shapeRef.current && !shapeRef.current.contains(e.target)) setShapeOpen(false); }
    document.addEventListener('mousedown', onDown);
    return () => document.removeEventListener('mousedown', onDown);
  }, [shapeOpen]);
  const [from] = isNarrator ? ['oklch(56% 0.11 285)', ''] : charColors(msg.author);
  const proseColor = isNarrator ? C.rose : C.amber;
  const modeColor = msg.mode === 'Guided AI' ? C.blue : msg.mode === 'Automatic AI' ? C.violet : C.text3;
  const modeBg    = msg.mode === 'Guided AI' ? C.blueBg : msg.mode === 'Automatic AI' ? C.violetBg : C.surface3;

  return (
    <>
    <div onMouseEnter={() => setHov(true)} onMouseLeave={() => setHov(false)}
      style={{ margin: '4px 0', padding: '0 16px' }}>
      <div style={{
        background: hov ? C.surface2 : 'transparent',
        borderRadius: 10, padding: '12px 14px',
        border: hov ? `1px solid ${C.border}` : '1px solid transparent',
        transition: 'background 0.15s, border-color 0.15s',
      }}>
        {/* Header */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
          {isNarrator ? (
            <div style={{ width: 26, height: 26, borderRadius: '50%', background: C.surface4, display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
              <Icon name="book-open" size={12} color={C.text3} />
            </div>
          ) : (
            <Avatar name={msg.author} size={26} />
          )}
          <span style={{ fontWeight: 600, fontSize: 13, color: C.text }}>{msg.author}</span>
          <span style={{ fontSize: 11, color: C.text3, marginLeft: 'auto' }}>{msg.ts}</span>
          <span style={{
            fontSize: 10, fontWeight: 700, padding: '2px 7px', borderRadius: 4,
            background: modeBg, color: modeColor,
            letterSpacing: '0.04em',
          }}>{msg.mode}</span>
        </div>

        {/* Body */}
        <p style={{
          fontSize: 14, lineHeight: 1.75,
          fontFamily: "'Playfair Display', Georgia, serif",
          fontStyle: 'italic',
          color: proseColor,
          letterSpacing: '0.01em',
          textWrap: 'pretty',
        }}>{msg.body}</p>

        {/* Footer — always rendered to prevent layout shift; hidden when not hovered */}
        <div style={{
          marginTop: 10,
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          visibility: (hov || authorOpen || shapeOpen || planOpen) ? 'visible' : 'hidden',
        }}>
          {msg.branch ? (
            <div style={{ display: 'flex', alignItems: 'center', gap: 2 }}>
              <Btn variant="ghost" sz="icon" style={{ padding: 4 }}><Icon name="chevron-left" size={12} /></Btn>
              <span style={{ fontSize: 11, color: C.text3, padding: '0 4px', fontFamily: "'DM Mono', monospace" }}>{msg.branch}</span>
              <Btn variant="ghost" sz="icon" style={{ padding: 4 }}><Icon name="chevron-right" size={12} /></Btn>
            </div>
          ) : <div />}
          <div style={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            {/* Change author popup */}
            <div style={{ position: 'relative' }} ref={authorRef}>
              <Btn variant="ghost" sz="icon" title="Change author" style={{ padding: 4 }}
                onClick={() => setAuthorOpen(v => !v)}>
                <Icon name="users" size={12} color={authorOpen ? C.amber : C.text3} />
              </Btn>
              {authorOpen && (
                <div style={{
                  position: 'absolute', bottom: 'calc(100% + 6px)', left: '50%',
                  transform: 'translateX(-50%)',
                  background: C.surface3, border: `1px solid ${C.borderMid}`,
                  borderRadius: 10, padding: 4, minWidth: 170,
                  boxShadow: '0 12px 40px rgba(0,0,0,0.55)', zIndex: 300,
                }}>
                  <div style={{ padding: '5px 10px 4px', fontSize: 10, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: C.text3 }}>
                    Change Author
                  </div>
                  {/* Narrator row */}
                  {(() => {
                    const isCurrent = msg.author === 'Narrator';
                    return (
                      <div style={{
                        padding: '6px 10px', borderRadius: 6,
                        display: 'flex', alignItems: 'center', gap: 8,
                        cursor: isCurrent ? 'default' : 'pointer',
                        opacity: isCurrent ? 0.4 : 1,
                        background: isCurrent ? C.surface4 : 'transparent',
                      }}
                        onMouseEnter={e => { if (!isCurrent) e.currentTarget.style.background = C.surface4; }}
                        onMouseLeave={e => { if (!isCurrent) e.currentTarget.style.background = 'transparent'; }}
                        onClick={() => { if (!isCurrent) setAuthorOpen(false); }}>
                        <div style={{ width: 20, height: 20, borderRadius: '50%', background: C.surface4, display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
                          <Icon name="book-open" size={10} color={C.text3} />
                        </div>
                        <span style={{ fontSize: 12, color: C.text2, fontStyle: 'italic', flex: 1 }}>Narrator</span>
                        {isCurrent && <Icon name="check" size={11} color={C.text3} />}
                      </div>
                    );
                  })()}
                  {(chars || []).map(c => {
                    const isCurrent = msg.author === c.name;
                    return (
                      <div key={c.id} style={{
                        padding: '6px 10px', borderRadius: 6,
                        display: 'flex', alignItems: 'center', gap: 8,
                        cursor: isCurrent ? 'default' : 'pointer',
                        opacity: isCurrent ? 0.4 : 1,
                        background: isCurrent ? C.surface4 : 'transparent',
                      }}
                        onMouseEnter={e => { if (!isCurrent) e.currentTarget.style.background = C.surface4; }}
                        onMouseLeave={e => { if (!isCurrent) e.currentTarget.style.background = 'transparent'; }}
                        onClick={() => { if (!isCurrent) setAuthorOpen(false); }}>
                        <Avatar name={c.name} size={20} />
                        <span style={{ fontSize: 12, color: C.text, flex: 1 }}>{c.name}</span>
                        {isCurrent && <Icon name="check" size={11} color={C.text3} />}
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
            {/* Edit message */}
            <Btn variant="ghost" sz="icon" title="Edit message" style={{ padding: 4 }}><Icon name="edit" size={12} color={C.text3} /></Btn>

            <div style={{ width: 1, height: 14, background: C.border, margin: '0 3px' }} />

            {/* Regen button group */}
            <div style={{
              display: 'flex', alignItems: 'stretch',
              border: `1px solid ${C.border}`, borderRadius: 7,
              background: C.surface3, position: 'relative',
            }}>
              {/* Regenerate from plan */}
              <button title="Regenerate from saved plan" style={{
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                padding: '3px 8px', background: 'transparent', border: 'none',
                cursor: 'pointer', gap: 4, color: C.blue,
                borderRadius: '6px 0 0 6px',
              }}
                onMouseEnter={e => e.currentTarget.style.background = C.surface4}
                onMouseLeave={e => e.currentTarget.style.background = 'transparent'}>
                <Icon name="refresh" size={12} color={C.blue} />
              </button>

              <div style={{ width: 1, background: C.border }} />

              {/* Turn shape + regenerate */}
              <div style={{ position: 'relative' }} ref={shapeRef}>
                <button title="Change turn shape & regenerate" onClick={() => setShapeOpen(v => !v)} style={{
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  padding: '3px 7px', background: 'transparent', border: 'none',
                  cursor: 'pointer', gap: 3,
                }}
                  onMouseEnter={e => e.currentTarget.style.background = C.surface4}
                  onMouseLeave={e => e.currentTarget.style.background = 'transparent'}>
                  <Icon name="layers" size={12} color={C.text3} />
                  <Icon name="chevron-down" size={10} color={C.text3} />
                </button>
                {shapeOpen && (
                  <div style={{
                    position: 'absolute', bottom: 'calc(100% + 6px)', left: '50%',
                    transform: 'translateX(-50%)',
                    background: C.surface3, border: `1px solid ${C.borderMid}`,
                    borderRadius: 9, padding: 4, minWidth: 168,
                    boxShadow: '0 12px 40px rgba(0,0,0,0.55)', zIndex: 300,
                  }}>
                    <div style={{ padding: '5px 10px 4px', fontSize: 10, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: C.text3 }}>
                      Turn Shape
                    </div>
                    {TURN_SHAPES.map(s => (
                      <div key={s} style={{
                        padding: '7px 10px', borderRadius: 6, cursor: 'pointer',
                        display: 'flex', alignItems: 'center', gap: 8,
                      }}
                        onMouseEnter={e => e.currentTarget.style.background = C.surface4}
                        onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
                        onClick={() => setShapeOpen(false)}>
                        <span style={{ fontSize: 12.5, color: C.text }}>{s}</span>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              <div style={{ width: 1, background: C.border }} />

              {/* Edit saved plan */}
              <button title="Edit saved plan" onClick={() => setPlanOpen(true)} style={{
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                padding: '3px 8px', background: 'transparent', border: 'none',
                cursor: 'pointer', borderRadius: '0 6px 6px 0',
              }}
                onMouseEnter={e => e.currentTarget.style.background = C.surface4}
                onMouseLeave={e => e.currentTarget.style.background = 'transparent'}>
                <Icon name="sliders" size={12} color={C.text3} />
              </button>
            </div>

            <div style={{ width: 1, height: 14, background: C.border, margin: '0 3px' }} />

            {/* Snapshot / Copy / Delete */}
            <Btn variant="ghost" sz="icon" title="Create snapshot here" style={{ padding: 4 }}><Icon name="flag" size={12} color={C.text3} /></Btn>
            <Btn variant="ghost" sz="icon" title="Copy message" style={{ padding: 4 }}><Icon name="copy" size={12} color={C.text3} /></Btn>
            <Btn variant="ghost" sz="icon" title="Delete message" style={{ padding: 4 }} onClick={() => setDeleteOpen(true)}><Icon name="trash" size={12} color={C.text3} /></Btn>
          </div>
        </div>
      </div>
    </div>

    {/* Edit Plan modal */}
    {planOpen && <EditPlanModal msg={msg} chars={chars} onClose={() => setPlanOpen(false)} />}
    {deleteOpen && (
      <MessageDeleteDialog
        subsequentCount={subsequentCount || 0}
        onDeleteSingle={() => { setDeleteOpen(false); onDeleteMsg && onDeleteMsg(msg.id); }}
        onDeleteBranch={() => { setDeleteOpen(false); onDeleteBranch && onDeleteBranch(msg.id); }}
        onCancel={() => setDeleteOpen(false)}
      />
    )}
    </>
  );
}

function ChatMessage({ msg, showAppearance, showProcess, chars, onDeleteMsg, onDeleteBranch, subsequentCount }) {
  if (msg.type === 'process'    && !showProcess)    return null;
  if (msg.type === 'appearance') return null;
  if (msg.type === 'process')    return <ProcessTrace msg={msg} />;
  if (msg.type === 'narrative')  return <NarrativeMessage msg={msg} chars={chars} onDeleteMsg={onDeleteMsg} onDeleteBranch={onDeleteBranch} subsequentCount={subsequentCount} />;
  return null;
}

function ChatFooter({ chars, speakingAs, setSpeakingAs, onPost }) {
  const [val, setVal] = useState('');
  const [turnShape, setTurnShape] = useState('Brief');
  const [speakerOpen, setSpeakerOpen] = useState(false);
  const [shapeOpen, setShapeOpen] = useState(false);
  const [respondAs, setRespondAs] = useState(null); // null = Narrator
  const [respondOpen, setRespondOpen] = useState(false);
  const textRef = useRef(null);
  const shapeRef = useRef(null);
  const respondRef = useRef(null);

  const isGuided = val.trim().length > 0;

  function handleKey(e) {
    if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) {
      onPost(val, speakingAs, isGuided ? 'guided' : 'automatic');
      setVal('');
    }
  }

  useEffect(() => {
    if (!shapeOpen) return;
    function onDown(e) { if (shapeRef.current && !shapeRef.current.contains(e.target)) setShapeOpen(false); }
    document.addEventListener('mousedown', onDown);
    return () => document.removeEventListener('mousedown', onDown);
  }, [shapeOpen]);

  useEffect(() => {
    if (!respondOpen) return;
    function onDown(e) { if (respondRef.current && !respondRef.current.contains(e.target)) setRespondOpen(false); }
    document.addEventListener('mousedown', onDown);
    return () => document.removeEventListener('mousedown', onDown);
  }, [respondOpen]);

  const speakerName = speakingAs ? speakingAs.name : 'Narrator';
  const respondName = respondAs ? respondAs.name : 'Narrator';
  const modeColor = isGuided ? C.blue : C.violet;
  const modeIcon  = isGuided ? 'layers' : 'zap';
  const modeLabel = isGuided ? 'Guided' : 'Auto';

  return (
    <div style={{ padding: '12px 16px 16px' }}>
      <div>
        <div style={{ background: C.surface2, borderRadius: 10, border: `1px solid ${C.border}`, overflow: 'hidden' }}>
          <textarea
            ref={textRef} value={val} onChange={e => setVal(e.target.value)} onKeyDown={handleKey}
            placeholder="Write a message, or let AI continue the scene…"
            rows={3}
            style={{
              width: '100%', background: 'transparent', border: 'none', outline: 'none',
              padding: '12px 14px 6px', color: C.text, fontSize: 13.5,
              fontFamily: "'Playfair Display', Georgia, serif",
              fontStyle: val ? 'italic' : 'normal',
              lineHeight: 1.7, resize: 'none',
            }} />

          {/* Footer controls */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, padding: '6px 10px', borderTop: `1px solid ${C.border}` }}>

            {/* Speaker selector */}
            <div style={{ position: 'relative' }}>
              <Btn variant="secondary" sz="xs" onClick={() => setSpeakerOpen(v => !v)} style={{ gap: 5 }}>
                {speakingAs ? <Avatar name={speakingAs.name} size={14} /> :
                  <div style={{ width: 14, height: 14, borderRadius: '50%', background: C.surface4, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <Icon name="book-open" size={8} color={C.text3} />
                  </div>}
                <span style={{ fontSize: 11.5 }}>{speakerName}</span>
                <Icon name="chevron-down" size={10} />
              </Btn>
              {speakerOpen && (
                <div style={{
                  position: 'absolute', bottom: '100%', left: 0, marginBottom: 6,
                  background: C.surface3, border: `1px solid ${C.borderMid}`,
                  borderRadius: 9, padding: 4, minWidth: 160,
                  boxShadow: '0 8px 32px rgba(0,0,0,0.5)', zIndex: 100,
                }}>
                  <div style={{ padding: '5px 10px', cursor: 'pointer', borderRadius: 6, display: 'flex', alignItems: 'center', gap: 8 }}
                    onClick={() => { setSpeakingAs(null); setSpeakerOpen(false); }}>
                    <div style={{ width: 20, height: 20, borderRadius: '50%', background: C.surface4, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                      <Icon name="book-open" size={10} color={C.text3} />
                    </div>
                    <span style={{ fontSize: 12, color: C.text2, fontStyle: 'italic' }}>Narrator</span>
                  </div>
                  {chars.map(c => (
                    <div key={c.id} style={{ padding: '5px 10px', cursor: 'pointer', borderRadius: 6, display: 'flex', alignItems: 'center', gap: 8 }}
                      onMouseEnter={e => e.currentTarget.style.background = C.surface4}
                      onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
                      onClick={() => { setSpeakingAs(c); setSpeakerOpen(false); }}>
                      <Avatar name={c.name} size={20} />
                      <span style={{ fontSize: 12, color: C.text }}>{c.name}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div style={{ flex: 1 }} />

            {/* Respond As */}
            <div style={{ position: 'relative' }} ref={respondRef}>
              <Btn variant="ghost" sz="xs" onClick={() => setRespondOpen(v => !v)} style={{ gap: 4, color: C.text3 }}>
                <Icon name="user" size={11} color={C.text3} />
                Respond As
                <Icon name="chevron-down" size={9} color={C.text3} />
              </Btn>
              {respondOpen && (
                <div style={{
                  position: 'absolute', bottom: 'calc(100% + 6px)', right: 0,
                  background: C.surface3, border: `1px solid ${C.borderMid}`,
                  borderRadius: 9, padding: 4, minWidth: 170,
                  boxShadow: '0 12px 40px rgba(0,0,0,0.55)', zIndex: 200,
                }}>
                  <div style={{ padding: '5px 10px 4px', fontSize: 10, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: C.text3 }}>
                    Respond As
                  </div>
                  <div
                    style={{
                      padding: '6px 10px', cursor: 'pointer', borderRadius: 6,
                      display: 'flex', alignItems: 'center', gap: 8,
                      background: !respondAs ? C.surface4 : 'transparent',
                    }}
                    onMouseEnter={e => e.currentTarget.style.background = C.surface4}
                    onMouseLeave={e => e.currentTarget.style.background = !respondAs ? C.surface4 : 'transparent'}
                    onClick={() => { setRespondAs(null); setRespondOpen(false); }}>
                    <div style={{ width: 20, height: 20, borderRadius: '50%', background: C.surface4, display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
                      <Icon name="book-open" size={10} color={C.text3} />
                    </div>
                    <span style={{ fontSize: 12, color: C.text2, fontStyle: 'italic', flex: 1 }}>Narrator</span>
                    {!respondAs && <Icon name="check" size={11} color={C.text3} />}
                  </div>
                  {chars.map(c => (
                    <div key={c.id}
                      style={{
                        padding: '6px 10px', cursor: 'pointer', borderRadius: 6,
                        display: 'flex', alignItems: 'center', gap: 8,
                        background: respondAs?.id === c.id ? C.surface4 : 'transparent',
                      }}
                      onMouseEnter={e => e.currentTarget.style.background = C.surface4}
                      onMouseLeave={e => e.currentTarget.style.background = respondAs?.id === c.id ? C.surface4 : 'transparent'}
                      onClick={() => { setRespondAs(c); setRespondOpen(false); }}>
                      <Avatar name={c.name} size={20} />
                      <span style={{ fontSize: 12, color: C.text, flex: 1 }}>{c.name}</span>
                      {respondAs?.id === c.id && <Icon name="check" size={11} color={C.text3} />}
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div style={{ width: 1, height: 16, background: C.border }} />

            {/* Combined mode + turn shape button */}
            <div style={{ position: 'relative', display: 'flex', alignItems: 'stretch' }} ref={shapeRef}>
              {/* Mode label — clicking fires the post action */}
              <button
                onClick={() => { onPost(val, speakingAs, isGuided ? 'guided' : 'automatic'); setVal(''); }}
                style={{
                  display: 'flex', alignItems: 'center', gap: 5,
                  padding: '3px 9px', background: C.surface3,
                  border: `1px solid ${C.border}`, borderRight: 'none',
                  borderRadius: '6px 0 0 6px', cursor: 'pointer',
                  color: modeColor, fontSize: 11.5, fontWeight: 500,
                  fontFamily: "'DM Sans', sans-serif",
                  transition: 'background 0.12s',
                }}
                onMouseEnter={e => e.currentTarget.style.background = C.surface4}
                onMouseLeave={e => e.currentTarget.style.background = C.surface3}
                title={isGuided ? 'Post guided message (⌘↵)' : 'Auto-continue scene (⌘↵)'}
              >
                <Icon name={modeIcon} size={11} color={modeColor} />
                {modeLabel}
              </button>

              {/* Turn shape dropdown trigger */}
              <button
                onClick={() => setShapeOpen(v => !v)}
                style={{
                  display: 'flex', alignItems: 'center', gap: 3,
                  padding: '3px 7px', background: C.surface3,
                  border: `1px solid ${C.border}`,
                  borderRadius: '0 6px 6px 0', cursor: 'pointer',
                  color: C.text3, fontSize: 11, fontWeight: 500,
                  fontFamily: "'DM Sans', sans-serif",
                  transition: 'background 0.12s',
                  borderLeft: `1px solid ${C.borderMid}`,
                }}
                onMouseEnter={e => e.currentTarget.style.background = C.surface4}
                onMouseLeave={e => e.currentTarget.style.background = C.surface3}
                title="Select Turn Shape"
              >
                <Icon name="chevron-down" size={9} color={C.text3} />
              </button>

              {/* Turn shape dropdown */}
              {shapeOpen && (
                <div style={{
                  position: 'absolute', bottom: 'calc(100% + 6px)', right: 0,
                  background: C.surface3, border: `1px solid ${C.borderMid}`,
                  borderRadius: 9, padding: 4, minWidth: 170,
                  boxShadow: '0 12px 40px rgba(0,0,0,0.55)', zIndex: 200,
                }}>
                  <div style={{ padding: '5px 10px 4px', fontSize: 10, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: C.text3 }}>
                    Turn Shape
                  </div>
                  {TURN_SHAPES.map(s => (
                    <div key={s}
                      onClick={() => { setTurnShape(s); setShapeOpen(false); }}
                      style={{
                        padding: '7px 10px', borderRadius: 6, cursor: 'pointer',
                        display: 'flex', alignItems: 'center', gap: 8,
                        background: s === turnShape ? C.surface4 : 'transparent',
                      }}
                      onMouseEnter={e => e.currentTarget.style.background = C.surface4}
                      onMouseLeave={e => e.currentTarget.style.background = s === turnShape ? C.surface4 : 'transparent'}>
                      <span style={{ fontSize: 12.5, color: s === turnShape ? C.text : C.text2, flex: 1 }}>{s}</span>
                      {s === turnShape && <Icon name="check" size={11} color={C.text3} />}
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div style={{ width: 1, height: 16, background: C.border }} />

            <Btn variant={val.trim() ? 'primary' : 'ghost'} sz="xs"
              onClick={() => { if (val.trim()) { onPost(val, speakingAs, isGuided ? 'guided' : 'automatic'); setVal(''); } }}
              disabled={!val.trim()}
              title="Send (⌘↵)">
              <Icon name="send" size={11} color={val.trim() ? '#0d0b09' : C.text3} />
              Post
            </Btn>
            <span style={{ fontSize: 11, color: C.text3, paddingLeft: 2 }}>⌘↵</span>
          </div>
        </div>
      </div>
    </div>
  );
}

// ─── Chat header "..." menu ───────────────────────────────────────────────────
function ChatMenu({ showAppearance, setShowAppearance, showProcess, setShowProcess }) {
  const [open, setOpen] = useState(false);
  const ref = useRef(null);

  useEffect(() => {
    if (!open) return;
    function onDown(e) { if (ref.current && !ref.current.contains(e.target)) setOpen(false); }
    document.addEventListener('mousedown', onDown);
    return () => document.removeEventListener('mousedown', onDown);
  }, [open]);

  function ToggleItem({ label, icon, checked, onChange }) {
    return (
      <div onClick={() => onChange(!checked)}
        style={{
          display: 'flex', alignItems: 'center', gap: 10, padding: '8px 12px',
          cursor: 'pointer', borderRadius: 6,
          transition: 'background 0.1s',
        }}
        onMouseEnter={e => e.currentTarget.style.background = C.surface4}
        onMouseLeave={e => e.currentTarget.style.background = 'transparent'}>
        <Icon name={icon} size={12} color={checked ? C.amber : C.text3} />
        <span style={{ flex: 1, fontSize: 12.5, color: checked ? C.text : C.text2 }}>{label}</span>
        {/* Toggle pill */}
        <div style={{
          width: 28, height: 16, borderRadius: 8, flexShrink: 0,
          background: checked ? C.amber : C.surface4,
          border: `1px solid ${checked ? C.amber : C.border}`,
          position: 'relative', transition: 'background 0.15s, border-color 0.15s',
        }}>
          <div style={{
            position: 'absolute', top: 2, left: checked ? 12 : 2,
            width: 10, height: 10, borderRadius: '50%',
            background: checked ? '#0d0b09' : C.text3,
            transition: 'left 0.15s',
          }} />
        </div>
      </div>
    );
  }

  return (
    <div style={{ position: 'relative' }} ref={ref}>
      <Btn variant="ghost" sz="icon" onClick={() => setOpen(v => !v)} active={open}>
        <Icon name="more-h" size={16} />
      </Btn>
      {open && (
        <div style={{
          position: 'absolute', top: 'calc(100% + 6px)', right: 0,
          background: C.surface3, border: `1px solid ${C.borderMid}`,
          borderRadius: 10, padding: 4, minWidth: 220,
          boxShadow: '0 12px 40px rgba(0,0,0,0.55)', zIndex: 200,
        }}>
          <div style={{ padding: '6px 12px 4px', fontSize: 10, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: C.text3 }}>
            Visible Blocks
          </div>
          <ToggleItem label="Appearance Blocks" icon="eye"    checked={showAppearance} onChange={setShowAppearance} />
          <ToggleItem label="Process Traces"    icon="zap"    checked={showProcess}    onChange={setShowProcess} />
          <div style={{ height: 1, background: 'var(--c-border)', margin: '4px 8px' }} />
          <div style={{ padding: '6px 12px 4px', fontSize: 10, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: C.text3 }}>
            Appearance
          </div>
          <div style={{ padding: '4px 12px 8px' }}>
            <ThemeToggle />
          </div>
        </div>
      )}
    </div>
  );
}

function ChatArea({ chat, messages, chars, speakingAs, setSpeakingAs, onPost, onDeleteMsg, onDeleteBranch }) {
  const endRef = useRef(null);
  const [showAppearance, setShowAppearance] = useState(true);
  const [showProcess,    setShowProcess]    = useState(true);

  useEffect(() => { endRef.current?.scrollIntoView({ block: 'nearest' }); }, [messages.length]);

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
      {/* Header */}
      <div style={{
        flexShrink: 0, padding: '14px 20px',
        borderBottom: `1px solid ${C.border}`,
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        background: C.surface,
      }}>
        <div>
          <h1 style={{ fontSize: 18, fontWeight: 700, letterSpacing: '-0.02em', fontFamily: "'Playfair Display', serif", color: C.text }}>{chat.title}</h1>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 2 }}>
            <Icon name="map-pin" size={11} color={C.text3} />
            <span style={{ fontSize: 12, color: C.text3 }}>{chat.location}</span>
            <span style={{ fontSize: 12, color: C.text3 }}>·</span>
            <span style={{ fontSize: 12, color: C.text3 }}>{chat.messages} messages</span>
          </div>
        </div>
        <ChatMenu
          showAppearance={showAppearance} setShowAppearance={setShowAppearance}
          showProcess={showProcess}       setShowProcess={setShowProcess}
        />
      </div>

      {/* Transcript + Footer together in scroll container */}
      <div style={{ flex: 1, overflowY: 'auto', padding: '12px 0', display: 'flex', flexDirection: 'column' }}>
        <div style={{ flex: 1 }}>
          {messages.map((msg, idx) => (
            <ChatMessage key={msg.id} msg={msg}
              showAppearance={showAppearance}
              showProcess={showProcess}
              chars={chars}
              onDeleteMsg={onDeleteMsg}
              onDeleteBranch={onDeleteBranch}
              subsequentCount={messages.length - idx - 1} />
          ))}
        </div>
        <ChatFooter chars={chars} speakingAs={speakingAs} setSpeakingAs={setSpeakingAs} onPost={onPost} />
        <div ref={endRef} />
      </div>
    </div>
  );
}

Object.assign(window, { ChatArea });

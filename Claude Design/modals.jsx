// modals.jsx

// ─── Image placeholder ─────────────────────────────────────────────────────
function ImgPlaceholder({ hue, width = '100%', height = 160, label, style: extra }) {
  const id = `g${hue}`;
  return (
    <div style={{
      width, height, borderRadius: 8, overflow: 'hidden',
      background: `linear-gradient(145deg, oklch(22% 0.07 ${hue}), oklch(16% 0.04 ${hue + 30}))`,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      position: 'relative', flexShrink: 0, ...extra,
    }}>
      <div style={{
        fontFamily: "'DM Mono', monospace", fontSize: 9, color: `oklch(45% 0.06 ${hue})`,
        textAlign: 'center', padding: '0 12px', lineHeight: 1.5,
        textTransform: 'uppercase', letterSpacing: '0.08em',
      }}>
        {label || '— image —'}
      </div>
      {/* Subtle grid pattern */}
      <svg style={{ position: 'absolute', inset: 0, width: '100%', height: '100%', opacity: 0.12 }} xmlns="http://www.w3.org/2000/svg">
        <defs>
          <pattern id={id} width="20" height="20" patternUnits="userSpaceOnUse">
            <path d="M 20 0 L 0 0 0 20" fill="none" stroke={`oklch(55% 0.08 ${hue})`} strokeWidth="0.5" />
          </pattern>
        </defs>
        <rect width="100%" height="100%" fill={`url(#${id})`} />
      </svg>
    </div>
  );
}

// ─── IMAGE GALLERY ────────────────────────────────────────────────────────────
function ImageGalleryModal({ onClose, onGenerate }) {
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('all');
  const [selected, setSelected] = useState(null);
  const [hov, setHov] = useState(null);
  const { confirmDelete, dialog: deleteDialog } = useConfirmDelete();

  const types = ['all', 'character', 'location', 'item'];
  const filtered = GALLERY_IMAGES.filter(img => {
    const matchType = filter === 'all' || img.entityType === filter;
    const matchSearch = !search || img.name.toLowerCase().includes(search.toLowerCase()) || img.entity.toLowerCase().includes(search.toLowerCase());
    return matchType && matchSearch;
  });

  return (
    <Modal onClose={onClose} maxW={860}>
      <ModalHeader title="Image Gallery" onClose={onClose} />

      {/* Filter bar */}
      <div style={{ padding: '10px 20px', borderBottom: `1px solid ${C.border}`, display: 'flex', alignItems: 'center', gap: 8, flexShrink: 0 }}>
        <div style={{ position: 'relative', flex: 1, maxWidth: 240 }}>
          <Icon name="search" size={13} color={C.text3} style={{ position: 'absolute', left: 9, top: '50%', transform: 'translateY(-50%)', pointerEvents: 'none' }} />
          <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Search images…"
            style={{ width: '100%', background: C.surface3, border: `1px solid ${C.border}`, borderRadius: 7, padding: '6px 10px 6px 30px', color: C.text, fontSize: 12.5, outline: 'none' }} />
        </div>
        <div style={{ display: 'flex', gap: 4 }}>
          {types.map(t => (
            <Btn key={t} variant={filter === t ? 'secondary' : 'ghost'} sz="xs" onClick={() => setFilter(t)}
              style={{ color: filter === t ? C.text : C.text3, textTransform: 'capitalize' }}>
              {t === 'all' ? 'All' : t === 'character' ? 'Characters' : t === 'location' ? 'Locations' : 'Items'}
            </Btn>
          ))}
        </div>
        <div style={{ flex: 1 }} />
        <Btn variant="secondary" sz="sm"><Icon name="upload" size={12} color={C.text2} />Upload</Btn>
        <Btn variant="primary" sz="sm" onClick={onGenerate}><Icon name="sparkle" size={12} color="#0d0b09" />Generate</Btn>
      </div>

      {/* Grid */}
      <div style={{ flex: 1, overflowY: 'auto', padding: 20 }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(170px, 1fr))', gap: 12 }}>
          {filtered.map(img => (
            <div key={img.id}
              onMouseEnter={() => setHov(img.id)} onMouseLeave={() => setHov(null)}
              onClick={() => setSelected(selected?.id === img.id ? null : img)}
              style={{
                borderRadius: 10, overflow: 'hidden', cursor: 'pointer',
                border: `2px solid ${selected?.id === img.id ? C.amber : hov === img.id ? C.borderMid : 'transparent'}`,
                transition: 'border-color 0.15s',
                background: C.surface2,
              }}>
              <div style={{ position: 'relative' }}>
                <ImgPlaceholder hue={img.hue} height={150} label={img.name} style={{ borderRadius: 0 }} />
                {hov === img.id && (
                  <div style={{
                    position: 'absolute', inset: 0, background: 'rgba(0,0,0,0.45)',
                    display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
                  }}>
                    <Btn variant="secondary" sz="xs" onClick={e => { e.stopPropagation(); }}>View</Btn>
                    <Btn variant="danger" sz="xs" onClick={e => { e.stopPropagation(); confirmDelete({ title: `Delete "${img.name}"?`, body: 'This image will be removed from the gallery.', onConfirm: () => {} }); }}>
                      <Icon name="trash" size={11} />
                    </Btn>
                  </div>
                )}
                {selected?.id === img.id && (
                  <div style={{ position: 'absolute', top: 6, right: 6, width: 18, height: 18, borderRadius: '50%', background: C.amber, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <Icon name="check" size={10} color="#0d0b09" />
                  </div>
                )}
              </div>
              <div style={{ padding: '8px 10px' }}>
                <div style={{ fontSize: 12.5, fontWeight: 500, color: C.text, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{img.name}</div>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: 2 }}>
                  <span style={{ fontSize: 10.5, color: C.text3, textTransform: 'capitalize' }}>{img.entityType}</span>
                  <span style={{ fontSize: 10.5, color: C.text3 }}>{img.date}</span>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Footer */}
      <div style={{ padding: '12px 20px', borderTop: `1px solid ${C.border}`, display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexShrink: 0 }}>
        <span style={{ fontSize: 12, color: C.text3 }}>{filtered.length} images{selected ? ' · 1 selected' : ''}</span>
        <div style={{ display: 'flex', gap: 8 }}>
          {selected && <Btn variant="primary" sz="sm">Use Selected</Btn>}
          <Btn variant="secondary" sz="sm" onClick={onClose}>Close</Btn>
        </div>
      </div>
      {deleteDialog}
    </Modal>
  );
}

// ─── GENERATE IMAGE ────────────────────────────────────────────────────────────
function GenerateImageModal({ onClose, onSaveToGallery }) {
  const [prompt, setPrompt] = useState('');
  const [model, setModel] = useState('gpt-image-1.5');
  const [size, setSize] = useState('landscape');
  const [quality, setQuality] = useState('auto');
  const [refs, setRefs] = useState('low');
  const [selectedEntities, setSelectedEntities] = useState(['c3']);
  const [selectedRefs, setSelectedRefs] = useState(['g1', 'g2']);
  const [generating, setGenerating] = useState(false);
  const [generated, setGenerated] = useState(null);
  const [history, setHistory] = useState([]);

  const allEntities = [
    ...CHARS.map(c => ({ ...c, type: 'Character' })),
    ...LOCS.map(l => ({ ...l, type: 'Location' })),
    ...ITEMS.map(i => ({ ...i, type: 'Item' })),
  ];

  function toggleEntity(id) {
    setSelectedEntities(prev => prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]);
  }
  function toggleRef(id) {
    setSelectedRefs(prev => prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]);
  }

  function handleGenerate() {
    if (!prompt.trim()) return;
    setGenerating(true);
    setTimeout(() => {
      const result = { hue: 200, rationale: "The prompt incorporates selected entity context while focusing on the user's scene description.", expandedPrompt: `A realistic scene: ${prompt}. Rendered with cinematic lighting and natural colors.` };
      setGenerated(result);
      setHistory(prev => [result, ...prev.slice(0, 4)]);
      setGenerating(false);
    }, 1800);
  }

  return (
    <Modal onClose={onClose} maxW={1000}>
      <ModalHeader title="Generate Image" onClose={onClose} />
      <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>

        {/* ── Left: Settings ── */}
        <div style={{ width: 400, borderRight: `1px solid ${C.border}`, display: 'flex', flexDirection: 'column', overflowY: 'auto' }}>
          <div style={{ padding: '16px 18px', borderBottom: `1px solid ${C.border}` }}>
            <Field label="Image Model">
              <FSelect value={model} onChange={setModel} options={[
                { value: 'gpt-image-1.5', label: 'gpt-image-1.5 (OpenAI)' },
                { value: 'dall-e-3', label: 'DALL·E 3 (OpenAI)' },
                { value: 'flux-1.1-pro', label: 'FLUX 1.1 Pro (Black Forest)' },
                { value: 'sd3.5', label: 'Stable Diffusion 3.5 (Stability)' },
              ]} />
            </Field>
            <div style={{ display: 'flex', gap: 8 }}>
              <div style={{ flex: 1 }}>
                <Field label="Size">
                  <FSelect value={size} onChange={setSize} options={[
                    { value: 'landscape', label: 'Landscape' },
                    { value: 'portrait', label: 'Portrait' },
                    { value: 'square', label: 'Square' },
                  ]} />
                </Field>
              </div>
              <div style={{ flex: 1 }}>
                <Field label="Quality">
                  <FSelect value={quality} onChange={setQuality} options={[
                    { value: 'auto', label: 'Auto' },
                    { value: 'high', label: 'High' },
                    { value: 'medium', label: 'Medium' },
                  ]} />
                </Field>
              </div>
              <div style={{ flex: 1 }}>
                <Field label="Refs">
                  <FSelect value={refs} onChange={setRefs} options={[
                    { value: 'low', label: 'Low' },
                    { value: 'medium', label: 'Medium' },
                    { value: 'high', label: 'High' },
                  ]} />
                </Field>
              </div>
            </div>
            <Field label="Prompt" onAI={() => {}}>
              <FTextarea value={prompt} onChange={setPrompt} placeholder="Describe the scene you want to generate…" rows={4} />
              <div style={{ fontSize: 11, color: C.text3, marginTop: 4 }}>Entity context from checked entities will be woven into the prompt automatically.</div>
            </Field>
          </div>

          {/* Entity context */}
          <div style={{ padding: '12px 18px', borderBottom: `1px solid ${C.border}` }}>
            <div style={{ fontSize: 11, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: C.text3, marginBottom: 10 }}>Entity Context</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 3, maxHeight: 200, overflowY: 'auto' }}>
              {allEntities.map(e => {
                const checked = selectedEntities.includes(e.id);
                return (
                  <div key={e.id}
                    onClick={() => toggleEntity(e.id)}
                    style={{
                      display: 'flex', alignItems: 'center', gap: 10, padding: '6px 8px',
                      borderRadius: 7, cursor: 'pointer',
                      background: checked ? `${C.amber}15` : 'transparent',
                      border: `1px solid ${checked ? C.amberDim : 'transparent'}`,
                      transition: 'all 0.12s',
                    }}>
                    <div style={{
                      width: 16, height: 16, borderRadius: 4, flexShrink: 0,
                      background: checked ? C.amber : C.surface3,
                      border: `1px solid ${checked ? C.amber : C.border}`,
                      display: 'flex', alignItems: 'center', justifyContent: 'center',
                      transition: 'all 0.12s',
                    }}>
                      {checked && <Icon name="check" size={10} color="#0d0b09" />}
                    </div>
                    {e.type === 'Character' ? <Avatar name={e.name} size={22} /> :
                      <div style={{ width: 22, height: 22, borderRadius: 5, background: C.surface3, display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
                        <Icon name={e.type === 'Location' ? 'map-pin' : 'box'} size={11} color={C.text3} />
                      </div>}
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontSize: 12.5, fontWeight: 500, color: C.text }}>{e.name}</div>
                      <div style={{ fontSize: 11, color: C.text3 }}>{e.type}</div>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>

          {/* Reference images */}
          <div style={{ padding: '12px 18px' }}>
            <div style={{ fontSize: 11, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: C.text3, marginBottom: 10 }}>Reference Images</div>
            <div style={{ display: 'flex', gap: 8, overflowX: 'auto', paddingBottom: 6 }}>
              {GALLERY_IMAGES.map(img => {
                const checked = selectedRefs.includes(img.id);
                return (
                  <div key={img.id} onClick={() => toggleRef(img.id)} style={{ flexShrink: 0, cursor: 'pointer' }}>
                    <div style={{ position: 'relative', borderRadius: 7, overflow: 'hidden', border: `2px solid ${checked ? C.amber : 'transparent'}`, transition: 'border-color 0.12s' }}>
                      <ImgPlaceholder hue={img.hue} width={72} height={56} label={img.name.split(' ')[0]} style={{ borderRadius: 0 }} />
                      {checked && (
                        <div style={{ position: 'absolute', bottom: 3, right: 3, width: 14, height: 14, borderRadius: '50%', background: C.amber, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                          <Icon name="check" size={8} color="#0d0b09" />
                        </div>
                      )}
                    </div>
                    <div style={{ fontSize: 10, color: C.text3, marginTop: 4, maxWidth: 72, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', textAlign: 'center' }}>{img.name.split(' ')[0]}</div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>

        {/* ── Right: Preview ── */}
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
          <div style={{ flex: 1, display: 'flex', flexDirection: 'column', padding: 20, gap: 12 }}>
            <div style={{ fontSize: 11, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: C.text3 }}>Generated Image</div>

            {/* Main preview */}
            <div style={{ flex: 1, minHeight: 280, borderRadius: 10, overflow: 'hidden', border: `1px solid ${C.border}`, position: 'relative' }}>
              {generating ? (
                <div style={{ width: '100%', height: '100%', background: C.surface2, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 12 }}>
                  <div style={{ width: 32, height: 32, borderRadius: '50%', border: `2px solid ${C.border}`, borderTopColor: C.amber, animation: 'spin 0.8s linear infinite' }} />
                  <span style={{ fontSize: 12, color: C.text3 }}>Generating…</span>
                  <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
                </div>
              ) : generated ? (
                <div style={{ position: 'relative', height: '100%' }}>
                  <ImgPlaceholder hue={generated.hue} width="100%" height="100%" label="Generated scene" style={{ borderRadius: 0 }} />
                </div>
              ) : (
                <div style={{ width: '100%', height: '100%', background: C.surface2, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 8 }}>
                  <Icon name="image" size={28} color={C.text3} />
                  <span style={{ fontSize: 12, color: C.text3 }}>Your image will appear here</span>
                </div>
              )}
            </div>

            {/* Metadata */}
            {generated && !generating && (
              <div style={{ background: C.surface2, borderRadius: 8, padding: '10px 12px', border: `1px solid ${C.border}` }}>
                <div style={{ fontSize: 11.5, fontWeight: 600, color: C.text2, marginBottom: 4 }}>Rationale</div>
                <p style={{ fontSize: 12, color: C.text3, lineHeight: 1.6 }}>{generated.rationale}</p>
              </div>
            )}

            {/* History strip */}
            {history.length > 1 && (
              <div>
                <div style={{ fontSize: 11, color: C.text3, marginBottom: 6 }}>History</div>
                <div style={{ display: 'flex', gap: 8 }}>
                  {history.map((h, i) => (
                    <div key={i} style={{ width: 56, height: 44, borderRadius: 6, overflow: 'hidden', cursor: 'pointer', border: `2px solid ${i === 0 ? C.amber : 'transparent'}`, transition: 'border-color 0.12s', flexShrink: 0 }}>
                      <ImgPlaceholder hue={h.hue + i * 20} width={56} height={44} style={{ borderRadius: 0 }} />
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>

          {/* Action footer */}
          <div style={{ padding: '12px 20px', borderTop: `1px solid ${C.border}`, display: 'flex', alignItems: 'center', gap: 8, flexShrink: 0 }}>
            <Btn variant="secondary" sz="sm" onClick={onClose}>Close</Btn>
            <Btn variant="outline" sz="sm" disabled={!generated}><Icon name="star" size={12} color={C.text2} />Save Default</Btn>
            <div style={{ flex: 1 }} />
            <Btn variant="secondary" sz="sm" onClick={handleGenerate} disabled={!prompt.trim() || generating}>
              <Icon name="refresh" size={12} color={C.text2} />Generate More
            </Btn>
            <Btn variant="primary" sz="sm" onClick={handleGenerate} disabled={!prompt.trim() || generating}>
              <Icon name="sparkle" size={12} color="#0d0b09" />
              {generating ? 'Generating…' : 'Generate'}
            </Btn>
            {generated && <Btn variant="blue" sz="sm" onClick={() => { onSaveToGallery(generated); onClose(); }}>
              <Icon name="download" size={12} color="white" />Save to Gallery
            </Btn>}
          </div>
        </div>
      </div>
    </Modal>
  );
}

// ─── ENTITY MANAGER ────────────────────────────────────────────────────────────
const ENTITY_TYPES = [
  { id: 'characters', label: 'Characters', icon: 'users' },
  { id: 'locations',  label: 'Locations',  icon: 'map-pin' },
  { id: 'items',      label: 'Items',      icon: 'box' },
  { id: 'timeline',   label: 'Timeline',   icon: 'clock' },
];

function CharacterEditor({ char, onSave, onDelete }) {
  const { confirmDelete, dialog: deleteDialog } = useConfirmDelete();
  const [data, setData] = useState({ ...char });
  const [view, setView] = useState('author');
  const upd = (k, v) => setData(d => ({ ...d, [k]: v }));

  const modelReady = `[Character: ${data.name}]\nName: ${data.name}\nSummary: ${data.summary}\n\nCore Personality: ${data.personality}\n\nGeneral Appearance: ${data.appearance}\n\nRelationships:\n${data.relationships}`;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      {/* Character header */}
      <div style={{ padding: '16px 20px', borderBottom: `1px solid ${C.border}`, display: 'flex', alignItems: 'flex-start', gap: 16, flexShrink: 0 }}>
        <div style={{ position: 'relative', cursor: 'pointer' }}>
          <Avatar name={data.name} size={64} />
          <div style={{ position: 'absolute', inset: 0, borderRadius: '50%', background: 'rgba(0,0,0,0)', display: 'flex', alignItems: 'center', justifyContent: 'center', transition: 'background 0.15s' }}
            onMouseEnter={e => e.currentTarget.style.background = 'rgba(0,0,0,0.45)'}
            onMouseLeave={e => e.currentTarget.style.background = 'rgba(0,0,0,0)'}>
            <Icon name="camera" size={16} color="white" style={{ opacity: 0.9 }} />
          </div>
        </div>
        <div style={{ flex: 1 }}>
          <div style={{ marginBottom: 8 }}>
            <input value={data.name} onChange={e => upd('name', e.target.value)}
              style={{ background: 'transparent', border: 'none', outline: 'none', fontSize: 18, fontWeight: 700, color: C.text, fontFamily: "'Playfair Display', serif", width: '100%' }} />
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div style={{ display: 'flex', gap: 4 }}>
              <Btn variant={view === 'author' ? 'secondary' : 'ghost'} sz="xs" onClick={() => setView('author')}>Author View</Btn>
              <Btn variant={view === 'model' ? 'secondary' : 'ghost'} sz="xs" onClick={() => setView('model')} style={{ color: view === 'model' ? C.blue : C.text3 }}>Model Context</Btn>
            </div>
            <div style={{ display: 'flex', gap: 4, marginLeft: 8, alignItems: 'center' }}>
              <div style={{ width: 7, height: 7, borderRadius: '50%', background: char.inScene ? C.emerald : C.text3 }} />
              <span style={{ fontSize: 11, color: C.text3 }}>{char.inScene ? 'In Scene' : 'Off Scene'}</span>
            </div>
          </div>
        </div>
      </div>

      {/* Editor body */}
      <div style={{ flex: 1, overflowY: 'auto', padding: '16px 20px' }}>
        {view === 'author' ? (
          <>
            <Field label="Summary" hint="1–2 sentences, plain language" onAI={() => {}}>
              <FTextarea value={data.summary} onChange={v => upd('summary', v)} placeholder="Who is this character in one or two sentences?" rows={2} />
            </Field>
            <Field label="Core Personality" hint="2–4 sentences, descriptive" onAI={() => {}}>
              <FTextarea value={data.personality} onChange={v => upd('personality', v)} placeholder="How does this character think, feel, and behave?" rows={3} />
            </Field>
            <Field label="General Appearance" hint="1–2 sentences, physical details" onAI={() => {}}>
              <FTextarea value={data.appearance} onChange={v => upd('appearance', v)} placeholder="What do they look like in general?" rows={2} />
            </Field>
            <Field label="Relationships" hint="Bullet list, 2–5 items" onAI={() => {}}>
              <FTextarea value={data.relationships} onChange={v => upd('relationships', v)} placeholder="- Name: relationship description" rows={3} />
            </Field>
            <Field label="Backstory" hint="Optional, paragraph form" onAI={() => {}}>
              <FTextarea value={data.backstory} onChange={v => upd('backstory', v)} placeholder="Key history that shapes who they are now…" rows={3} />
            </Field>
            <Field label="Voice & Speech" hint="1–2 sentences" onAI={() => {}}>
              <FTextarea value={data.voice} onChange={v => upd('voice', v)} placeholder="How do they speak, what phrases, what cadence?" rows={2} />
            </Field>
            <Field label="Notes">
              <FTextarea value={data.notes} onChange={v => upd('notes', v)} placeholder="Private author notes, continuity reminders…" rows={2} />
            </Field>
          </>
        ) : (
          <div>
            <div style={{ fontSize: 11, color: C.text3, marginBottom: 10, lineHeight: 1.6 }}>This is the prompt context passed to the model. It is assembled automatically from your Author View fields.</div>
            <pre style={{
              background: C.surface2, border: `1px solid ${C.border}`, borderRadius: 8,
              padding: '14px 16px', fontSize: 12, fontFamily: "'DM Mono', monospace",
              color: C.text2, lineHeight: 1.7, whiteSpace: 'pre-wrap', wordBreak: 'break-word',
            }}>{modelReady}</pre>
            <Btn variant="ghost" sz="sm" style={{ marginTop: 10, color: C.text3 }}>
              <Icon name="copy" size={12} color={C.text3} />Copy to clipboard
            </Btn>
          </div>
        )}
      </div>

      {/* Save footer */}
      <div style={{ padding: '12px 20px', borderTop: `1px solid ${C.border}`, display: 'flex', gap: 8, flexShrink: 0 }}>
        <Btn variant="danger" sz="sm" onClick={() => confirmDelete({ title: `Delete "${data.name}"?`, body: 'This character will be permanently removed from your story.', onConfirm: onDelete })}><Icon name="trash" size={12} />Delete</Btn>
        <div style={{ flex: 1 }} />
        <Btn variant="secondary" sz="sm">Discard</Btn>
        <Btn variant="primary" sz="sm" onClick={() => onSave(data)}>
          <Icon name="check" size={12} color="#0d0b09" />Save
        </Btn>
      </div>
      {deleteDialog}
    </div>
  );
}

function LocationEditor({ loc, onSave, onDelete }) {
  const { confirmDelete, dialog: deleteDialog } = useConfirmDelete();
  const [data, setData] = useState({ ...loc });
  const upd = (k, v) => setData(d => ({ ...d, [k]: v }));
  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div style={{ padding: '16px 20px', borderBottom: `1px solid ${C.border}`, display: 'flex', alignItems: 'center', gap: 14, flexShrink: 0 }}>
        <div style={{ width: 64, height: 64, borderRadius: 12, overflow: 'hidden', cursor: 'pointer', border: `1px solid ${C.border}`, flexShrink: 0 }}>
          <ImgPlaceholder hue={210} width={64} height={64} label="loc" style={{ borderRadius: 0 }} />
        </div>
        <div style={{ flex: 1 }}>
          <input value={data.name} onChange={e => upd('name', e.target.value)}
            style={{ background: 'transparent', border: 'none', outline: 'none', fontSize: 18, fontWeight: 700, color: C.text, fontFamily: "'Playfair Display', serif", width: '100%' }} />
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 4 }}>
            <div style={{ width: 7, height: 7, borderRadius: '50%', background: loc.isActive ? C.emerald : C.text3 }} />
            <span style={{ fontSize: 11, color: C.text3 }}>{loc.isActive ? 'Active Location' : 'Inactive'}</span>
          </div>
        </div>
      </div>
      <div style={{ flex: 1, overflowY: 'auto', padding: '16px 20px' }}>
        <Field label="Summary" hint="1–2 sentences" onAI={() => {}}><FTextarea value={data.summary} onChange={v => upd('summary', v)} rows={2} /></Field>
        <Field label="Description" hint="Physical description" onAI={() => {}}><FTextarea value={data.description} onChange={v => upd('description', v)} rows={3} /></Field>
        <Field label="Atmosphere" hint="Mood and feeling" onAI={() => {}}><FTextarea value={data.atmosphere} onChange={v => upd('atmosphere', v)} rows={2} /></Field>
        <Field label="Notable Features" hint="Bullet list" onAI={() => {}}><FTextarea value={data.features} onChange={v => upd('features', v)} rows={3} /></Field>
      </div>
      <div style={{ padding: '12px 20px', borderTop: `1px solid ${C.border}`, display: 'flex', gap: 8, flexShrink: 0 }}>
        <Btn variant="danger" sz="sm" onClick={() => confirmDelete({ title: `Delete "${data.name}"?`, body: 'This location will be permanently removed from your story.', onConfirm: onDelete })}><Icon name="trash" size={12} />Delete</Btn>
        <div style={{ flex: 1 }} />
        <Btn variant="secondary" sz="sm">Discard</Btn>
        <Btn variant="primary" sz="sm" onClick={() => onSave(data)}><Icon name="check" size={12} color="#0d0b09" />Save</Btn>
      </div>
      {deleteDialog}
    </div>
  );
}

function ItemEditor({ item, onSave, onDelete }) {
  const { confirmDelete, dialog: deleteDialog } = useConfirmDelete();
  const [data, setData] = useState({ ...item });
  const upd = (k, v) => setData(d => ({ ...d, [k]: v }));
  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div style={{ padding: '16px 20px', borderBottom: `1px solid ${C.border}`, display: 'flex', alignItems: 'center', gap: 14, flexShrink: 0 }}>
        <div style={{ width: 64, height: 64, borderRadius: 12, overflow: 'hidden', cursor: 'pointer', border: `1px solid ${C.border}`, flexShrink: 0 }}>
          <ImgPlaceholder hue={200} width={64} height={64} label="item" style={{ borderRadius: 0 }} />
        </div>
        <div style={{ flex: 1 }}>
          <input value={data.name} onChange={e => upd('name', e.target.value)}
            style={{ background: 'transparent', border: 'none', outline: 'none', fontSize: 18, fontWeight: 700, color: C.text, fontFamily: "'Playfair Display', serif", width: '100%' }} />
          <div style={{ fontSize: 11, color: C.text3, marginTop: 4 }}>Scene Item</div>
        </div>
      </div>
      <div style={{ flex: 1, overflowY: 'auto', padding: '16px 20px' }}>
        <Field label="Summary" onAI={() => {}}><FTextarea value={data.summary} onChange={v => upd('summary', v)} rows={2} /></Field>
        <Field label="Description" hint="Physical details" onAI={() => {}}><FTextarea value={data.description} onChange={v => upd('description', v)} rows={3} /></Field>
        <Field label="History" hint="How did it come to be here?" onAI={() => {}}><FTextarea value={data.history} onChange={v => upd('history', v)} rows={2} /></Field>
        <Field label="Properties" hint="Key/value list" onAI={() => {}}><FTextarea value={data.properties} onChange={v => upd('properties', v)} rows={3} /></Field>
      </div>
      <div style={{ padding: '12px 20px', borderTop: `1px solid ${C.border}`, display: 'flex', gap: 8, flexShrink: 0 }}>
        <Btn variant="danger" sz="sm" onClick={() => confirmDelete({ title: `Delete "${data.name}"?`, body: 'This item will be permanently removed from your story.', onConfirm: onDelete })}><Icon name="trash" size={12} />Delete</Btn>
        <div style={{ flex: 1 }} />
        <Btn variant="secondary" sz="sm">Discard</Btn>
        <Btn variant="primary" sz="sm" onClick={() => onSave(data)}><Icon name="check" size={12} color="#0d0b09" />Save</Btn>
      </div>
      {deleteDialog}
    </div>
  );
}

function TimelineEditor({ entry, onSave, onDelete }) {
  const { confirmDelete, dialog: deleteDialog } = useConfirmDelete();
  const [data, setData] = useState({ ...entry });
  const upd = (k, v) => setData(d => ({ ...d, [k]: v }));
  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div style={{ padding: '16px 20px', borderBottom: `1px solid ${C.border}`, flexShrink: 0 }}>
        <input value={data.title} onChange={e => upd('title', e.target.value)}
          style={{ background: 'transparent', border: 'none', outline: 'none', fontSize: 18, fontWeight: 700, color: C.text, fontFamily: "'Playfair Display', serif", width: '100%' }} />
        <div style={{ fontSize: 12, color: C.text3, marginTop: 4 }}>{data.date}</div>
      </div>
      <div style={{ flex: 1, overflowY: 'auto', padding: '16px 20px' }}>
        <Field label="Date / Time"><FInput value={data.date} onChange={v => upd('date', v)} placeholder="e.g. Apr 26, 2026 · Evening" /></Field>
        <Field label="Description" hint="What happened?" onAI={() => {}}><FTextarea value={data.description} onChange={v => upd('description', v)} rows={4} /></Field>
        <Field label="Characters Involved" hint="Comma-separated names"><FInput value={(data.characters||[]).join(', ')} onChange={v => upd('characters', v.split(',').map(s => s.trim()))} /></Field>
        <Field label="Significance" hint="Why does this matter to the story?" onAI={() => {}}><FTextarea value={data.significance} onChange={v => upd('significance', v)} rows={2} /></Field>
      </div>
      <div style={{ padding: '12px 20px', borderTop: `1px solid ${C.border}`, display: 'flex', gap: 8, flexShrink: 0 }}>
        <Btn variant="danger" sz="sm" onClick={() => confirmDelete({ title: `Delete "${data.title}"?`, body: 'This timeline event will be permanently removed.', onConfirm: onDelete })}><Icon name="trash" size={12} />Delete</Btn>
        <div style={{ flex: 1 }} />
        <Btn variant="secondary" sz="sm">Discard</Btn>
        <Btn variant="primary" sz="sm" onClick={() => onSave(data)}><Icon name="check" size={12} color="#0d0b09" />Save</Btn>
      </div>
      {deleteDialog}
    </div>
  );
}

function EntityManagerModal({ initialType = 'characters', initialId, chars, locs, items, timeline, onClose, onUpdate }) {
  const [type, setType] = useState(initialType);
  const [selectedId, setSelectedId] = useState(initialId || (initialType === 'characters' ? chars[0]?.id : initialType === 'locations' ? locs[0]?.id : initialType === 'items' ? items[0]?.id : timeline[0]?.id));
  const [hovId, setHovId] = useState(null);
  const [charPicker, setCharPicker] = useState(false);
  const [v2Wizard, setV2Wizard]     = useState(null); // { char, isNew }

  const lists = { characters: chars, locations: locs, items, timeline };
  const list = lists[type] || [];

  const ICONS = { characters: 'users', locations: 'map-pin', items: 'box', timeline: 'clock' };

  const selected = list.find(e => e.id === selectedId);

  function addNew() {
    if (type === 'characters') { setCharPicker(true); return; }
    const id = `new-${Date.now()}`;
    const templates = {
      locations:  { id, name: 'New Location',  summary: '', description: '', atmosphere: '', features: '', isActive: false },
      items:      { id, name: 'New Item',       summary: '', description: '', history: '', properties: '', inScene: false },
      timeline:   { id, title: 'New Event', date: '', description: '', characters: [], significance: '' },
    };
    onUpdate(type, [{ ...templates[type] }, ...list]);
    setSelectedId(id);
  }

  function handlePickV1() {
    setCharPicker(false);
    const id = `new-${Date.now()}`;
    const newChar = { id, version: 'v1', name: 'New Character', summary: '', personality: '', appearance: '', relationships: '', backstory: '', voice: '', notes: '', inScene: false };
    onUpdate('characters', [newChar, ...chars]);
    setSelectedId(id);
  }

  function handlePickV2() {
    setCharPicker(false);
    setV2Wizard({ char: emptyV2Char('c-v2-' + Date.now()), isNew: true });
  }

  function handleV2Save(data) {
    const wasNew = v2Wizard?.isNew;
    setV2Wizard(null);
    if (wasNew) {
      onUpdate('characters', [data, ...chars]);
      setSelectedId(data.id);
    } else {
      onUpdate('characters', chars.map(c => c.id === data.id ? data : c));
    }
  }

  function toggleInScene(e, entityId) {
    e.stopPropagation();
    if (type === 'characters') {
      onUpdate('characters', chars.map(c => c.id === entityId ? { ...c, inScene: !c.inScene } : c));
    } else if (type === 'locations') {
      onUpdate('locations', locs.map(l => ({ ...l, isActive: l.id === entityId })));
    } else if (type === 'items') {
      onUpdate('items', items.map(i => i.id === entityId ? { ...i, inScene: !i.inScene } : i));
    }
  }

  const hasToggle = type === 'characters' || type === 'locations' || type === 'items';

  return (
    <>
    <Modal onClose={onClose} maxW={960}>
      <ModalHeader title="Story Entities" onClose={onClose} />

      {/* Type tabs */}
      <div style={{ display: 'flex', gap: 0, borderBottom: `1px solid ${C.border}`, padding: '0 20px', flexShrink: 0 }}>
        {ENTITY_TYPES.map(t => (
          <div key={t.id} onClick={() => { setType(t.id); setSelectedId(lists[t.id]?.[0]?.id); }}
            style={{
              padding: '10px 14px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 6,
              borderBottom: `2px solid ${type === t.id ? C.amber : 'transparent'}`,
              color: type === t.id ? C.text : C.text3,
              fontSize: 13, fontWeight: type === t.id ? 600 : 400,
              transition: 'color 0.12s',
            }}>
            <Icon name={t.icon} size={13} color={type === t.id ? C.amber : C.text3} />
            {t.label}
            <span style={{ fontSize: 11, fontFamily: "'DM Mono', monospace", color: type === t.id ? C.amberDim : C.text3 }}>{lists[t.id]?.length || 0}</span>
          </div>
        ))}
      </div>

      <div style={{ display: 'flex', flex: 1, minHeight: 0, height: 580 }}>
        {/* Entity list */}
        <div style={{ width: 260, borderRight: `1px solid ${C.border}`, display: 'flex', flexDirection: 'column', flexShrink: 0 }}>
          <div style={{ padding: '10px 12px 6px', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexShrink: 0 }}>
            <span style={{ fontSize: 11, fontWeight: 700, letterSpacing: '0.06em', textTransform: 'uppercase', color: C.text3 }}>
              {ENTITY_TYPES.find(t => t.id === type)?.label}
            </span>
            <Btn variant="ghost" sz="icon" onClick={addNew} style={{ padding: 4 }}>
              <Icon name="plus" size={13} color={C.amber} />
            </Btn>
          </div>
          <div style={{ flex: 1, overflowY: 'auto' }}>
            {list.map(e => {
              const name = e.name || e.title || 'Untitled';
              const sub = type === 'characters' ? e.summary : type === 'locations' ? e.summary : type === 'items' ? e.summary : e.date;
              const isActive = type === 'locations' ? e.isActive : type === 'characters' ? e.inScene : type === 'items' ? e.inScene : false;
              const isSel = e.id === selectedId;
              const isHov = e.id === hovId;
              return (
                <div key={e.id} onClick={() => setSelectedId(e.id)}
                  onMouseEnter={() => setHovId(e.id)}
                  onMouseLeave={() => setHovId(null)}
                  style={{
                    display: 'flex', alignItems: 'center', gap: 10, padding: '9px 10px 9px 12px',
                    cursor: 'pointer', margin: '1px 6px', borderRadius: 8,
                    background: isSel ? `${C.amber}18` : isHov ? C.surface3 : 'transparent',
                    border: `1px solid ${isSel ? C.amberDim : 'transparent'}`,
                    transition: 'all 0.12s',
                  }}>
                  {/* Avatar / icon with in-scene indicator */}
                  <div style={{ position: 'relative', flexShrink: 0 }}>
                    {type === 'characters' ? <Avatar name={name} size={34} /> :
                     type === 'timeline' ? (
                      <div style={{ width: 34, height: 34, borderRadius: 8, background: C.surface3, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                        <Icon name="clock" size={15} color={C.text3} />
                      </div>
                    ) : (
                      <div style={{ width: 34, height: 34, borderRadius: 8, overflow: 'hidden' }}>
                        <ImgPlaceholder hue={type === 'locations' ? 210 : 200} width={34} height={34} style={{ borderRadius: 0 }} />
                      </div>
                    )}
                    {isActive && (
                      <div style={{
                        position: 'absolute', bottom: 1, right: 1,
                        width: 9, height: 9, borderRadius: '50%',
                        background: type === 'locations' ? C.blue : C.emerald,
                        border: `1.5px solid ${C.surface}`,
                      }} />
                    )}
                  </div>

                  <div style={{ flex: 1, minWidth: 0 }}>
                    <span style={{ fontSize: 13, fontWeight: 500, color: isSel ? C.text : C.text2, display: 'block', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{name}</span>
                    {sub && <div style={{ fontSize: 11, color: C.text3, marginTop: 2, display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden', lineHeight: 1.45 }}>{sub}</div>}
                  </div>

                  {/* In-scene toggle — only on hover */}
                  {hasToggle && isHov && (
                    <Btn variant="ghost" sz="icon"
                      onClick={ev => toggleInScene(ev, e.id)}
                      title={isActive
                        ? (type === 'locations' ? 'Active location' : 'Remove from scene')
                        : (type === 'locations' ? 'Set as active location' : 'Add to scene')}
                      style={{ padding: 4, flexShrink: 0 }}>
                      <Icon
                        name={type === 'locations' ? 'map-pin' : 'eye'}
                        size={13}
                        color={isActive ? (type === 'locations' ? C.blue : C.emerald) : C.text3}
                      />
                    </Btn>
                  )}
                </div>
              );
            })}
          </div>
        </div>

        {/* Editor */}
        <div style={{ flex: 1, minWidth: 0 }}>
          {selected ? (
            type === 'characters' && selected.version === 'v2'
              ? <CharacterEditorV2
                  key={selected.id}
                  char={selected}
                  existingChars={chars}
                  onSave={data => onUpdate('characters', chars.map(c => c.id === data.id ? data : c))}
                  onOpenWizard={() => setV2Wizard({ char: selected, isNew: false })}
                />
              : type === 'characters' ? <CharacterEditor char={selected} onSave={data => onUpdate('characters', chars.map(c => c.id === data.id ? data : c))} onDelete={() => { const next = chars.filter(c => c.id !== selected.id); onUpdate('characters', next); setSelectedId(next[0]?.id || null); }} /> :
            type === 'locations'  ? <LocationEditor loc={selected}   onSave={data => onUpdate('locations', locs.map(l => l.id === data.id ? data : l))} onDelete={() => { const next = locs.filter(l => l.id !== selected.id); onUpdate('locations', next); setSelectedId(next[0]?.id || null); }} /> :
            type === 'items'      ? <ItemEditor item={selected}       onSave={data => onUpdate('items', items.map(i => i.id === data.id ? data : i))} onDelete={() => { const next = items.filter(i => i.id !== selected.id); onUpdate('items', next); setSelectedId(next[0]?.id || null); }} /> :
            type === 'timeline'   ? <TimelineEditor entry={selected}  onSave={data => onUpdate('timeline', timeline.map(t => t.id === data.id ? data : t))} onDelete={() => { const next = timeline.filter(t => t.id !== selected.id); onUpdate('timeline', next); setSelectedId(next[0]?.id || null); }} /> :
            null
          ) : (
            <div style={{ height: '100%', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 12, padding: 40 }}>
              <Icon name={ICONS[type]} size={32} color={C.text3} />
              <span style={{ fontSize: 13, color: C.text3 }}>Select an entity to edit, or add a new one</span>
              <Btn variant="secondary" sz="sm" onClick={addNew}><Icon name="plus" size={12} color={C.text2} />Add {ENTITY_TYPES.find(t => t.id === type)?.label.slice(0,-1)}</Btn>
            </div>
          )}
        </div>
      </div>
    </Modal>

    {/* ── New character picker ── */}
    {charPicker && (
      <NewCharPickerModal
        onPickV1={handlePickV1}
        onPickV2={handlePickV2}
        onClose={() => setCharPicker(false)}
      />
    )}

    {/* ── V2 wizard ── */}
    {v2Wizard && (
      <V2WizardModal
        initialChar={v2Wizard.char}
        existingChars={chars}
        onSave={handleV2Save}
        onClose={() => setV2Wizard(null)}
      />
    )}
    </>
  );
}

// ─── EXPORT MODAL ─────────────────────────────────────────────────────────────
const EXPORT_FORMATS = [
  { id: 'markdown', label: 'Markdown', ext: '.md',   icon: 'align-left' },
  { id: 'json',     label: 'JSON',     ext: '.json', icon: 'layers'     },
  { id: 'txt',      label: 'Plain Text', ext: '.txt', icon: 'book-open' },
];

const INCLUDE_ITEMS = [
  { id: 'transcript', label: 'Chat Transcript',  icon: 'message-sq', note: 'Full message history', requiresAll: true },
  { id: 'timeline',   label: 'History / Timeline', icon: 'clock',     note: 'Chronological events'  },
  { id: 'characters', label: 'Characters',         icon: 'users',     note: 'Profiles & context'    },
  { id: 'locations',  label: 'Locations',          icon: 'map-pin',   note: 'Descriptions & details' },
  { id: 'items',      label: 'Items',              icon: 'box',       note: 'Scene props & objects'  },
];

function ExportCheckbox({ checked, onChange, indeterminate }) {
  const ref = useRef(null);
  useEffect(() => { if (ref.current) ref.current.indeterminate = !!indeterminate; }, [indeterminate]);
  return (
    <div onClick={onChange} style={{
      width: 18, height: 18, borderRadius: 5, flexShrink: 0, cursor: 'pointer',
      background: checked ? C.amber : C.surface3,
      border: `1.5px solid ${checked ? C.amber : C.borderMid}`,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      transition: 'all 0.12s',
    }}>
      {checked && <Icon name="check" size={11} color="#0d0b09" />}
      {!checked && indeterminate && <div style={{ width: 8, height: 2, borderRadius: 1, background: C.text3 }} />}
    </div>
  );
}

function ExportModal({ chars, locs, items, timeline, chats, activeChat, onClose }) {
  const [format, setFormat]   = useState('markdown');
  const [included, setIncluded] = useState({ transcript: false, timeline: true, characters: true, locations: true, items: true });
  const [images, setImages]   = useState('referenced'); // 'none' | 'referenced' | 'all'

  // If transcript is toggled on, force-enable everything
  function toggle(id) {
    if (id === 'transcript') {
      const next = !included.transcript;
      if (next) setIncluded({ transcript: true, timeline: true, characters: true, locations: true, items: true });
      else      setIncluded(prev => ({ ...prev, transcript: false }));
    } else {
      setIncluded(prev => {
        const next = { ...prev, [id]: !prev[id] };
        // If any sub-item is unchecked, uncheck transcript
        if (!next[id]) next.transcript = false;
        return next;
      });
    }
  }

  const allSubChecked = included.timeline && included.characters && included.locations && included.items;
  const someSubChecked = included.timeline || included.characters || included.locations || included.items;
  const transcriptIndeterminate = !included.transcript && someSubChecked && !allSubChecked;

  // Compute estimated size
  const counts = {
    transcript: activeChat ? '1 chat' : `${chats.length} chats`,
    timeline:   `${timeline.length} events`,
    characters: `${chars.length} characters`,
    locations:  `${locs.length} locations`,
    items:      `${items.length} items`,
  };

  const activeItems = INCLUDE_ITEMS.filter(it => included[it.id]);
  const imgLabel = images === 'none' ? 'No images' : images === 'referenced' ? 'Referenced images only' : 'All images';
  const fmt = EXPORT_FORMATS.find(f => f.id === format);

  const summaryLines = [
    ...activeItems.map(it => `${it.label} · ${counts[it.id]}`),
    images !== 'none' ? imgLabel : null,
  ].filter(Boolean);

  function handleExport() {
    // Placeholder — real export logic would go here
    alert(`Exporting as ${fmt.label} (${fmt.ext})\n\n${summaryLines.join('\n')}`);
    onClose();
  }

  return (
    <Modal onClose={onClose} maxW={680}>
      <ModalHeader title="Export" subtitle="Choose what to include in your export file" onClose={onClose} />

      <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>

        {/* ── Left: Options ── */}
        <div style={{ flex: 1, overflowY: 'auto', padding: '20px 24px', display: 'flex', flexDirection: 'column', gap: 24 }}>

          {/* Format */}
          <div>
            <div style={{ fontSize: 11, fontWeight: 700, letterSpacing: '0.09em', textTransform: 'uppercase', color: C.text3, marginBottom: 10 }}>Format</div>
            <div style={{ display: 'flex', gap: 8 }}>
              {EXPORT_FORMATS.map(f => {
                const active = format === f.id;
                return (
                  <div key={f.id} onClick={() => setFormat(f.id)} style={{
                    flex: 1, padding: '10px 12px', borderRadius: 9, cursor: 'pointer',
                    border: `1.5px solid ${active ? C.amber : C.border}`,
                    background: active ? `${C.amber}12` : C.surface2,
                    transition: 'all 0.12s',
                    display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6,
                  }}>
                    <Icon name={f.icon} size={16} color={active ? C.amber : C.text3} />
                    <span style={{ fontSize: 12.5, fontWeight: 600, color: active ? C.text : C.text2 }}>{f.label}</span>
                    <span style={{ fontSize: 10.5, fontFamily: "'DM Mono', monospace", color: active ? C.amberDim : C.text3 }}>{f.ext}</span>
                  </div>
                );
              })}
            </div>
          </div>

          {/* Include */}
          <div>
            <div style={{ fontSize: 11, fontWeight: 700, letterSpacing: '0.09em', textTransform: 'uppercase', color: C.text3, marginBottom: 10 }}>Include</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              {INCLUDE_ITEMS.map((it, idx) => {
                const checked = included[it.id];
                const isTranscript = it.id === 'transcript';
                return (
                  <div key={it.id}>
                    <div onClick={() => toggle(it.id)} style={{
                      display: 'flex', alignItems: 'center', gap: 12, padding: '9px 12px',
                      borderRadius: 8, cursor: 'pointer',
                      background: checked ? `${C.amber}0e` : 'transparent',
                      border: `1px solid ${checked ? `${C.amber}30` : 'transparent'}`,
                      transition: 'all 0.12s',
                    }}>
                      <ExportCheckbox
                        checked={checked}
                        indeterminate={isTranscript ? transcriptIndeterminate : false}
                        onChange={() => toggle(it.id)}
                      />
                      <div style={{ width: 28, height: 28, borderRadius: 7, background: checked ? `${C.amber}20` : C.surface3, display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0, transition: 'background 0.12s' }}>
                        <Icon name={it.icon} size={13} color={checked ? C.amber : C.text3} />
                      </div>
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{ fontSize: 13, fontWeight: 500, color: checked ? C.text : C.text2 }}>{it.label}</div>
                        <div style={{ fontSize: 11, color: C.text3, marginTop: 1 }}>{it.note}</div>
                      </div>
                      <span style={{ fontSize: 11, fontFamily: "'DM Mono', monospace", color: C.text3, flexShrink: 0 }}>{counts[it.id]}</span>
                    </div>
                    {/* Transcript note */}
                    {isTranscript && checked && (
                      <div style={{ margin: '2px 12px 4px 52px', fontSize: 11, color: C.amberDim, display: 'flex', alignItems: 'center', gap: 5 }}>
                        <Icon name="zap" size={10} color={C.amberDim} />
                        All sections below are required for a full transcript export
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>

          {/* Images */}
          <div>
            <div style={{ fontSize: 11, fontWeight: 700, letterSpacing: '0.09em', textTransform: 'uppercase', color: C.text3, marginBottom: 10 }}>Images</div>
            <div style={{ display: 'flex', gap: 8 }}>
              {[
                { id: 'none',       label: 'No Images',       sub: 'Text only',    icon: 'x'      },
                { id: 'referenced', label: 'Referenced',      sub: 'Cited only',   icon: 'flag'   },
                { id: 'all',        label: 'All Images',      sub: 'Full gallery', icon: 'image'  },
              ].map(opt => {
                const active = images === opt.id;
                return (
                  <div key={opt.id} onClick={() => setImages(opt.id)} style={{
                    flex: 1, padding: '10px 12px', borderRadius: 9, cursor: 'pointer',
                    border: `1.5px solid ${active ? C.amber : C.border}`,
                    background: active ? `${C.amber}12` : C.surface2,
                    transition: 'all 0.12s',
                    display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6,
                  }}>
                    <Icon name={opt.icon} size={16} color={active ? C.amber : C.text3} />
                    <span style={{ fontSize: 12.5, fontWeight: 600, color: active ? C.text : C.text2 }}>{opt.label}</span>
                    <span style={{ fontSize: 10.5, color: active ? C.amberDim : C.text3 }}>{opt.sub}</span>
                  </div>
                );
              })}
            </div>
          </div>
        </div>

        {/* ── Right: Summary ── */}
        <div style={{ width: 220, borderLeft: `1px solid ${C.border}`, background: C.surface2, display: 'flex', flexDirection: 'column', flexShrink: 0 }}>
          <div style={{ padding: '20px 18px', flex: 1 }}>
            <div style={{ fontSize: 11, fontWeight: 700, letterSpacing: '0.09em', textTransform: 'uppercase', color: C.text3, marginBottom: 14 }}>Summary</div>

            {/* Format badge */}
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 10px', borderRadius: 8, background: `${C.amber}15`, border: `1px solid ${C.amber}30`, marginBottom: 16 }}>
              <Icon name={fmt.icon} size={13} color={C.amber} />
              <div>
                <div style={{ fontSize: 12.5, fontWeight: 600, color: C.text }}>{fmt.label}</div>
                <div style={{ fontSize: 10.5, fontFamily: "'DM Mono', monospace", color: C.amberDim }}>{fmt.ext}</div>
              </div>
            </div>

            {summaryLines.length > 0 ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                {summaryLines.map((line, i) => (
                  <div key={i} style={{ display: 'flex', alignItems: 'flex-start', gap: 7 }}>
                    <Icon name="check" size={11} color={C.emerald} style={{ marginTop: 2, flexShrink: 0 }} />
                    <span style={{ fontSize: 12, color: C.text2, lineHeight: 1.45 }}>{line}</span>
                  </div>
                ))}
              </div>
            ) : (
              <div style={{ fontSize: 12, color: C.text3, fontStyle: 'italic' }}>Nothing selected yet</div>
            )}
          </div>

          <div style={{ padding: '0 18px 20px' }}>
            <div style={{ height: 1, background: C.border, marginBottom: 16 }} />
            <div style={{ fontSize: 10.5, color: C.text3, lineHeight: 1.6, marginBottom: 14 }}>
              {`Exporting "${activeChat?.title || 'active chat'}"`}
            </div>
            <Btn variant="primary" sz="md"
              disabled={summaryLines.length === 0}
              onClick={handleExport}
              style={{ width: '100%', justifyContent: 'center' }}>
              <Icon name="download" size={13} color="#0d0b09" />
              Export
            </Btn>
          </div>
        </div>
      </div>
    </Modal>
  );
}

// ─── AI PROVIDERS MODAL ───────────────────────────────────────────────────────

const AI_PROVIDERS_META = {
  openai: {
    id: 'openai', name: 'OpenAI',
    desc: 'GPT-4o, o3, and the full OpenAI model suite.',
    keyLabel: 'OpenAI API Key', keyLink: 'https://platform.openai.com/api-keys',
    needs: ['apiKey'], apiKeyRequired: true, endpointRequired: false,
    sampleModels: [
      { id: 'gpt-4o', text: true, image: true },
      { id: 'gpt-4o-mini', text: true, image: true },
      { id: 'o3', text: true, image: false },
      { id: 'o4-mini', text: true, image: false },
      { id: 'gpt-4-turbo', text: true, image: true },
      { id: 'gpt-3.5-turbo', text: true, image: false },
      { id: 'dall-e-3', text: false, image: true },
    ]
  },
  grok: {
    id: 'grok', name: 'Grok / xAI',
    desc: 'xAI Grok models including vision and reasoning variants.',
    keyLabel: 'xAI API Key', keyLink: 'https://console.x.ai',
    needs: ['apiKey'], apiKeyRequired: true, endpointRequired: false,
    sampleModels: [
      { id: 'grok-4-1-fast-non-reasoning', text: true, image: false },
      { id: 'grok-4-0709', text: true, image: false },
      { id: 'grok-4.20-0309', text: true, image: true },
      { id: 'grok-4.20-0309-non-reasoning', text: true, image: false },
      { id: 'grok-4.20-0309-reasoning', text: true, image: false },
      { id: 'grok-vision-beta', text: true, image: true },
    ]
  },
  claude: {
    id: 'claude', name: 'Claude / Anthropic',
    desc: 'Claude Opus, Sonnet, and Haiku model families.',
    keyLabel: 'Anthropic API Key', keyLink: 'https://console.anthropic.com/settings/keys',
    needs: ['apiKey'], apiKeyRequired: true, endpointRequired: false,
    sampleModels: [
      { id: 'claude-opus-4-5', text: true, image: true },
      { id: 'claude-sonnet-4-5', text: true, image: true },
      { id: 'claude-haiku-4-5', text: true, image: true },
      { id: 'claude-3-5-sonnet-20241022', text: true, image: true },
      { id: 'claude-3-haiku-20240307', text: true, image: false },
    ]
  },
  huggingface: {
    id: 'huggingface', name: 'Hugging Face',
    desc: 'Managed HF Inference Endpoints for open-weight models.',
    keyLabel: 'HF Access Token', keyLink: 'https://huggingface.co/settings/tokens',
    needs: ['endpoint', 'apiKey'], apiKeyRequired: true, endpointRequired: false,
    sampleModels: [
      { id: 'meta-llama/Meta-Llama-3.1-8B-Instruct', text: true, image: false },
      { id: 'mistralai/Mixtral-8x7B-Instruct-v0.1', text: true, image: false },
      { id: 'stabilityai/stable-diffusion-xl-base-1.0', text: false, image: true },
    ]
  },
  compatible: {
    id: 'compatible', name: 'OpenAI-compatible',
    desc: 'LM Studio, Ollama, or any OpenAI-compatible gateway.',
    keyLabel: 'API Key', keyLink: null,
    needs: ['endpoint', 'apiKey'], apiKeyRequired: false, endpointRequired: true,
    sampleModels: []
  },
};

const INITIAL_AI_PROVIDERS = [
  {
    id: 'ap1', type: 'grok', name: 'Grok / xAI', enabled: true,
    apiKey: 'xai-••••••••••••••••••••••••••••••••',
    models: [
      { id: 'grok-4-1-fast-non-reasoning',  enabled: true,  text: true,  image: false },
      { id: 'grok-4-0709',                  enabled: true,  text: true,  image: false },
      { id: 'grok-4.20-0309',               enabled: true,  text: true,  image: true  },
      { id: 'grok-4.20-0309-non-reasoning', enabled: false, text: true,  image: false },
      { id: 'grok-4.20-0309-reasoning',     enabled: false, text: true,  image: false },
      { id: 'grok-vision-beta',             enabled: false, text: true,  image: true  },
    ]
  },
  {
    id: 'ap2', type: 'huggingface', name: 'Hugging Face Endpoints', enabled: true,
    apiKey: 'hf_••••••••••••••••••••••••••••••••',
    endpoint: 'https://api-inference.huggingface.co',
    models: [
      { id: 'meta-llama/Meta-Llama-3.1-8B-Instruct',       enabled: true,  text: true,  image: false },
      { id: 'mistralai/Mixtral-8x7B-Instruct-v0.1',         enabled: false, text: true,  image: false },
      { id: 'stabilityai/stable-diffusion-xl-base-1.0',     enabled: false, text: false, image: true  },
    ]
  },
  {
    id: 'ap3', type: 'openai', name: 'OpenAI', enabled: true,
    apiKey: 'sk-••••••••••••••••••••••••••••••••',
    models: [
      { id: 'gpt-4o',        enabled: true,  text: true,  image: true  },
      { id: 'gpt-4o-mini',   enabled: true,  text: true,  image: true  },
      { id: 'o3',            enabled: true,  text: true,  image: false },
      { id: 'o4-mini',       enabled: false, text: true,  image: false },
      { id: 'gpt-4-turbo',   enabled: false, text: true,  image: true  },
      { id: 'gpt-3.5-turbo', enabled: false, text: true,  image: false },
      { id: 'dall-e-3',      enabled: false, text: false, image: true  },
    ]
  },
];

function KeyInput({ value, onChange, placeholder }) {
  const [show, setShow] = useState(false);
  const [focused, setFocused] = useState(false);
  return (
    <div style={{ position: 'relative' }}>
      <input
        type={show ? 'text' : 'password'}
        value={value} onChange={e => onChange(e.target.value)}
        placeholder={placeholder || 'sk-…'}
        onFocus={() => setFocused(true)} onBlur={() => setFocused(false)}
        style={{
          width: '100%', background: C.surface3,
          border: `1px solid ${focused ? C.amberDim : C.border}`,
          borderRadius: 7, padding: '7px 34px 7px 10px',
          color: C.text, fontSize: 12.5,
          fontFamily: "'DM Mono', monospace",
          outline: 'none', transition: 'border-color 0.15s',
          letterSpacing: '0.02em',
        }}
      />
      <button onClick={() => setShow(s => !s)} style={{
        position: 'absolute', right: 8, top: '50%', transform: 'translateY(-50%)',
        background: 'none', border: 'none', cursor: 'pointer', padding: 2,
        display: 'flex', alignItems: 'center',
      }}>
        <Icon name="eye" size={13} color={show ? C.amber : C.text3} />
      </button>
    </div>
  );
}

function ProviderModelRow({ model, onChange }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', padding: '8px 0', borderBottom: `1px solid ${C.border}` }}>
      <div
        onClick={() => onChange({ ...model, enabled: !model.enabled })}
        title={model.enabled ? 'Disable' : 'Enable'}
        style={{
          width: 8, height: 8, borderRadius: '50%', flexShrink: 0, cursor: 'pointer',
          background: model.enabled ? C.emerald : C.surface4,
          border: `1.5px solid ${model.enabled ? C.emerald : C.border}`,
          boxShadow: model.enabled ? `0 0 6px ${C.emerald}60` : 'none',
          marginRight: 10, transition: 'all 0.15s',
        }}
      />
      <span style={{
        flex: 1, fontSize: 12, fontFamily: "'DM Mono', monospace",
        color: model.enabled ? C.text : C.text3,
        overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
        transition: 'color 0.15s',
      }}>{model.id}</span>
      <div style={{ display: 'flex', gap: 4, flexShrink: 0 }}>
        {model.text !== undefined && (
          <span onClick={() => model.enabled && onChange({ ...model, text: !model.text })} style={{
            fontSize: 10, fontWeight: 700, padding: '2px 7px', borderRadius: 4,
            cursor: model.enabled ? 'pointer' : 'default',
            background: model.enabled && model.text ? `${C.blue}25` : C.surface3,
            color: model.enabled && model.text ? C.blue : C.text3,
            border: `1px solid ${model.enabled && model.text ? `${C.blue}40` : 'transparent'}`,
            transition: 'all 0.12s', userSelect: 'none',
          }}>Text</span>
        )}
        {model.image !== undefined && (
          <span onClick={() => model.enabled && onChange({ ...model, image: !model.image })} style={{
            fontSize: 10, fontWeight: 700, padding: '2px 7px', borderRadius: 4,
            cursor: model.enabled ? 'pointer' : 'default',
            background: model.enabled && model.image ? `${C.violet}25` : C.surface3,
            color: model.enabled && model.image ? C.violet : C.text3,
            border: `1px solid ${model.enabled && model.image ? `${C.violet}40` : 'transparent'}`,
            transition: 'all 0.12s', userSelect: 'none',
          }}>Image</span>
        )}
      </div>
    </div>
  );
}

function ProviderBadge({ type, size = 28 }) {
  const palettes = {
    openai:      { bg: 'oklch(28% 0.08 160)', color: 'oklch(65% 0.13 160)', char: '⬡' },
    grok:        { bg: 'oklch(30% 0.10 68)',  color: 'oklch(72% 0.14 68)',  char: '⚡' },
    claude:      { bg: 'oklch(28% 0.08 15)',  color: 'oklch(65% 0.14 15)',  char: '✦' },
    huggingface: { bg: 'oklch(30% 0.10 50)',  color: 'oklch(75% 0.13 50)',  char: '🤗' },
    compatible:  { bg: 'oklch(28% 0.07 200)', color: 'oklch(65% 0.11 200)', char: '⬡' },
  };
  const p = palettes[type] || palettes.compatible;
  return (
    <div style={{
      width: size, height: size, borderRadius: size * 0.28, flexShrink: 0,
      background: p.bg, display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontSize: size * 0.42, color: p.color,
    }}>{p.char}</div>
  );
}

function ProviderDetailPanel({ provider, onUpdate, onDelete }) {
  const { confirmDelete, dialog: deleteDialog } = useConfirmDelete();
  const meta = AI_PROVIDERS_META[provider.type];
  const [testing, setTesting]     = useState(false);
  const [testOk, setTestOk]       = useState(null);
  const [refreshing, setRefreshing] = useState(false);

  const enabled    = provider.models.filter(m => m.enabled);
  const textCount  = enabled.filter(m => m.text).length;
  const imageCount = enabled.filter(m => m.image).length;

  function handleTest() {
    setTesting(true); setTestOk(null);
    setTimeout(() => { setTesting(false); setTestOk(true); setTimeout(() => setTestOk(null), 3000); }, 1400);
  }
  function handleRefresh() {
    setRefreshing(true);
    setTimeout(() => setRefreshing(false), 1200);
  }
  function updateModel(upd) {
    onUpdate({ ...provider, models: provider.models.map(m => m.id === upd.id ? upd : m) });
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div style={{ padding: '16px 20px', borderBottom: `1px solid ${C.border}`, display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', flexShrink: 0 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <ProviderBadge type={provider.type} size={40} />
          <div>
            <div style={{ fontSize: 17, fontWeight: 700, letterSpacing: '-0.01em' }}>{provider.name}</div>
            <div style={{ fontSize: 12, color: C.text3, marginTop: 2 }}>{textCount} text · {imageCount} image · {provider.models.length} total</div>
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ fontSize: 12, color: C.text3 }}>{provider.enabled ? 'Enabled' : 'Disabled'}</span>
          <div onClick={() => onUpdate({ ...provider, enabled: !provider.enabled })} style={{
            width: 34, height: 19, borderRadius: 10, cursor: 'pointer',
            background: provider.enabled ? C.amber : C.surface4,
            border: `1px solid ${provider.enabled ? C.amber : C.border}`,
            position: 'relative', transition: 'all 0.2s',
          }}>
            <div style={{
              position: 'absolute', top: 3, left: provider.enabled ? 17 : 3,
              width: 11, height: 11, borderRadius: '50%',
              background: provider.enabled ? '#0d0b09' : C.text3,
              transition: 'left 0.2s',
            }} />
          </div>
        </div>
      </div>

      <div style={{ padding: '10px 20px', borderBottom: `1px solid ${C.border}`, display: 'flex', gap: 6, flexShrink: 0, alignItems: 'center' }}>
        <Btn variant="primary" sz="sm"><Icon name="check" size={11} color="#0d0b09" />Save</Btn>
        <Btn variant="secondary" sz="sm" onClick={handleTest} disabled={testing}>
          {testing ? <><SpinnerInline />Testing…</> : <><Icon name="zap" size={11} color={C.text2} />Test connection</>}
        </Btn>
        <Btn variant="secondary" sz="sm" onClick={handleRefresh} disabled={refreshing}>
          {refreshing ? <><SpinnerInline />Refreshing…</> : <><Icon name="refresh" size={11} color={C.text2} />Refresh models</>}
        </Btn>
        {testOk && <span style={{ fontSize: 11, color: C.emerald, display: 'flex', alignItems: 'center', gap: 4 }}><Icon name="check" size={11} color={C.emerald} />Connected</span>}
        <div style={{ flex: 1 }} />
        <Btn variant="danger" sz="sm" onClick={() => confirmDelete({ title: `Delete "${meta?.name || provider.type}" provider?`, body: 'This provider and all its configured models will be removed.', onConfirm: onDelete })}><Icon name="trash" size={11} />Delete</Btn>
      </div>

      <div style={{ flex: 1, overflowY: 'auto' }}>
        <div style={{ padding: '14px 20px', borderBottom: `1px solid ${C.border}` }}>
          <div style={{ fontSize: 10, fontWeight: 700, letterSpacing: '0.09em', textTransform: 'uppercase', color: C.text3, marginBottom: 12 }}>Connection</div>
          <div style={{ marginBottom: 12 }}>
            <div style={{ fontSize: 11.5, fontWeight: 600, color: C.text2, marginBottom: 5 }}>Display Name</div>
            <FInput value={provider.name} onChange={v => onUpdate({ ...provider, name: v })} />
          </div>
          <div>
            <div style={{ fontSize: 11.5, fontWeight: 600, color: C.text2, marginBottom: 5, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              {meta.keyLabel}
              {meta.keyLink && (
                <a href={meta.keyLink} target="_blank" rel="noopener noreferrer"
                  style={{ fontSize: 11, color: C.amber, textDecoration: 'none' }}>
                  Get your key →
                </a>
              )}
            </div>
            <KeyInput value={provider.apiKey} onChange={v => onUpdate({ ...provider, apiKey: v })} />
            <div style={{ fontSize: 10.5, color: C.text3, marginTop: 5 }}>Stored locally, never sent to our servers.</div>
          </div>
          {provider.endpoint !== undefined && (
            <div style={{ marginTop: 12 }}>
              <div style={{ fontSize: 11.5, fontWeight: 600, color: C.text2, marginBottom: 5, display: 'flex', alignItems: 'center', gap: 6 }}>
                Endpoint URL
                <span style={{ fontSize: 10, fontWeight: 700, padding: '1px 5px', borderRadius: 4, background: C.surface3, color: C.text3 }}>OPTIONAL</span>
              </div>
              <FInput value={provider.endpoint || ''} onChange={v => onUpdate({ ...provider, endpoint: v })} placeholder="https://…" />
            </div>
          )}
        </div>

        <div style={{ padding: '14px 20px' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
            <div style={{ fontSize: 10, fontWeight: 700, letterSpacing: '0.09em', textTransform: 'uppercase', color: C.text3 }}>
              Models
              <span style={{ fontSize: 11, fontWeight: 400, textTransform: 'none', letterSpacing: 0, color: C.text3, marginLeft: 8 }}>click dot · toggle caps</span>
            </div>
            <div style={{ display: 'flex', gap: 6 }}>
              <Btn variant="ghost" sz="xs" style={{ color: C.text3 }} onClick={() => onUpdate({ ...provider, models: provider.models.map(m => ({ ...m, enabled: true })) })}>All on</Btn>
              <Btn variant="ghost" sz="xs" style={{ color: C.text3 }} onClick={() => onUpdate({ ...provider, models: provider.models.map(m => ({ ...m, enabled: false })) })}>All off</Btn>
            </div>
          </div>
          {provider.models.map(m => <ProviderModelRow key={m.id} model={m} onChange={updateModel} />)}
        </div>
      </div>
      {deleteDialog}
    </div>
  );
}

function SpinnerInline() {
  return <div style={{ width: 10, height: 10, borderRadius: '50%', border: `1.5px solid ${C.border}`, borderTopColor: C.text2, animation: 'provSpin 0.7s linear infinite', flexShrink: 0 }} />;
}

function OnboardPickStep({ onSelect, onCancel }) {
  const [hovered, setHovered] = useState(null);
  const providers = Object.values(AI_PROVIDERS_META);
  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div style={{ padding: '18px 20px 14px', borderBottom: `1px solid ${C.border}`, flexShrink: 0 }}>
        <div style={{ fontSize: 10, fontWeight: 700, letterSpacing: '0.09em', textTransform: 'uppercase', color: C.amber, marginBottom: 5 }}>New Provider</div>
        <div style={{ fontSize: 17, fontWeight: 700, letterSpacing: '-0.01em' }}>Connect a provider</div>
        <div style={{ fontSize: 12, color: C.text3, marginTop: 3 }}>Choose the platform you want to use for AI generation.</div>
      </div>
      <div style={{ flex: 1, overflowY: 'auto', padding: '14px 20px', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, alignContent: 'start' }}>
        {providers.map(p => {
          const isHov = hovered === p.id;
          return (
            <div key={p.id}
              onMouseEnter={() => setHovered(p.id)} onMouseLeave={() => setHovered(null)}
              onClick={() => onSelect(p.id)}
              style={{
                padding: '14px', borderRadius: 10, cursor: 'pointer',
                border: `1.5px solid ${isHov ? C.amber : C.border}`,
                background: isHov ? `${C.amber}0e` : C.surface2,
                transition: 'all 0.15s', display: 'flex', flexDirection: 'column', gap: 10,
              }}>
              <ProviderBadge type={p.id} size={32} />
              <div>
                <div style={{ fontSize: 13, fontWeight: 600 }}>{p.name}</div>
                <div style={{ fontSize: 11, color: C.text3, marginTop: 3, lineHeight: 1.45 }}>{p.desc}</div>
              </div>
            </div>
          );
        })}
      </div>
      <div style={{ padding: '12px 20px', borderTop: `1px solid ${C.border}`, flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <StepPips current={0} />
        <Btn variant="secondary" sz="sm" onClick={onCancel}>Cancel</Btn>
      </div>
    </div>
  );
}

function OnboardConfigStep({ providerType, onBack, onNext }) {
  const meta = AI_PROVIDERS_META[providerType];
  const [apiKey, setApiKey]     = useState('');
  const [endpoint, setEndpoint] = useState('');
  const [loading, setLoading]   = useState(false);

  const needsEndpoint = meta.needs.includes('endpoint');
  const canContinue   = (!meta.apiKeyRequired || apiKey.trim()) && (!meta.endpointRequired || endpoint.trim());

  function handleNext() {
    setLoading(true);
    setTimeout(() => {
      setLoading(false);
      onNext({ apiKey, endpoint, models: meta.sampleModels.map(m => ({ ...m, enabled: false })) });
    }, 1300);
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div style={{ padding: '18px 20px 14px', borderBottom: `1px solid ${C.border}`, flexShrink: 0 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
          <ProviderBadge type={providerType} size={28} />
          <div>
            <div style={{ fontSize: 10, fontWeight: 700, letterSpacing: '0.09em', textTransform: 'uppercase', color: C.amber }}>Configure</div>
            <div style={{ fontSize: 15, fontWeight: 700, letterSpacing: '-0.01em' }}>{meta.name}</div>
          </div>
        </div>
        <div style={{ fontSize: 12, color: C.text3 }}>Enter your credentials to connect this provider.</div>
      </div>
      <div style={{ flex: 1, overflowY: 'auto', padding: '16px 20px', display: 'flex', flexDirection: 'column', gap: 14 }}>
        {needsEndpoint && (
          <div>
            <div style={{ fontSize: 11.5, fontWeight: 600, color: C.text2, marginBottom: 5, display: 'flex', alignItems: 'center', gap: 6 }}>
              Endpoint URL
              {meta.endpointRequired
                ? <span style={{ fontSize: 10, fontWeight: 700, padding: '1px 5px', borderRadius: 4, background: `${C.amber}20`, color: C.amber }}>REQUIRED</span>
                : <span style={{ fontSize: 10, fontWeight: 700, padding: '1px 5px', borderRadius: 4, background: C.surface3, color: C.text3 }}>OPTIONAL</span>}
            </div>
            <FInput value={endpoint} onChange={setEndpoint} placeholder="https://your-endpoint.example.com/v1" />
            {providerType === 'compatible' && <div style={{ fontSize: 10.5, color: C.text3, marginTop: 5, lineHeight: 1.5 }}>Works with LM Studio, Ollama, or any OpenAI-compatible API. Point to the <code style={{ fontFamily: 'DM Mono', fontSize: 10, background: C.surface3, padding: '1px 4px', borderRadius: 3 }}>/v1</code> base URL.</div>}
            {providerType === 'huggingface' && <div style={{ fontSize: 10.5, color: C.text3, marginTop: 5 }}>Leave blank to use the default HF Inference API endpoint.</div>}
          </div>
        )}
        <div>
          <div style={{ fontSize: 11.5, fontWeight: 600, color: C.text2, marginBottom: 5, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              {meta.keyLabel}
              {meta.apiKeyRequired
                ? <span style={{ fontSize: 10, fontWeight: 700, padding: '1px 5px', borderRadius: 4, background: `${C.amber}20`, color: C.amber }}>REQUIRED</span>
                : <span style={{ fontSize: 10, fontWeight: 700, padding: '1px 5px', borderRadius: 4, background: C.surface3, color: C.text3 }}>OPTIONAL</span>}
            </span>
            {meta.keyLink && <a href={meta.keyLink} target="_blank" rel="noopener noreferrer" style={{ fontSize: 11, color: C.amber, textDecoration: 'none' }}>Get your key →</a>}
          </div>
          <KeyInput value={apiKey} onChange={setApiKey} placeholder="sk-…" />
          <div style={{ fontSize: 10.5, color: C.text3, marginTop: 5 }}>Stored locally only, never sent to our servers.</div>
        </div>
      </div>
      <div style={{ padding: '12px 20px', borderTop: `1px solid ${C.border}`, flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <StepPips current={1} />
        <div style={{ display: 'flex', gap: 8 }}>
          <Btn variant="secondary" sz="sm" onClick={onBack}>Back</Btn>
          <Btn variant="primary" sz="sm" disabled={!canContinue || loading} onClick={handleNext}>
            {loading ? <><SpinnerInline />Fetching models…</> : <>Fetch models <Icon name="chevron-right" size={12} color="#0d0b09" /></>}
          </Btn>
        </div>
      </div>
    </div>
  );
}

function OnboardModelsStep({ providerType, data, onBack, onFinish }) {
  const meta = AI_PROVIDERS_META[providerType];
  const [models, setModels] = useState(data.models);
  const noModels = models.length === 0;
  const enabledCount = models.filter(m => m.enabled).length;
  function updateModel(upd) { setModels(ms => ms.map(m => m.id === upd.id ? upd : m)); }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div style={{ padding: '18px 20px 14px', borderBottom: `1px solid ${C.border}`, flexShrink: 0 }}>
        <div style={{ fontSize: 17, fontWeight: 700, letterSpacing: '-0.01em', marginBottom: 3 }}>
          {noModels ? 'No models discovered' : 'Choose your models'}
        </div>
        <div style={{ fontSize: 12, color: C.text3 }}>
          {noModels ? 'You can configure models manually later.' : `${models.length} models found — enable the ones you want.`}
        </div>
        {!noModels && (
          <div style={{ marginTop: 10, padding: '7px 11px', borderRadius: 7, background: `${C.emerald}15`, border: `1px solid ${C.emerald}30`, display: 'flex', alignItems: 'center', gap: 7, fontSize: 11.5, color: C.emerald }}>
            <Icon name="check" size={11} color={C.emerald} />
            Connection successful · {meta.name}
          </div>
        )}
      </div>
      <div style={{ flex: 1, overflowY: 'auto', padding: '0 20px' }}>
        {noModels ? (
          <div style={{ padding: '32px 0', textAlign: 'center', color: C.text3 }}>
            <Icon name="layers" size={28} color={C.text3} />
            <div style={{ fontSize: 13, marginTop: 10, marginBottom: 6 }}>No models returned from this endpoint.</div>
            <div style={{ fontSize: 11 }}>You can add models manually after connecting.</div>
          </div>
        ) : (
          <>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '10px 0 6px', borderBottom: `1px solid ${C.border}`, marginBottom: 2 }}>
              <span style={{ fontSize: 11, color: C.text3 }}>{enabledCount} of {models.length} enabled</span>
              <div style={{ display: 'flex', gap: 8 }}>
                <Btn variant="ghost" sz="xs" style={{ color: C.text3 }} onClick={() => setModels(ms => ms.map(m => ({ ...m, enabled: true })))}>Enable all</Btn>
                <Btn variant="ghost" sz="xs" style={{ color: C.text3 }} onClick={() => setModels(ms => ms.map(m => ({ ...m, enabled: false })))}>Disable all</Btn>
              </div>
            </div>
            {models.map(m => <ProviderModelRow key={m.id} model={m} onChange={updateModel} />)}
          </>
        )}
      </div>
      <div style={{ padding: '12px 20px', borderTop: `1px solid ${C.border}`, flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <StepPips current={2} />
        <div style={{ display: 'flex', gap: 8 }}>
          <Btn variant="secondary" sz="sm" onClick={onBack}>Back</Btn>
          <Btn variant="primary" sz="sm" onClick={() => onFinish({ ...data, models })}>
            <Icon name="check" size={11} color="#0d0b09" />Add provider
          </Btn>
        </div>
      </div>
    </div>
  );
}

function StepPips({ current }) {
  return (
    <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
      {[0, 1, 2].map(i => (
        <div key={i} style={{
          height: 5, borderRadius: 3, transition: 'all 0.2s',
          width: i === current ? 18 : 5,
          background: i < current ? C.emerald : i === current ? C.amber : C.surface4,
        }} />
      ))}
    </div>
  );
}

function AIProvidersModal({ onClose }) {
  const [providers, setProviders] = useState(INITIAL_AI_PROVIDERS);
  const [selectedId, setSelectedId] = useState('ap1');
  const [onboarding, setOnboarding] = useState(null);

  const selected = providers.find(p => p.id === selectedId);

  function startAdd() { setSelectedId(null); setOnboarding({ step: 1, type: null, data: {} }); }

  function finishOnboarding({ type, data }) {
    const meta = AI_PROVIDERS_META[type];
    const newP = {
      id: 'ap' + Date.now(), type, name: meta.name, enabled: true,
      apiKey: data.apiKey,
      ...(meta.needs.includes('endpoint') ? { endpoint: data.endpoint || '' } : {}),
      models: data.models,
    };
    setProviders(ps => [...ps, newP]);
    setSelectedId(newP.id);
    setOnboarding(null);
  }

  function deleteProvider() {
    const remaining = providers.filter(p => p.id !== selectedId);
    setProviders(remaining);
    setSelectedId(remaining[0]?.id || null);
  }

  const counts = (p) => ({
    t: p.models.filter(m => m.enabled && m.text).length,
    i: p.models.filter(m => m.enabled && m.image).length,
  });

  return (
    <Modal onClose={onClose} maxW={900}>
      <ModalHeader title="AI Providers" onClose={onClose} />
      <style>{`@keyframes provSpin { to { transform: rotate(360deg); } }`}</style>
      <div style={{ display: 'flex', height: 560 }}>
        {/* Sidebar */}
        <div style={{ width: 230, borderRight: `1px solid ${C.border}`, display: 'flex', flexDirection: 'column', flexShrink: 0 }}>
          <div style={{ padding: '10px 12px 6px', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexShrink: 0 }}>
            <span style={{ fontSize: 10, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: C.text3 }}>Providers</span>
            <Btn variant="ghost" sz="icon" onClick={startAdd} title="Add provider" style={{ padding: 4 }}>
              <Icon name="plus" size={13} color={C.amber} />
            </Btn>
          </div>
          <div style={{ flex: 1, overflowY: 'auto' }}>
            {providers.map(p => {
              const isSel = p.id === selectedId && !onboarding;
              const { t, i } = counts(p);
              return (
                <div key={p.id}
                  onClick={() => { setSelectedId(p.id); setOnboarding(null); }}
                  style={{
                    display: 'flex', alignItems: 'center', gap: 9,
                    padding: '8px 10px', margin: '1px 6px', borderRadius: 8, cursor: 'pointer',
                    background: isSel ? `${C.amber}15` : 'transparent',
                    border: `1px solid ${isSel ? `${C.amber}35` : 'transparent'}`,
                    transition: 'all 0.12s',
                  }}>
                  <ProviderBadge type={p.type} size={28} />
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: 12.5, fontWeight: 500, color: isSel ? C.text : C.text2, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{p.name}</div>
                    <div style={{ fontSize: 10.5, color: C.text3, marginTop: 1 }}>{t}t · {i}i</div>
                  </div>
                  <span style={{ fontSize: 10, fontWeight: 700, padding: '2px 6px', borderRadius: 5, background: isSel ? `${C.amber}25` : C.surface3, color: isSel ? C.amber : C.text3 }}>{t + i}</span>
                </div>
              );
            })}
            {providers.length === 0 && (
              <div style={{ padding: '20px 14px', fontSize: 12, color: C.text3, textAlign: 'center', lineHeight: 1.6 }}>No providers yet.<br />Click + to add one.</div>
            )}
          </div>
          <div style={{ padding: '8px 10px', borderTop: `1px solid ${C.border}`, display: 'flex', gap: 6 }}>
            <Btn variant="outline" sz="xs" style={{ flex: 1, justifyContent: 'center' }}><Icon name="upload" size={11} color={C.text2} />Export</Btn>
            <Btn variant="outline" sz="xs" style={{ flex: 1, justifyContent: 'center' }}><Icon name="download" size={11} color={C.text2} />Import</Btn>
          </div>
        </div>

        {/* Main panel */}
        <div style={{ flex: 1, minWidth: 0 }}>
          {onboarding ? (
            onboarding.step === 1 ? (
              <OnboardPickStep
                onSelect={type => setOnboarding({ step: 2, type, data: {} })}
                onCancel={() => { setOnboarding(null); if (providers[0]) setSelectedId(providers[0].id); }}
              />
            ) : onboarding.step === 2 ? (
              <OnboardConfigStep
                providerType={onboarding.type}
                onBack={() => setOnboarding({ step: 1, type: null, data: {} })}
                onNext={data => setOnboarding({ step: 3, type: onboarding.type, data })}
              />
            ) : (
              <OnboardModelsStep
                providerType={onboarding.type}
                data={onboarding.data}
                onBack={() => setOnboarding({ step: 2, type: onboarding.type, data: onboarding.data })}
                onFinish={data => finishOnboarding({ type: onboarding.type, data })}
              />
            )
          ) : selected ? (
            <ProviderDetailPanel
              provider={selected}
              onUpdate={p => setProviders(ps => ps.map(x => x.id === p.id ? p : x))}
              onDelete={deleteProvider}
            />
          ) : (
            <div style={{ height: '100%', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 12 }}>
              <Icon name="zap" size={32} color={C.text3} />
              <div style={{ fontSize: 13, color: C.text2, fontWeight: 500 }}>No provider selected</div>
              <div style={{ fontSize: 12, color: C.text3 }}>Select a provider or add a new one.</div>
              <Btn variant="primary" sz="sm" onClick={startAdd} style={{ marginTop: 4 }}>
                <Icon name="plus" size={12} color="#0d0b09" />Add provider
              </Btn>
            </div>
          )}
        </div>
      </div>
    </Modal>
  );
}

// ─── PROMPT LIBRARY ───────────────────────────────────────────────────────────

const PROMPT_STEPS = [
  { id: 'appearance', label: 'Appearance', hasTurnShapes: false },
  { id: 'selection',  label: 'Selection',  hasTurnShapes: false },
  { id: 'planning',   label: 'Planning',   hasTurnShapes: true  },
  { id: 'prose',      label: 'Prose',      hasTurnShapes: true  },
];

const PL_PLACEHOLDERS = {
  appearance: [
    { token: '{content.explicitLabel}',  desc: 'Explicit content label.' },
    { token: '{content.violentLabel}',   desc: 'Violent content label.' },
    { token: '{appearance.characters}',  desc: 'Appearance-stage character list.' },
    { token: '{appearance.transcript}',  desc: 'Appearance-stage transcript.' },
  ],
  selection: [
    { token: '{content.explicitLabel}',  desc: 'Explicit content label.' },
    { token: '{content.violentLabel}',   desc: 'Violent content label.' },
    { token: '{context.characters}',     desc: 'Present characters list.' },
    { token: '{context.transcript}',     desc: 'Recent transcript excerpt.' },
  ],
  planning: [
    { token: '{context.snapshot}',                        desc: 'Latest snapshot summary.' },
    { token: '{context.transcript}',                      desc: 'Transcript since latest snapshot.' },
    { token: '{context.earlierPrivateIntentContinuity}',  desc: 'Older private intent continuity.' },
    { token: '{context.characterAppearances}',            desc: 'Appearance state for present characters.' },
    { token: '{actor.name}',                              desc: 'Current actor name.' },
    { token: '{guidance}',                                desc: 'Guidance text, when supplied.' },
    { token: '{guidanceSection}',                         desc: 'Guidance section with heading and spacing.' },
    { token: '{requestedTurnShape}',                      desc: 'Requested turn shape label.' },
    { token: '{requestedTurnShapeSection}',               desc: 'Turn-shape instructions, when supplied.' },
    { token: '{turnScopeRules}',                          desc: 'Default planning turn scope rules.' },
    { token: '{planning.turnShapeDefinitions}',           desc: 'Editable planning turn-shape definitions.' },
  ],
  prose: [
    { token: '{context.snapshot}',                        desc: 'Latest snapshot summary.' },
    { token: '{context.transcript}',                      desc: 'Transcript since latest snapshot.' },
    { token: '{context.earlierPrivateIntentContinuity}',  desc: 'Older private intent continuity.' },
    { token: '{context.characterAppearances}',            desc: 'Appearance state for present characters.' },
    { token: '{actor.name}',                              desc: 'Current actor name.' },
    { token: '{guidance}',                                desc: 'Guidance text, when supplied.' },
    { token: '{guidanceSection}',                         desc: 'Guidance section with heading and spacing.' },
    { token: '{requestedTurnShape}',                      desc: 'Requested turn shape label.' },
    { token: '{requestedTurnShapeSection}',               desc: 'Turn-shape instructions, when supplied.' },
    { token: '{planning.output}',                         desc: 'Planning stage output.' },
  ],
};

const PL_DEFAULTS = {
  appearance: {
    system: `You update character scene state.\n\nReturn structured output only.\n\nScene state is what is visibly true about each character right now: clothing, carried items, body position, location, posture, visible condition, and current physical contact with people or objects.\n\nUse the prior scene state as the starting point.\nUse the latest transcript to update it.`,
    user: `Content guidance:\n- Explicit content: {content.explicitLabel}\n- Violent content: {content.violentLabel}\n\nCharacters in the scene with initial appearance:\n{appearance.characters}\n\n**Transcript:**\n{appearance.transcript}`,
  },
  selection: {
    system: `You determine which character should respond next in a collaborative fiction scene. Consider dramatic momentum, who has been addressed, who has unspoken motivation, and whose silence would be most conspicuous. Output only the character name and a one-sentence reason.`,
    user: `Scene: {context.transcript}\n\nPresent: {context.characters}\n\nWho should respond next?`,
  },
  planning: {
    system: `You are a dramaturgical planner for a collaborative fiction session. Given the scene state, character profiles, and recent turns, produce a structured plan for the next character's response. Include: narrative beat, intent, immediate goal, why now, and what change this turn introduces. Do not write the prose itself.`,
    user: `{context.snapshot}\n\n{context.transcript}\n\n{context.characterAppearances}\n\nActor: {actor.name}\n\n{guidanceSection}\n\n{requestedTurnShapeSection}\n\n{turnScopeRules}`,
  },
  prose: {
    system: `You are a skilled prose writer for a collaborative fiction tool, writing in the style of contemporary literary fiction. Write in third-person limited from the perspective of the active character. Be economical. Use italics for action beats (*like this*). Stay tightly in character voice.`,
    user: `{context.snapshot}\n\n{context.transcript}\n\n{context.characterAppearances}\n\nActor: {actor.name}\n\n{guidanceSection}\n\n{requestedTurnShapeSection}\n\n{planning.output}`,
  },
};

const PL_TURN_SHAPES_DEFAULTS = {
  planning: [
    { id: 'compact',         label: 'compact',         value: 'one action beat, one or two phrases, optional short tag (always preferred)' },
    { id: 'brief',           label: 'brief',           value: 'one action beat, one to two short lines with a tag in between (rare)' },
    { id: 'monologue',       label: 'monologue',       value: 'short monologue allowed (for multi-step beats, rare)' },
    { id: 'extended',        label: 'extended',        value: 'elaborate the beat into three focused paragraphs with well choreography interactions (only when asked)' },
    { id: 'silent',          label: 'silent',          value: 'quick action/subtext only, no spoken lines (rare)' },
    { id: 'silent-extended', label: 'silent extended', value: 'extended action/subtext only, no spoken lines; detailed movement' },
  ],
  prose: [
    { id: 'compact',         label: 'compact',         value: 'one action beat, one or two phrases, optional short tag (always preferred)' },
    { id: 'brief',           label: 'brief',           value: 'one action beat, one to two short lines with a tag in between (rare)' },
    { id: 'monologue',       label: 'monologue',       value: 'short monologue allowed (for multi-step beats, rare)' },
    { id: 'extended',        label: 'extended',        value: 'elaborate the beat into three focused paragraphs with well choreography interactions (only when asked)' },
    { id: 'silent',          label: 'silent',          value: 'quick action/subtext only, no spoken lines (rare)' },
    { id: 'silent-extended', label: 'silent extended', value: 'extended action/subtext only, no spoken lines; detailed movement' },
  ],
};

function PLResetIcon({ size = 12, color = 'currentColor' }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none"
      stroke={color} strokeWidth={1.75} strokeLinecap="round" strokeLinejoin="round"
      style={{ flexShrink: 0 }}>
      <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" />
      <path d="M3 3v5h5" />
    </svg>
  );
}

function PLPromptSection({ label, value, onChange, onReset, isDirty }) {
  const [focused, setFocused] = useState(false);
  return (
    <div>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 6 }}>
        <span style={{ fontSize: 11.5, fontWeight: 600, color: C.text2, letterSpacing: '0.01em' }}>{label}</span>
        <button onClick={onReset} disabled={!isDirty} title="Reset to default"
          style={{
            display: 'inline-flex', alignItems: 'center', gap: 4,
            padding: '2px 7px', borderRadius: 5, border: 'none', cursor: isDirty ? 'pointer' : 'default',
            background: 'transparent', color: isDirty ? C.text3 : 'transparent',
            fontSize: 11, fontFamily: 'inherit', fontWeight: 500,
            transition: 'color 0.15s, background 0.15s',
          }}
          onMouseEnter={e => { if (isDirty) e.currentTarget.style.background = C.surface3; }}
          onMouseLeave={e => { e.currentTarget.style.background = 'transparent'; }}>
          <PLResetIcon size={11} color={isDirty ? C.text3 : 'transparent'} />
          Reset
        </button>
      </div>
      <textarea
        value={value}
        onChange={e => onChange(e.target.value)}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        rows={6}
        style={{
          width: '100%', background: C.surface2,
          border: `1px solid ${focused ? C.amberDim : C.border}`,
          borderRadius: 8, padding: '10px 12px',
          color: C.text, fontSize: 12, lineHeight: 1.7,
          fontFamily: "'DM Mono', monospace",
          outline: 'none', resize: 'vertical',
          transition: 'border-color 0.15s',
          whiteSpace: 'pre-wrap',
        }}
      />
    </div>
  );
}

function PLTurnShapeRow({ shape, isDirty, onChange, onReset }) {
  const [focused, setFocused] = useState(false);
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '116px 1fr', gap: 10, alignItems: 'start' }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', paddingTop: 8 }}>
        <span style={{ fontSize: 12, color: isDirty ? C.text : C.text2, fontWeight: isDirty ? 600 : 400, fontFamily: "'DM Mono', monospace" }}>
          {shape.label}
        </span>
        <button onClick={onReset} disabled={!isDirty} title="Reset"
          style={{
            display: 'inline-flex', alignItems: 'center', padding: '2px', border: 'none',
            background: 'transparent', cursor: isDirty ? 'pointer' : 'default',
            color: isDirty ? C.text3 : 'transparent', borderRadius: 4,
            transition: 'color 0.15s',
          }}>
          <PLResetIcon size={11} color={isDirty ? C.amberDim : 'transparent'} />
        </button>
      </div>
      <textarea
        value={shape.value}
        onChange={e => onChange(e.target.value)}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        rows={2}
        style={{
          width: '100%', background: C.surface2,
          border: `1px solid ${focused ? C.amberDim : isDirty ? 'color-mix(in oklch, oklch(55% 0.10 68) 50%, transparent)' : C.border}`,
          borderRadius: 7, padding: '7px 10px',
          color: C.text, fontSize: 12, lineHeight: 1.55,
          fontFamily: "'DM Mono', monospace",
          outline: 'none', resize: 'vertical',
          transition: 'border-color 0.15s',
        }}
      />
    </div>
  );
}

function PromptLibraryModal({ onClose }) {
  const [step, setStep]           = useState('appearance');
  const [prompts, setPrompts]     = useState(() => JSON.parse(JSON.stringify(PL_DEFAULTS)));
  const [turnShapes, setTurnShapes] = useState(() => JSON.parse(JSON.stringify(PL_TURN_SHAPES_DEFAULTS)));
  const [showPH, setShowPH]       = useState(false);
  const [copied, setCopied]       = useState(null);

  const stepDef    = PROMPT_STEPS.find(s => s.id === step);
  const placeholders = PL_PLACEHOLDERS[step] || [];

  function switchStep(id) { setStep(id); setShowPH(false); }

  function copyToken(token) {
    navigator.clipboard.writeText(token).catch(() => {});
    setCopied(token);
    setTimeout(() => setCopied(null), 1400);
  }

  function resetPrompt(field) {
    setPrompts(prev => ({ ...prev, [step]: { ...prev[step], [field]: PL_DEFAULTS[step][field] } }));
  }

  function resetTurnShape(stepId, shapeId) {
    const def = PL_TURN_SHAPES_DEFAULTS[stepId]?.find(s => s.id === shapeId);
    if (!def) return;
    setTurnShapes(prev => ({
      ...prev,
      [stepId]: prev[stepId].map(s => s.id === shapeId ? { ...s, value: def.value } : s),
    }));
  }

  const anyDirty = (
    prompts[step].system !== PL_DEFAULTS[step].system ||
    prompts[step].user   !== PL_DEFAULTS[step].user   ||
    (stepDef.hasTurnShapes && turnShapes[step]?.some((s, i) => s.value !== PL_TURN_SHAPES_DEFAULTS[step][i]?.value))
  );

  return (
    <Modal onClose={onClose} maxW={860}>
      <ModalHeader title="Prompt Library" onClose={onClose} />

      <div style={{ display: 'flex', flex: 1, minHeight: 0, height: 600 }}>

        {/* ── Left rail ── */}
        <div style={{
          width: 156, borderRight: `1px solid ${C.border}`,
          display: 'flex', flexDirection: 'column',
          padding: '14px 8px 14px', gap: 2, flexShrink: 0,
        }}>
          {PROMPT_STEPS.map(s => {
            const active = step === s.id;
            const isDirtyStep = (
              prompts[s.id].system !== PL_DEFAULTS[s.id].system ||
              prompts[s.id].user   !== PL_DEFAULTS[s.id].user   ||
              (s.hasTurnShapes && turnShapes[s.id]?.some((ts, i) => ts.value !== PL_TURN_SHAPES_DEFAULTS[s.id][i]?.value))
            );
            return (
              <div key={s.id} onClick={() => switchStep(s.id)}
                style={{
                  padding: '9px 12px', borderRadius: 8, cursor: 'pointer',
                  background: active ? 'color-mix(in oklch, oklch(72% 0.14 68) 10%, transparent)' : 'transparent',
                  border: `1px solid ${active ? 'color-mix(in oklch, oklch(55% 0.10 68) 50%, transparent)' : 'transparent'}`,
                  color: active ? C.text : C.text3,
                  fontSize: 13, fontWeight: active ? 600 : 400,
                  transition: 'all 0.12s',
                  display: 'flex', alignItems: 'center', gap: 6,
                }}>
                <div style={{
                  width: 3, height: 13, borderRadius: 2, flexShrink: 0,
                  background: active ? C.amber : 'transparent',
                  transition: 'background 0.12s',
                }} />
                <span style={{ flex: 1 }}>{s.label}</span>
                {isDirtyStep && (
                  <div style={{ width: 6, height: 6, borderRadius: '50%', background: C.amberDim, flexShrink: 0 }} />
                )}
              </div>
            );
          })}

          <div style={{ flex: 1 }} />

          <div style={{ padding: '6px 12px', display: 'flex', flexDirection: 'column', gap: 4 }}>
            <div style={{ height: 1, background: C.border, marginBottom: 4 }} />
            <div style={{ fontSize: 10.5, color: C.text3, lineHeight: 1.5 }}>
              Appearance · Selection shape prompts per step.
            </div>
            <div style={{ fontSize: 10.5, color: C.text3, lineHeight: 1.5 }}>
              Planning · Prose also include turn shape definitions.
            </div>
          </div>
        </div>

        {/* ── Main ── */}
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>

          {/* Step bar + placeholders toggle */}
          <div style={{
            padding: '10px 18px', borderBottom: `1px solid ${C.border}`,
            display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexShrink: 0,
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ fontSize: 13, fontWeight: 600, color: C.text }}>{stepDef.label}</span>
              {stepDef.hasTurnShapes && (
                <span style={{
                  fontSize: 10, fontFamily: "'DM Mono', monospace",
                  color: C.amberDim, background: 'color-mix(in oklch, oklch(72% 0.14 68) 10%, transparent)',
                  border: '1px solid color-mix(in oklch, oklch(55% 0.10 68) 40%, transparent)',
                  padding: '1px 6px', borderRadius: 4,
                }}>Turn Shapes</span>
              )}
            </div>
            <button
              onClick={() => setShowPH(p => !p)}
              style={{
                display: 'inline-flex', alignItems: 'center', gap: 5,
                padding: '4px 10px', borderRadius: 6, border: `1px solid ${showPH ? 'color-mix(in oklch, oklch(55% 0.10 68) 70%, transparent)' : C.border}`,
                background: showPH ? 'color-mix(in oklch, oklch(72% 0.14 68) 12%, transparent)' : C.surface3,
                color: showPH ? C.amberDim : C.text3,
                fontSize: 11.5, fontFamily: "'DM Mono', monospace", fontWeight: 500,
                cursor: 'pointer', transition: 'all 0.15s',
              }}>
              <span>{'{…}'}</span>
              <span style={{ fontFamily: "'DM Sans', sans-serif" }}>Placeholders</span>
              <svg width={10} height={10} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
                <path d={showPH ? 'M18 15l-6-6-6 6' : 'M6 9l6 6 6-6'} />
              </svg>
            </button>
          </div>

          {/* Placeholders panel */}
          {showPH && (
            <div style={{
              padding: '10px 18px 12px', borderBottom: `1px solid ${C.border}`,
              background: C.surface2, flexShrink: 0,
            }}>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 5 }}>
                {placeholders.map(p => {
                  const isCopied = copied === p.token;
                  return (
                    <button key={p.token} onClick={() => copyToken(p.token)} title={p.desc}
                      style={{
                        display: 'inline-flex', alignItems: 'center', gap: 5,
                        padding: '3px 9px', borderRadius: 6, border: 'none', cursor: 'pointer',
                        background: isCopied ? `${C.emerald}20` : C.surface3,
                        border: `1px solid ${isCopied ? `${C.emerald}60` : C.border}`,
                        transition: 'all 0.12s',
                      }}>
                      <span style={{
                        fontSize: 11, fontFamily: "'DM Mono', monospace",
                        color: isCopied ? C.emerald : C.amberDim,
                      }}>{p.token}</span>
                      {isCopied
                        ? <Icon name="check" size={10} color={C.emerald} />
                        : <Icon name="copy" size={10} color={C.text3} />}
                    </button>
                  );
                })}
              </div>
              <div style={{ fontSize: 10.5, color: C.text3, marginTop: 7 }}>
                Click any placeholder to copy it to your clipboard.
              </div>
            </div>
          )}

          {/* Scrollable editor */}
          <div style={{ flex: 1, overflowY: 'auto', padding: '18px 18px 24px', display: 'flex', flexDirection: 'column', gap: 22 }}>

            <PLPromptSection
              label="System Prompt"
              value={prompts[step].system}
              onChange={v => setPrompts(prev => ({ ...prev, [step]: { ...prev[step], system: v } }))}
              onReset={() => resetPrompt('system')}
              isDirty={prompts[step].system !== PL_DEFAULTS[step].system}
            />

            <PLPromptSection
              label="User Prompt Template"
              value={prompts[step].user}
              onChange={v => setPrompts(prev => ({ ...prev, [step]: { ...prev[step], user: v } }))}
              onReset={() => resetPrompt('user')}
              isDirty={prompts[step].user !== PL_DEFAULTS[step].user}
            />

            {stepDef.hasTurnShapes && (
              <div>
                <div style={{
                  fontSize: 11, fontWeight: 700, letterSpacing: '0.08em',
                  textTransform: 'uppercase', color: C.text3,
                  marginBottom: 12, display: 'flex', alignItems: 'center', gap: 8,
                }}>
                  Turn Shapes
                  <div style={{ flex: 1, height: 1, background: C.border }} />
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  {(turnShapes[step] || []).map(shape => {
                    const def = PL_TURN_SHAPES_DEFAULTS[step]?.find(s => s.id === shape.id);
                    return (
                      <PLTurnShapeRow
                        key={shape.id}
                        shape={shape}
                        isDirty={shape.value !== def?.value}
                        onChange={v => setTurnShapes(prev => ({
                          ...prev,
                          [step]: prev[step].map(s => s.id === shape.id ? { ...s, value: v } : s),
                        }))}
                        onReset={() => resetTurnShape(step, shape.id)}
                      />
                    );
                  })}
                </div>
              </div>
            )}
          </div>

          {/* Footer */}
          <div style={{
            padding: '11px 18px', borderTop: `1px solid ${C.border}`,
            display: 'flex', alignItems: 'center', justifyContent: 'flex-end',
            gap: 8, flexShrink: 0,
          }}>
            <Btn variant="secondary" sz="sm" onClick={onClose}>Cancel</Btn>
            <Btn variant="primary" sz="sm">
              <Icon name="check" size={12} color="#0d0b09" />Save
            </Btn>
          </div>
        </div>
      </div>
    </Modal>
  );
}

// ─── MODEL TUNING ─────────────────────────────────────────────────────────────

const MT_STEPS = [
  { id: 'appearance', label: 'Appearance' },
  { id: 'selection',  label: 'Selection'  },
  { id: 'planning',   label: 'Planning'   },
  { id: 'prose',      label: 'Prose'      },
];

const MT_DEFAULTS = {
  appearance: { temperature: 0.4, topP: '', maxTokens: '', seed: '', frequencyPenalty: '', presencePenalty: '', stopSequences: '' },
  selection:  { temperature: 0.2, topP: '', maxTokens: '', seed: '', frequencyPenalty: '', presencePenalty: '', stopSequences: '' },
  planning:   { temperature: 0.4, topP: '', maxTokens: '', seed: '', frequencyPenalty: '', presencePenalty: '', stopSequences: '' },
  prose:      { temperature: 0.7, topP: '', maxTokens: '', seed: '', frequencyPenalty: '', presencePenalty: '', stopSequences: '' },
};

function MTNumberField({ label, value, onChange, placeholder = 'model default' }) {
  const [focused, setFocused] = useState(false);
  return (
    <div>
      <div style={{ fontSize: 11.5, fontWeight: 600, color: C.text2, marginBottom: 5 }}>{label}</div>
      <input
        type="number" value={value} onChange={e => onChange(e.target.value)}
        placeholder={placeholder}
        onFocus={() => setFocused(true)} onBlur={() => setFocused(false)}
        style={{
          width: '100%', background: C.surface2,
          border: `1px solid ${focused ? C.amberDim : C.border}`,
          borderRadius: 7, padding: '7px 10px', color: C.text,
          fontSize: 13, outline: 'none', transition: 'border-color 0.15s',
          fontFamily: "'DM Mono', monospace",
        }}
      />
    </div>
  );
}

function MTSlider({ value, onChange }) {
  return (
    <div>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
        <span style={{ fontSize: 11.5, fontWeight: 600, color: C.text2 }}>Temperature</span>
        <span style={{ fontSize: 12, fontFamily: "'DM Mono', monospace", color: C.amber, fontWeight: 600 }}>{value.toFixed(2)}</span>
      </div>
      <div style={{ position: 'relative', height: 20, display: 'flex', alignItems: 'center' }}>
        {/* Track */}
        <div style={{ position: 'absolute', left: 0, right: 0, height: 4, borderRadius: 2, background: C.surface4 }} />
        {/* Fill */}
        <div style={{
          position: 'absolute', left: 0, height: 4, borderRadius: 2,
          width: `${(value / 2) * 100}%`,
          background: `linear-gradient(90deg, ${C.amberDim}, ${C.amber})`,
        }} />
        <input
          type="range" min={0} max={2} step={0.01} value={value}
          onChange={e => onChange(parseFloat(e.target.value))}
          style={{
            position: 'absolute', left: 0, right: 0, width: '100%',
            opacity: 0, height: 20, cursor: 'pointer', margin: 0,
          }}
        />
        {/* Thumb */}
        <div style={{
          position: 'absolute', left: `${(value / 2) * 100}%`,
          transform: 'translateX(-50%)',
          width: 16, height: 16, borderRadius: '50%',
          background: C.amber, border: `2px solid ${C.surface}`,
          boxShadow: '0 1px 4px rgba(0,0,0,0.3)',
          pointerEvents: 'none',
        }} />
      </div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 4 }}>
        <span style={{ fontSize: 10, color: C.text3, fontFamily: "'DM Mono', monospace" }}>0 · steadier</span>
        <span style={{ fontSize: 10, color: C.text3, fontFamily: "'DM Mono', monospace" }}>2 · wilder</span>
      </div>
    </div>
  );
}

function CopyToPopover({ currentStep, onCopy, onClose }) {
  const others = MT_STEPS.filter(s => s.id !== currentStep);
  const ref = useRef(null);

  useEffect(() => {
    function handler(e) {
      if (ref.current && !ref.current.contains(e.target)) onClose();
    }
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [onClose]);

  return (
    <div ref={ref} style={{
      position: 'absolute', top: '100%', right: 0, marginTop: 4, zIndex: 100,
      background: C.surface, border: `1px solid ${C.borderMid}`,
      borderRadius: 9, boxShadow: '0 8px 32px rgba(0,0,0,0.4)',
      padding: 6, minWidth: 150,
    }}>
      <div style={{ fontSize: 10.5, color: C.text3, padding: '4px 8px 6px', letterSpacing: '0.06em', textTransform: 'uppercase', fontWeight: 700 }}>Copy settings to</div>
      {others.map(s => (
        <div key={s.id} onClick={() => { onCopy(s.id); onClose(); }}
          style={{
            padding: '7px 10px', borderRadius: 6, cursor: 'pointer',
            fontSize: 13, color: C.text2, fontWeight: 500,
            transition: 'background 0.1s',
          }}
          onMouseEnter={e => e.currentTarget.style.background = C.surface3}
          onMouseLeave={e => e.currentTarget.style.background = 'transparent'}>
          {s.label}
        </div>
      ))}
      <div style={{ height: 1, background: C.border, margin: '4px 0' }} />
      <div onClick={() => { MT_STEPS.filter(s => s.id !== currentStep).forEach(s => onCopy(s.id)); onClose(); }}
        style={{
          padding: '7px 10px', borderRadius: 6, cursor: 'pointer',
          fontSize: 13, color: C.amber, fontWeight: 600,
          transition: 'background 0.1s',
        }}
        onMouseEnter={e => e.currentTarget.style.background = C.surface3}
        onMouseLeave={e => e.currentTarget.style.background = 'transparent'}>
        All phases
      </div>
    </div>
  );
}

function ModelTuningModal({ onClose }) {
  const [step, setStep]     = useState('appearance');
  const [settings, setSettings] = useState(() => JSON.parse(JSON.stringify(MT_DEFAULTS)));
  const [showCopyTo, setShowCopyTo] = useState(false);
  const [copiedToast, setCopiedToast] = useState(null);

  const cur = settings[step];
  const def = MT_DEFAULTS[step];

  function upd(key, val) {
    setSettings(prev => ({ ...prev, [step]: { ...prev[step], [key]: val } }));
  }

  function isDirty() {
    return Object.keys(def).some(k => String(cur[k]) !== String(def[k]));
  }

  function isDirtyStep(id) {
    const c = settings[id], d = MT_DEFAULTS[id];
    return Object.keys(d).some(k => String(c[k]) !== String(d[k]));
  }

  function resetCurrent() {
    setSettings(prev => ({ ...prev, [step]: { ...MT_DEFAULTS[step] } }));
  }

  function copyTo(targetId) {
    setSettings(prev => ({ ...prev, [targetId]: { ...prev[step] } }));
    const label = MT_STEPS.find(s => s.id === targetId)?.label;
    setCopiedToast(`Copied to ${label}`);
    setTimeout(() => setCopiedToast(null), 2000);
  }

  function handleExport() {
    const blob = new Blob([JSON.stringify(cur, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = `model-tuning-${step}.json`;
    a.click(); URL.revokeObjectURL(url);
  }

  function handleImport() {
    const input = document.createElement('input');
    input.type = 'file'; input.accept = '.json';
    input.onchange = e => {
      const file = e.target.files[0];
      if (!file) return;
      const reader = new FileReader();
      reader.onload = ev => {
        try {
          const data = JSON.parse(ev.target.result);
          setSettings(prev => ({ ...prev, [step]: { ...MT_DEFAULTS[step], ...data } }));
        } catch {}
      };
      reader.readAsText(file);
    };
    input.click();
  }

  return (
    <Modal onClose={onClose} maxW={780}>
      <ModalHeader title="Model Tuning" onClose={onClose} />

      <div style={{ display: 'flex', flex: 1, minHeight: 0, height: 540 }}>

        {/* ── Left rail ── */}
        <div style={{
          width: 156, borderRight: `1px solid ${C.border}`,
          display: 'flex', flexDirection: 'column',
          padding: '14px 8px', gap: 2, flexShrink: 0,
        }}>
          {MT_STEPS.map(s => {
            const active = step === s.id;
            const dirty  = isDirtyStep(s.id);
            return (
              <div key={s.id} onClick={() => setStep(s.id)}
                style={{
                  padding: '9px 12px', borderRadius: 8, cursor: 'pointer',
                  background: active ? 'color-mix(in oklch, oklch(72% 0.14 68) 10%, transparent)' : 'transparent',
                  border: `1px solid ${active ? 'color-mix(in oklch, oklch(55% 0.10 68) 50%, transparent)' : 'transparent'}`,
                  color: active ? C.text : C.text3,
                  fontSize: 13, fontWeight: active ? 600 : 400,
                  transition: 'all 0.12s',
                  display: 'flex', alignItems: 'center', gap: 6,
                }}>
                <div style={{
                  width: 3, height: 13, borderRadius: 2, flexShrink: 0,
                  background: active ? C.amber : 'transparent', transition: 'background 0.12s',
                }} />
                <span style={{ flex: 1 }}>{s.label}</span>
                {dirty && <div style={{ width: 6, height: 6, borderRadius: '50%', background: C.amberDim, flexShrink: 0 }} />}
              </div>
            );
          })}

          <div style={{ flex: 1 }} />
          <div style={{ padding: '6px 12px' }}>
            <div style={{ height: 1, background: C.border, marginBottom: 8 }} />
            <div style={{ fontSize: 10.5, color: C.text3, lineHeight: 1.55 }}>
              Lower temperature is steadier. Leave fields blank to use model defaults.
            </div>
          </div>
        </div>

        {/* ── Main ── */}
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>

          {/* Toolbar */}
          <div style={{
            padding: '10px 16px', borderBottom: `1px solid ${C.border}`,
            display: 'flex', alignItems: 'center', gap: 6, flexShrink: 0,
          }}>
            <span style={{ fontSize: 13, fontWeight: 600, color: C.text, marginRight: 6 }}>
              {MT_STEPS.find(s => s.id === step)?.label}
            </span>
            <div style={{ flex: 1 }} />

            {/* Import */}
            <button onClick={handleImport} style={{
              display: 'inline-flex', alignItems: 'center', gap: 5,
              padding: '4px 10px', borderRadius: 6, cursor: 'pointer',
              background: C.surface3, border: `1px solid ${C.border}`,
              color: C.text2, fontSize: 11.5, fontFamily: 'inherit', fontWeight: 500,
              transition: 'background 0.12s',
            }}
            onMouseEnter={e => e.currentTarget.style.background = C.surface4}
            onMouseLeave={e => e.currentTarget.style.background = C.surface3}>
              <Icon name="download" size={12} color={C.text3} />Import
            </button>

            {/* Export */}
            <button onClick={handleExport} style={{
              display: 'inline-flex', alignItems: 'center', gap: 5,
              padding: '4px 10px', borderRadius: 6, cursor: 'pointer',
              background: C.surface3, border: `1px solid ${C.border}`,
              color: C.text2, fontSize: 11.5, fontFamily: 'inherit', fontWeight: 500,
              transition: 'background 0.12s',
            }}
            onMouseEnter={e => e.currentTarget.style.background = C.surface4}
            onMouseLeave={e => e.currentTarget.style.background = C.surface3}>
              <Icon name="upload" size={12} color={C.text3} />Export
            </button>

            {/* Copy To */}
            <div style={{ position: 'relative' }}>
              <button onClick={() => setShowCopyTo(p => !p)} style={{
                display: 'inline-flex', alignItems: 'center', gap: 5,
                padding: '4px 10px', borderRadius: 6, cursor: 'pointer',
                background: showCopyTo ? 'color-mix(in oklch, oklch(72% 0.14 68) 12%, transparent)' : C.surface3,
                border: `1px solid ${showCopyTo ? 'color-mix(in oklch, oklch(55% 0.10 68) 50%, transparent)' : C.border}`,
                color: showCopyTo ? C.amber : C.text2,
                fontSize: 11.5, fontFamily: 'inherit', fontWeight: 500,
                transition: 'all 0.12s',
              }}>
                <Icon name="copy" size={12} color={showCopyTo ? C.amber : C.text3} />
                Copy To
                <svg width={9} height={9} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round">
                  <path d={showCopyTo ? 'M18 15l-6-6-6 6' : 'M6 9l6 6 6-6'} />
                </svg>
              </button>
              {showCopyTo && (
                <CopyToPopover currentStep={step} onCopy={copyTo} onClose={() => setShowCopyTo(false)} />
              )}
            </div>

            {/* Reset */}
            {isDirty() && (
              <button onClick={resetCurrent} style={{
                display: 'inline-flex', alignItems: 'center', gap: 4,
                padding: '4px 8px', borderRadius: 6, cursor: 'pointer',
                background: 'transparent', border: 'none',
                color: C.text3, fontSize: 11.5, fontFamily: 'inherit',
                transition: 'color 0.12s',
              }}
              onMouseEnter={e => e.currentTarget.style.color = C.text}
              onMouseLeave={e => e.currentTarget.style.color = C.text3}>
                <PLResetIcon size={11} color={C.text3} />Reset
              </button>
            )}
          </div>

          {/* Toast */}
          {copiedToast && (
            <div style={{
              position: 'absolute', bottom: 70, right: 20, zIndex: 200,
              background: C.surface3, border: `1px solid ${C.border}`,
              borderRadius: 8, padding: '7px 14px',
              fontSize: 12, color: C.text2,
              boxShadow: '0 4px 16px rgba(0,0,0,0.3)',
              display: 'flex', alignItems: 'center', gap: 6,
              animation: 'fadeIn 0.15s ease',
            }}>
              <Icon name="check" size={12} color={C.emerald} />{copiedToast}
            </div>
          )}

          {/* Fields */}
          <div style={{ flex: 1, overflowY: 'auto', padding: '20px 20px 24px', display: 'flex', flexDirection: 'column', gap: 22 }}>

            <MTSlider value={cur.temperature} onChange={v => upd('temperature', v)} />

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
              <MTNumberField label="Top P" value={cur.topP} onChange={v => upd('topP', v)} />
              <MTNumberField label="Max Output Tokens" value={cur.maxTokens} onChange={v => upd('maxTokens', v)} />
              <MTNumberField label="Seed" value={cur.seed} onChange={v => upd('seed', v)} />
              <MTNumberField label="Frequency Penalty" value={cur.frequencyPenalty} onChange={v => upd('frequencyPenalty', v)} />
              <MTNumberField label="Presence Penalty" value={cur.presencePenalty} onChange={v => upd('presencePenalty', v)} />
            </div>

            <div>
              <div style={{ fontSize: 11.5, fontWeight: 600, color: C.text2, marginBottom: 5 }}>Stop Sequences</div>
              <textarea
                value={cur.stopSequences}
                onChange={e => upd('stopSequences', e.target.value)}
                placeholder="One stop sequence per line…"
                rows={3}
                style={{
                  width: '100%', background: C.surface2,
                  border: `1px solid ${C.border}`,
                  borderRadius: 7, padding: '8px 10px', color: C.text,
                  fontSize: 12, lineHeight: 1.6, outline: 'none', resize: 'vertical',
                  fontFamily: "'DM Mono', monospace",
                  transition: 'border-color 0.15s',
                }}
                onFocus={e => e.target.style.borderColor = C.amberDim}
                onBlur={e => e.target.style.borderColor = C.border}
              />
              <div style={{ fontSize: 10.5, color: C.text3, marginTop: 4 }}>Enter one stop sequence per line.</div>
            </div>
          </div>

          {/* Footer */}
          <div style={{
            padding: '11px 18px', borderTop: `1px solid ${C.border}`,
            display: 'flex', alignItems: 'center', justifyContent: 'flex-end',
            gap: 8, flexShrink: 0,
          }}>
            <Btn variant="secondary" sz="sm" onClick={onClose}>Cancel</Btn>
            <Btn variant="primary" sz="sm">
              <Icon name="check" size={12} color="#0d0b09" />Save
            </Btn>
          </div>
        </div>
      </div>
    </Modal>
  );
}

Object.assign(window, { ImageGalleryModal, GenerateImageModal, EntityManagerModal, ExportModal, AIProvidersModal, PromptLibraryModal, ModelTuningModal });

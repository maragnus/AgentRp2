// character_v2.jsx — V2 character wizard, editor, and relationship components

const {
  SCENE_ROLES, TRAIT_CATEGORIES,
  CORE_DRIVES, CORE_FEARS, SURFACE_MASKS, HIDDEN_TRUTHS,
  SENTENCE_STYLES, HONESTY_STYLES, EMOTIONAL_LEAKAGES, ACTION_FINGERPRINTS, STRESS_PATTERNS,
  SOFT_SPOTS, AVOID_PATTERNS,
  BOND_TYPES, DYNAMICS,
  V2_STEPS, emptyV2Char,
} = window.V2_TAXONOMY;

// ── Section header — one design used everywhere ────────────────────────────────
// color bar + TITLE + description inline + count/max only for multiselects
function SectionHeader({ title, hint, color = C.amber, count, max }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 9 }}>
      <div style={{ width: 3, height: 13, borderRadius: 2, background: color, flexShrink: 0 }} />
      <span style={{ fontSize: 10.5, fontWeight: 700, letterSpacing: '0.09em', textTransform: 'uppercase', color: C.text3 }}>{title}</span>
      {hint && <span style={{ fontSize: 11.5, color: C.text3 }}>{hint}</span>}
      {max != null && (
        <span style={{ marginLeft: 'auto', fontSize: 11, fontFamily: "'DM Mono', monospace", color: count > 0 ? color : C.text3 }}>{count}/{max}</span>
      )}
    </div>
  );
}

// ── Item card — one card style used everywhere ─────────────────────────────────
// Leading circle indicator + label + description. Works for single and multi-select.
function ItemCard({ item, isSelected, onToggle, color = C.amber, disabled = false }) {
  return (
    <div onClick={() => !disabled && onToggle()}
      style={{
        display: 'flex', alignItems: 'center', gap: 9, padding: '9px 11px',
        borderRadius: 8, cursor: disabled ? 'not-allowed' : 'pointer',
        border: `1.5px solid ${isSelected ? color : C.border}`,
        background: isSelected ? `${color}12` : C.surface2,
        opacity: disabled ? 0.35 : 1, transition: 'all 0.12s',
      }}>
      <div style={{
        width: 16, height: 16, borderRadius: '50%', flexShrink: 0,
        border: `1.5px solid ${isSelected ? color : C.borderMid}`,
        background: isSelected ? color : 'transparent',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        transition: 'all 0.12s',
      }}>
        {isSelected && <Icon name="check" size={9} color="#0d0b09" />}
      </div>
      <div style={{ minWidth: 0 }}>
        <div style={{ fontSize: 12.5, fontWeight: isSelected ? 600 : 500, color: isSelected ? C.text : C.text2, lineHeight: 1.2 }}>{item.label}</div>
        {item.hover && <div style={{ fontSize: 11, color: C.text3, marginTop: 2, lineHeight: 1.4 }}>{item.hover}</div>}
      </div>
    </div>
  );
}

// ── Trait chip — chip style for Personality and Scene Roles ───────────────────
function TraitChip({ item, selected, onToggle, disabled }) {
  const [hov, setHov] = useState(false);
  const on = selected;
  return (
    <button title={item.hover} onClick={() => !disabled && onToggle(item.id)}
      onMouseEnter={() => setHov(true)} onMouseLeave={() => setHov(false)}
      style={{
        display: 'inline-flex', alignItems: 'center',
        padding: '4px 11px', borderRadius: 20, fontFamily: 'inherit',
        border: `1px solid ${on ? C.amber : hov ? C.borderMid : C.border}`,
        background: on ? `${C.amber}22` : hov ? C.surface3 : 'transparent',
        color: on ? C.amber : hov ? C.text2 : C.text3,
        fontSize: 12, fontWeight: on ? 600 : 400,
        cursor: disabled && !on ? 'not-allowed' : 'pointer',
        opacity: disabled && !on ? 0.35 : 1,
        transition: 'all 0.12s', whiteSpace: 'nowrap',
      }}>
      {item.label}
    </button>
  );
}

// ═══════════════════════════════════════════════════════════════════════════════
// WIZARD STEPS
// ═══════════════════════════════════════════════════════════════════════════════

function ConceptStep({ data, onChange }) {
  const selRoles = data.sceneRoles || [];
  const MAX = 2;
  function toggleRole(id) {
    if (selRoles.includes(id)) onChange({ sceneRoles: selRoles.filter(r => r !== id) });
    else if (selRoles.length < MAX) onChange({ sceneRoles: [...selRoles, id] });
  }
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 22 }}>
      <div>
        <SectionHeader title="Name" color={C.amber} />
        <input value={data.name} onChange={e => onChange({ name: e.target.value })}
          placeholder="Character name…" autoFocus
          style={{
            width: '100%', background: C.surface3, border: `1px solid ${C.borderMid}`,
            borderRadius: 8, padding: '10px 14px', color: C.text, fontSize: 16, fontWeight: 600,
            fontFamily: "'Playfair Display', serif", outline: 'none',
          }} />
      </div>
      <div>
        <SectionHeader title="Summary" hint="— one sentence, optional" color={C.blue} />
        <textarea value={data.summary} onChange={e => onChange({ summary: e.target.value })}
          placeholder="Who is this character in a sentence?" rows={2}
          style={{
            width: '100%', background: C.surface3, border: `1px solid ${C.border}`,
            borderRadius: 8, padding: '9px 12px', color: C.text, fontSize: 13,
            lineHeight: 1.6, fontFamily: 'inherit', outline: 'none', resize: 'vertical',
          }} />
      </div>
      <div>
        <SectionHeader title="Scene Roles" hint="— what function they serve" color={C.violet} count={selRoles.length} max={MAX} />
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
          {SCENE_ROLES.map(r => (
            <TraitChip key={r.id} item={r} selected={selRoles.includes(r.id)} onToggle={id => {
              if (selRoles.includes(id)) onChange({ sceneRoles: selRoles.filter(r => r !== id) });
              else if (selRoles.length < MAX) onChange({ sceneRoles: [...selRoles, id] });
            }} color={C.violet}
              disabled={selRoles.length >= MAX && !selRoles.includes(r.id)} />
          ))}
        </div>
      </div>
    </div>
  );
}

function PersonalityStep({ data, onChange }) {
  const traits = data.traits || {};
  const totalSelected = Object.values(traits).flat().length;
  const MAX = 6;

  function toggleTrait(catKey, traitId) {
    const cat = traits[catKey] || [];
    let newCat;
    if (cat.includes(traitId)) newCat = cat.filter(t => t !== traitId);
    else if (totalSelected < MAX) newCat = [...cat, traitId];
    else return;
    onChange({ traits: { ...traits, [catKey]: newCat } });
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 0 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
        <p style={{ fontSize: 12.5, color: C.text3, lineHeight: 1.6 }}>Pick 3 to 6 total. Hover any trait for a description.</p>
        <div style={{
          padding: '3px 10px', borderRadius: 20,
          background: totalSelected >= 3 ? `${C.amber}20` : C.surface3,
          border: `1px solid ${totalSelected >= 3 ? C.amberDim : C.border}`,
        }}>
          <span style={{ fontSize: 12, fontFamily: "'DM Mono', monospace", color: totalSelected >= 3 ? C.amber : C.text3 }}>
            {totalSelected} / {MAX}
          </span>
        </div>
      </div>

      {Object.entries(TRAIT_CATEGORIES).map(([catKey, catTraits], catIdx) => {
        const catSelected = traits[catKey] || [];
        const CAT_COLORS = [C.amber, C.rose, C.violet, C.blue, C.emerald];
        const catColor = CAT_COLORS[catIdx % CAT_COLORS.length];
        return (
          <div key={catKey} style={{ marginBottom: 18 }}>
            <SectionHeader title={catKey} color={catColor} count={catSelected.length} max={catSelected.length > 0 ? undefined : undefined} />
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 5 }}>
              {catTraits.map(trait => (
                <TraitChip key={trait.id} item={trait}
                  selected={catSelected.includes(trait.id)}
                  onToggle={id => toggleTrait(catKey, id)}
                  color={catColor}
                  disabled={totalSelected >= MAX && !catSelected.includes(trait.id)} />
              ))}
            </div>
          </div>
        );
      })}
    </div>
  );
}

function EngineStep({ data, onChange }) {
  const sections = [
    { key:'coreDrive',   label:'Core Drive',   hint:'The engine behind every choice.',     items: CORE_DRIVES,   color: C.amber,  cols: 3 },
    { key:'coreFear',    label:'Core Fear',    hint:'What they most want to avoid.',        items: CORE_FEARS,    color: C.rose,   cols: 3 },
    { key:'surfaceMask', label:'Surface Mask', hint:'How they present to the world.',       items: SURFACE_MASKS, color: C.violet, cols: 2 },
    { key:'hiddenTruth', label:'Hidden Truth', hint:'What quietly drives them underneath.', items: HIDDEN_TRUTHS, color: C.blue,   cols: 2 },
  ];
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 26 }}>
      <p style={{ fontSize: 12.5, color: C.text3, lineHeight: 1.6 }}>
        Pick one for each. These four fields give the model emotional logic to draw from.
      </p>
      {sections.map(sec => (
        <div key={sec.key}>
          <SectionHeader title={sec.label} hint={sec.hint} color={sec.color} />
          <div style={{ display: 'grid', gridTemplateColumns: `repeat(${sec.cols}, 1fr)`, gap: 6 }}>
            {sec.items.map(item => (
              <ItemCard key={item.id} item={item}
                isSelected={data[sec.key] === item.id}
                onToggle={() => onChange({ [sec.key]: data[sec.key] === item.id ? null : item.id })}
                color={sec.color} />
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

function VoiceStep({ data, onChange }) {
  const sections = [
    { key:'sentenceStyle',     label:'Sentence Style',     hint:'How they construct speech.',            items: SENTENCE_STYLES,     color: C.amber,   cols: 2 },
    { key:'honestyStyle',      label:'Honesty Style',      hint:'How direct vs. evasive they are.',      items: HONESTY_STYLES,      color: C.blue,    cols: 2 },
    { key:'emotionalLeakage',  label:'Emotional Leakage',  hint:'How feelings escape their control.',    items: EMOTIONAL_LEAKAGES,  color: C.rose,    cols: 2 },
    { key:'actionFingerprint', label:'Action Fingerprint', hint:'Their physical signature in a scene.',  items: ACTION_FINGERPRINTS, color: C.violet,  cols: 2 },
    { key:'stressPattern',     label:'Stress Pattern',     hint:'How behavior escalates under pressure.',items: STRESS_PATTERNS,     color: C.emerald, cols: 2 },
  ];
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <p style={{ fontSize: 12.5, color: C.text3, lineHeight: 1.6 }}>
        Pick one per category. Voice and behavior make characters feel distinct on the page.
      </p>
      {sections.map(sec => (
        <div key={sec.key}>
          <SectionHeader title={sec.label} hint={sec.hint} color={sec.color} />
          <div style={{ display: 'grid', gridTemplateColumns: `repeat(${sec.cols}, 1fr)`, gap: 6 }}>
            {sec.items.map(item => (
              <ItemCard key={item.id} item={item}
                isSelected={data[sec.key] === item.id}
                onToggle={() => onChange({ [sec.key]: data[sec.key] === item.id ? null : item.id })}
                color={sec.color} />
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

function LimitsStep({ data, onChange }) {
  const selSoftSpots  = data.softSpots     || [];
  const selAvoids     = data.avoidPatterns || [];

  function toggleSoftSpot(id) {
    if (selSoftSpots.includes(id)) onChange({ softSpots: selSoftSpots.filter(s => s !== id) });
    else if (selSoftSpots.length < 3) onChange({ softSpots: [...selSoftSpots, id] });
  }
  function toggleAvoid(id) {
    if (selAvoids.includes(id)) onChange({ avoidPatterns: selAvoids.filter(a => a !== id) });
    else if (selAvoids.length < 5) onChange({ avoidPatterns: [...selAvoids, id] });
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 28 }}>
      <div>
        <SectionHeader title="Soft Spots" hint="— what makes them open up" color={C.emerald} count={selSoftSpots.length} max={3} />
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
          {SOFT_SPOTS.map(item => (
            <ItemCard key={item.id} item={item}
              isSelected={selSoftSpots.includes(item.id)}
              onToggle={() => toggleSoftSpot(item.id)}
              color={C.emerald}
              disabled={!selSoftSpots.includes(item.id) && selSoftSpots.length >= 3} />
          ))}
        </div>
      </div>

      <div>
        <SectionHeader title="Avoid Patterns" hint="— what the model must not do" color={C.rose} count={selAvoids.length} max={5} />
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
          {AVOID_PATTERNS.map(item => (
            <ItemCard key={item.id} item={item}
              isSelected={selAvoids.includes(item.id)}
              onToggle={() => toggleAvoid(item.id)}
              color={C.rose}
              disabled={!selAvoids.includes(item.id) && selAvoids.length >= 5} />
          ))}
        </div>
      </div>
    </div>
  );
}

// ── Relationship row (expandable) ─────────────────────────────────────────────
function RelationshipRow({ char, rel, selfName, onToggle, onUpdate }) {
  const [expanded, setExpanded] = useState(!!rel);
  const known = !!rel;

  function toggleBond(b) {
    const bonds = rel?.bonds || [];
    onUpdate({ bonds: bonds.includes(b) ? bonds.filter(x => x !== b) : [...bonds, b] });
  }
  function toggleDynamic(d) {
    const dynamics = rel?.dynamics || [];
    onUpdate({ dynamics: dynamics.includes(d) ? dynamics.filter(x => x !== d) : [...dynamics, d] });
  }

  return (
    <div style={{
      borderRadius: 10, overflow: 'hidden',
      border: `1px solid ${known ? C.amberDim : C.border}`,
      background: known ? `${C.amber}07` : C.surface2,
      transition: 'border-color 0.15s, background 0.15s',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px 12px', cursor: 'pointer' }}
        onClick={() => {
          if (!known) { onToggle(); setTimeout(() => setExpanded(true), 50); }
          else setExpanded(v => !v);
        }}>
        <div onClick={e => { e.stopPropagation(); onToggle(); if (!known) setExpanded(true); }}
          style={{
            width: 20, height: 20, borderRadius: 6, flexShrink: 0,
            border: `1.5px solid ${known ? C.amber : C.borderMid}`,
            background: known ? C.amber : 'transparent',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            transition: 'all 0.12s', cursor: 'pointer',
          }}>
          {known && <Icon name="check" size={11} color="#0d0b09" />}
        </div>
        <Avatar name={char.name} size={30} />
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: 13, fontWeight: 600, color: C.text }}>{char.name}</div>
          {char.summary && (
            <div style={{ fontSize: 11, color: C.text3, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{char.summary}</div>
          )}
        </div>
        {known && (
          <div style={{ display: 'flex', alignItems: 'center', gap: 5, flexShrink: 0 }}>
            {(rel.bonds || []).slice(0, 1).map(b => (
              <span key={b} style={{ fontSize: 10.5, padding: '2px 7px', borderRadius: 10, background: `${C.amber}20`, color: C.amber, fontWeight: 600 }}>{b}</span>
            ))}
            <Icon name={expanded ? 'chevron-up' : 'chevron-down'} size={13} color={C.text3} />
          </div>
        )}
      </div>

      {known && expanded && (
        <div style={{ borderTop: `1px solid ${C.border}`, padding: '14px 12px', display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div>
            <SectionHeader title="Bond Type" color={C.amber} />
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 5 }}>
              {BOND_TYPES.map(b => {
                const on = (rel.bonds || []).includes(b);
                return (
                  <button key={b} onClick={() => toggleBond(b)} style={{
                    padding: '3px 9px', borderRadius: 20, fontFamily: 'inherit',
                    border: `1px solid ${on ? C.amber : C.border}`,
                    background: on ? `${C.amber}18` : 'transparent',
                    color: on ? C.amber : C.text2, fontSize: 11.5, fontWeight: on ? 600 : 400,
                    cursor: 'pointer', transition: 'all 0.1s',
                  }}>{b}</button>
                );
              })}
            </div>
          </div>
          <div>
            <SectionHeader title="Shared Dynamic" color={C.blue} />
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 5 }}>
              {DYNAMICS.map(d => {
                const on = (rel.dynamics || []).includes(d);
                return (
                  <button key={d} onClick={() => toggleDynamic(d)} style={{
                    padding: '3px 9px', borderRadius: 20, fontFamily: 'inherit',
                    border: `1px solid ${on ? C.blue : C.border}`,
                    background: on ? `${C.blue}18` : 'transparent',
                    color: on ? C.blue : C.text2, fontSize: 11.5, fontWeight: on ? 600 : 400,
                    cursor: 'pointer', transition: 'all 0.1s',
                  }}>{d}</button>
                );
              })}
            </div>
          </div>
          <div>
            <SectionHeader title={`How ${selfName || 'this character'} sees ${char.name}`} color={C.violet} />
            <textarea value={rel.noteAtoB || ''} onChange={e => onUpdate({ noteAtoB: e.target.value })}
              placeholder={`How does ${selfName || 'this character'} perceive and treat ${char.name}?`} rows={1}
              style={{
                width: '100%', background: C.surface3, border: `1px solid ${C.border}`,
                borderRadius: 6, padding: '6px 9px', color: C.text, fontSize: 11.5,
                lineHeight: 1.5, fontFamily: 'inherit', outline: 'none', resize: 'vertical',
              }} />
          </div>
          <div>
            <SectionHeader title={`How ${char.name} sees ${selfName || 'this character'}`} color={C.rose} />
            <textarea value={rel.noteBtoA || ''} onChange={e => onUpdate({ noteBtoA: e.target.value })}
              placeholder={`How does ${char.name} perceive and treat ${selfName || 'this character'}?`} rows={1}
              style={{
                width: '100%', background: C.surface3, border: `1px solid ${C.border}`,
                borderRadius: 6, padding: '6px 9px', color: C.text, fontSize: 11.5,
                lineHeight: 1.5, fontFamily: 'inherit', outline: 'none', resize: 'vertical',
              }} />
          </div>
          <div>
            <SectionHeader title="External Perception" color={C.emerald} />
            <textarea value={rel.noteExternal || ''} onChange={e => onUpdate({ noteExternal: e.target.value })}
              placeholder="How would others describe this relationship from the outside?" rows={1}
              style={{
                width: '100%', background: C.surface3, border: `1px solid ${C.border}`,
                borderRadius: 6, padding: '6px 9px', color: C.text, fontSize: 11.5,
                lineHeight: 1.5, fontFamily: 'inherit', outline: 'none', resize: 'vertical',
              }} />
          </div>
        </div>
      )}
    </div>
  );
}

function RelationshipsStep({ data, onChange, existingChars, selfId }) {
  const rels = data.relationships || [];
  function getRel(charId) { return rels.find(r => r.charId === charId) || null; }
  function toggleChar(charId) {
    if (getRel(charId)) onChange({ relationships: rels.filter(r => r.charId !== charId) });
    else onChange({ relationships: [...rels, { charId, bonds: [], dynamics: [], noteAtoB: '', noteBtoA: '', noteExternal: '' }] });
  }
  function updateRel(charId, patch) {
    onChange({ relationships: rels.map(r => r.charId === charId ? { ...r, ...patch } : r) });
  }
  const others = (existingChars || []).filter(c => c.id !== selfId);
  if (others.length === 0) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 10, padding: '40px 0', color: C.text3 }}>
        <Icon name="users" size={28} color={C.text3} />
        <span style={{ fontSize: 13, textAlign: 'center', lineHeight: 1.6 }}>No other characters yet.<br />Relationships can be added later from the character editor.</span>
      </div>
    );
  }
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      <p style={{ fontSize: 12.5, color: C.text3, lineHeight: 1.6, marginBottom: 4 }}>
        Toggle the characters this character knows, then define the dynamic from each direction.
      </p>
      {others.map(char => (
        <RelationshipRow key={char.id} char={char} rel={getRel(char.id)}
          selfName={data.name}
          onToggle={() => toggleChar(char.id)}
          onUpdate={patch => updateRel(char.id, patch)} />
      ))}
    </div>
  );
}

// ═══════════════════════════════════════════════════════════════════════════════
// V2 WIZARD MODAL
// ═══════════════════════════════════════════════════════════════════════════════

function V2WizardModal({ initialChar, existingChars, onSave, onClose }) {
  const [step, setStep] = useState(0);
  const [data, setData] = useState(() => initialChar ? { ...initialChar } : emptyV2Char());

  function onChange(patch) { setData(d => ({ ...d, ...patch })); }

  function stepDone(i) {
    if (i === 0) return data.name.trim().length > 0;
    if (i === 1) return Object.values(data.traits || {}).flat().length >= 1;
    if (i === 2) return !!(data.coreDrive || data.coreFear || data.surfaceMask || data.hiddenTruth);
    if (i === 3) return !!(data.sentenceStyle || data.stressPattern);
    if (i === 4) return (data.softSpots || []).length > 0 || (data.avoidPatterns || []).length > 0;
    return true;
  }

  const canNext = step === 0 ? data.name.trim().length > 0 : true;
  const isLast  = step === V2_STEPS.length - 1;

  const STEP_COMPONENTS = [
    <ConceptStep       key="concept"       data={data} onChange={onChange} />,
    <PersonalityStep   key="personality"   data={data} onChange={onChange} />,
    <EngineStep        key="engine"        data={data} onChange={onChange} />,
    <VoiceStep         key="voice"         data={data} onChange={onChange} />,
    <LimitsStep        key="limits"        data={data} onChange={onChange} />,
    <RelationshipsStep key="relationships" data={data} onChange={onChange}
      existingChars={existingChars} selfId={data.id} />,
  ];

  return (
    <div onClick={e => e.target === e.currentTarget && onClose()}
      style={{
        position: 'fixed', inset: 0, zIndex: 1200,
        background: 'rgba(0,0,0,0.78)', backdropFilter: 'blur(8px)',
        display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 20,
      }}>
      <div style={{
        width: '100%', maxWidth: 960, height: 680,
        background: C.surface, border: `1px solid ${C.borderMid}`,
        borderRadius: 16, boxShadow: '0 40px 120px rgba(0,0,0,0.8)',
        display: 'flex', flexDirection: 'column', overflow: 'hidden',
      }}>
        {/* Header */}
        <div style={{
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          padding: '14px 20px', borderBottom: `1px solid ${C.border}`, flexShrink: 0,
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <span style={{ fontSize: 14, fontWeight: 600, color: C.text }}>
              {initialChar?.name || 'New Character'}
            </span>
            <span style={{ fontSize: 10, fontFamily: "'DM Mono', monospace", padding: '2px 7px', borderRadius: 5, background: `${C.amber}22`, color: C.amber }}>
              Guided · V2
            </span>
          </div>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <Btn variant="secondary" sz="sm" onClick={() => onSave(data)}>Save Draft</Btn>
            <Btn variant="ghost" sz="icon" onClick={onClose}><Icon name="x" size={15} /></Btn>
          </div>
        </div>

        {/* Body */}
        <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>

          {/* Sidebar */}
          <div style={{ width: 210, flexShrink: 0, borderRight: `1px solid ${C.border}`, display: 'flex', flexDirection: 'column' }}>
            <div style={{
              padding: '20px 16px 16px', borderBottom: `1px solid ${C.border}`,
              display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 8,
            }}>
              <Avatar name={data.name || '?'} size={52} />
              <span style={{
                fontSize: 13, fontWeight: 600,
                color: data.name ? C.text : C.text3,
                fontFamily: data.name ? "'Playfair Display', serif" : 'inherit',
                textAlign: 'center', maxWidth: '100%',
                overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
              }}>
                {data.name || 'Unnamed'}
              </span>
              {(data.sceneRoles || []).length > 0 && (
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4, justifyContent: 'center' }}>
                  {data.sceneRoles.map(r => {
                    const role = SCENE_ROLES.find(x => x.id === r);
                    return role ? (
                      <span key={r} style={{ fontSize: 10, padding: '1px 6px', borderRadius: 10, background: `${C.amber}1a`, color: C.amber }}>{role.label}</span>
                    ) : null;
                  })}
                </div>
              )}
            </div>

            <div style={{ flex: 1, overflowY: 'auto', padding: '10px' }}>
              {V2_STEPS.map((s, i) => {
                const active     = i === step;
                const done       = stepDone(i);
                const accessible = i <= step + 1 || done;
                return (
                  <div key={s.id} onClick={() => accessible && setStep(i)}
                    style={{
                      display: 'flex', alignItems: 'center', gap: 9,
                      padding: '9px 10px', borderRadius: 8,
                      cursor: accessible ? 'pointer' : 'default',
                      background: active ? `${C.amber}16` : 'transparent',
                      border: `1px solid ${active ? C.amberDim : 'transparent'}`,
                      transition: 'all 0.12s', marginBottom: 1,
                    }}>
                    <div style={{
                      width: 22, height: 22, borderRadius: '50%', flexShrink: 0,
                      background: done && !active ? C.emerald : active ? C.amber : C.surface3,
                      border: `1.5px solid ${done && !active ? C.emerald : active ? C.amber : C.border}`,
                      display: 'flex', alignItems: 'center', justifyContent: 'center',
                      fontSize: 10, fontWeight: 700,
                      color: (done && !active) || active ? '#0d0b09' : C.text3,
                      transition: 'all 0.18s',
                    }}>
                      {done && !active ? <Icon name="check" size={11} color="rgba(0,0,0,0.75)" /> : i + 1}
                    </div>
                    <div style={{ minWidth: 0 }}>
                      <div style={{ fontSize: 12.5, fontWeight: active ? 600 : 400, color: active ? C.text : accessible ? C.text2 : C.text3 }}>{s.label}</div>
                      <div style={{ fontSize: 10.5, color: C.text3 }}>{s.sub}</div>
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Progress bar */}
            <div style={{ padding: '10px 14px', borderTop: `1px solid ${C.border}` }}>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 5 }}>
                <span style={{ fontSize: 10, color: C.text3 }}>Completeness</span>
                <span style={{ fontSize: 10, fontFamily: "'DM Mono', monospace", color: C.text3 }}>
                  {V2_STEPS.filter((_, i) => stepDone(i)).length}/{V2_STEPS.length}
                </span>
              </div>
              <div style={{ height: 3, borderRadius: 2, background: C.surface3 }}>
                <div style={{
                  height: '100%', borderRadius: 2, background: C.amber,
                  width: `${(V2_STEPS.filter((_, i) => stepDone(i)).length / V2_STEPS.length) * 100}%`,
                  transition: 'width 0.3s ease',
                }} />
              </div>
            </div>
          </div>

          {/* Step content */}
          <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
            <div style={{ padding: '16px 24px 12px', borderBottom: `1px solid ${C.border}`, flexShrink: 0 }}>
              <div style={{ fontSize: 17, fontWeight: 700, color: C.text, fontFamily: "'Playfair Display', serif" }}>{V2_STEPS[step].label}</div>
              <div style={{ fontSize: 12, color: C.text3, marginTop: 2 }}>{V2_STEPS[step].sub}</div>
            </div>
            <div style={{ flex: 1, overflowY: 'auto', padding: '20px 24px' }}>
              {STEP_COMPONENTS[step]}
            </div>
            <div style={{ padding: '11px 24px', borderTop: `1px solid ${C.border}`, display: 'flex', alignItems: 'center', gap: 8, flexShrink: 0 }}>
              {step > 0 && (
                <Btn variant="secondary" sz="sm" onClick={() => setStep(s => s - 1)}>
                  <Icon name="chevron-left" size={13} color={C.text2} />Back
                </Btn>
              )}
              <div style={{ flex: 1 }} />
              {isLast ? (
                <Btn variant="primary" sz="sm" onClick={() => onSave(data)} disabled={!data.name.trim()}>
                  <Icon name="check" size={13} color="#0d0b09" />Finish &amp; Save
                </Btn>
              ) : (
                <Btn variant="primary" sz="sm" onClick={() => setStep(s => s + 1)} disabled={!canNext}>
                  Next<Icon name="chevron-right" size={13} color="#0d0b09" />
                </Btn>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════════════════════════
// V2 CHARACTER EDITOR (inside EntityManagerModal)
// ═══════════════════════════════════════════════════════════════════════════════

function buildV2ModelContext(data) {
  const lines = [`[Character: ${data.name}]`, `Version: Guided (V2)`];
  if (data.summary) lines.push(`Summary: ${data.summary}`);

  const roles = (data.sceneRoles || []).map(id => SCENE_ROLES.find(r => r.id === id)?.label).filter(Boolean);
  if (roles.length) lines.push(`Scene Roles: ${roles.join(', ')}`);

  const allTraits = [];
  for (const [, ids] of Object.entries(data.traits || {})) {
    for (const id of ids) {
      for (const traits of Object.values(TRAIT_CATEGORIES)) {
        const t = traits.find(t => t.id === id);
        if (t) { allTraits.push(t.label); break; }
      }
    }
  }
  if (allTraits.length) lines.push(`\nPersonality:\n${allTraits.map(t => `- ${t}`).join('\n')}`);

  const drive = CORE_DRIVES.find(d => d.id === data.coreDrive);
  const fear  = CORE_FEARS.find(f => f.id === data.coreFear);
  const mask  = SURFACE_MASKS.find(m => m.id === data.surfaceMask);
  const truth = HIDDEN_TRUTHS.find(t => t.id === data.hiddenTruth);
  const engineEntries = [
    drive  && `- Core Drive: ${drive.label} — ${drive.hover}`,
    fear   && `- Core Fear: ${fear.label} — ${fear.hover}`,
    mask   && `- Surface Mask: ${mask.label} — ${mask.hover}`,
    truth  && `- Hidden Truth: ${truth.label} — ${truth.hover}`,
  ].filter(Boolean);
  if (engineEntries.length) lines.push(`\nInner Engine:\n${engineEntries.join('\n')}`);

  const voicePairs = [
    ['Sentence Style',    SENTENCE_STYLES,     data.sentenceStyle],
    ['Honesty Style',     HONESTY_STYLES,      data.honestyStyle],
    ['Emotional Leakage', EMOTIONAL_LEAKAGES,  data.emotionalLeakage],
    ['Action Fingerprint',ACTION_FINGERPRINTS, data.actionFingerprint],
    ['Stress Pattern',    STRESS_PATTERNS,     data.stressPattern],
  ].filter(([,, id]) => id).map(([label, list, id]) => {
    const found = list.find(i => i.id === id);
    return found ? `- ${label}: ${found.label} — ${found.hover}` : null;
  }).filter(Boolean);
  if (voicePairs.length) lines.push(`\nVoice & Behavior:\n${voicePairs.join('\n')}`);

  const softLabels  = (data.softSpots     || []).map(id => SOFT_SPOTS.find(s => s.id === id)?.label).filter(Boolean);
  const avoidLabels = (data.avoidPatterns || []).map(id => AVOID_PATTERNS.find(a => a.id === id)?.label).filter(Boolean);
  if (softLabels.length)  lines.push(`\nSoft Spots:\n${softLabels.map(s => `- ${s}`).join('\n')}`);
  if (avoidLabels.length) lines.push(`\nAvoid Patterns:\n${avoidLabels.map(a => `- ${a}`).join('\n')}`);

  const rels = (data.relationships || []);
  if (rels.length) {
    const relLines = rels.map(r => {
      const parts = [];
      if ((r.bonds || []).length)    parts.push(`Bonds: ${r.bonds.join(', ')}`);
      if ((r.dynamics || []).length) parts.push(`Dynamics: ${r.dynamics.join(', ')}`);
      if (r.noteAtoB)   parts.push(`${data.name} → them: ${r.noteAtoB}`);
      if (r.noteBtoA)   parts.push(`Them → ${data.name}: ${r.noteBtoA}`);
      if (r.noteExternal) parts.push(`External perception: ${r.noteExternal}`);
      return `- [${r.charId}]\n  ${parts.join('\n  ')}`;
    });
    lines.push(`\nRelationships:\n${relLines.join('\n')}`);
  }

  return lines.join('\n');
}

function V2ProfileView({ data }) {
  const allTraits = [];
  for (const [cat, ids] of Object.entries(data.traits || {})) {
    for (const id of ids) {
      for (const traits of Object.values(TRAIT_CATEGORIES)) {
        const t = traits.find(t => t.id === id);
        if (t) { allTraits.push({ ...t, cat }); break; }
      }
    }
  }
  const sceneRoleLabels = (data.sceneRoles   || []).map(id => SCENE_ROLES.find(r => r.id === id)).filter(Boolean);
  const softLabels      = (data.softSpots    || []).map(id => SOFT_SPOTS.find(s => s.id === id)).filter(Boolean);
  const avoidLabels     = (data.avoidPatterns|| []).map(id => AVOID_PATTERNS.find(a => a.id === id)).filter(Boolean);

  const isEmpty = allTraits.length === 0 && !data.coreDrive && !data.sentenceStyle && sceneRoleLabels.length === 0;
  if (isEmpty) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 10, padding: '40px 0', color: C.text3 }}>
        <Icon name="sliders" size={26} color={C.text3} />
        <span style={{ fontSize: 13, textAlign: 'center', lineHeight: 1.6 }}>No selections yet.<br />Click "Edit in Wizard" to build this character.</span>
      </div>
    );
  }

  function Block({ title, color, children }) {
    return (
      <div style={{ marginBottom: 18 }}>
        <SectionHeader title={title} color={color || C.amber} />
        {children}
      </div>
    );
  }

  return (
    <div>
      {sceneRoleLabels.length > 0 && (
        <Block title="Scene Roles" color={C.amber}>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
            {sceneRoleLabels.map(r => (
              <span key={r.id} title={r.hover} style={{ padding: '4px 10px', borderRadius: 20, background: `${C.amber}18`, border: `1px solid ${C.amberDim}`, fontSize: 12, color: C.amber, fontWeight: 600 }}>{r.label}</span>
            ))}
          </div>
        </Block>
      )}

      {allTraits.length > 0 && (
        <Block title="Personality Traits" color={C.amber}>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 5 }}>
            {allTraits.map(t => (
              <span key={t.id} title={t.hover} style={{ padding: '4px 10px', borderRadius: 20, background: C.surface3, border: `1px solid ${C.border}`, fontSize: 12, color: C.text2 }}>{t.label}</span>
            ))}
          </div>
        </Block>
      )}

      {(data.coreDrive || data.coreFear || data.surfaceMask || data.hiddenTruth) && (
        <Block title="Inner Engine" color={C.amber}>
          {[
            { key:'coreDrive',   label:'Drive', list: CORE_DRIVES,   color: C.amber  },
            { key:'coreFear',    label:'Fear',  list: CORE_FEARS,    color: C.rose   },
            { key:'surfaceMask', label:'Mask',  list: SURFACE_MASKS, color: C.violet },
            { key:'hiddenTruth', label:'Truth', list: HIDDEN_TRUTHS, color: C.blue   },
          ].filter(e => data[e.key]).map(e => {
            const found = e.list.find(i => i.id === data[e.key]);
            return found ? (
              <div key={e.key} style={{ display: 'flex', gap: 8, alignItems: 'baseline', marginBottom: 5 }}>
                <span style={{ fontSize: 9.5, fontWeight: 700, letterSpacing: '0.06em', textTransform: 'uppercase', color: e.color, flexShrink: 0, minWidth: 34 }}>{e.label}</span>
                <span style={{ fontSize: 12.5, fontWeight: 600, color: C.text }}>{found.label}</span>
                <span style={{ fontSize: 11.5, color: C.text3 }}>— {found.hover}</span>
              </div>
            ) : null;
          })}
        </Block>
      )}

      {(data.sentenceStyle || data.honestyStyle || data.emotionalLeakage || data.actionFingerprint || data.stressPattern) && (
        <Block title="Voice &amp; Behavior" color={C.amber}>
          {[
            { key:'sentenceStyle',     label:'Sentence',  list: SENTENCE_STYLES     },
            { key:'honestyStyle',      label:'Honesty',   list: HONESTY_STYLES      },
            { key:'emotionalLeakage',  label:'Leakage',   list: EMOTIONAL_LEAKAGES  },
            { key:'actionFingerprint', label:'Action',    list: ACTION_FINGERPRINTS },
            { key:'stressPattern',     label:'Stress',    list: STRESS_PATTERNS     },
          ].filter(e => data[e.key]).map(e => {
            const found = e.list.find(i => i.id === data[e.key]);
            return found ? (
              <div key={e.key} style={{ display: 'flex', gap: 8, alignItems: 'baseline', marginBottom: 5 }}>
                <span style={{ fontSize: 9.5, fontWeight: 700, letterSpacing: '0.06em', textTransform: 'uppercase', color: C.text3, flexShrink: 0, minWidth: 50 }}>{e.label}</span>
                <span style={{ fontSize: 12.5, fontWeight: 600, color: C.text }}>{found.label}</span>
                <span style={{ fontSize: 11.5, color: C.text3 }}>— {found.hover}</span>
              </div>
            ) : null;
          })}
        </Block>
      )}

      {softLabels.length > 0 && (
        <Block title="Soft Spots" color={C.emerald}>
          {softLabels.map(s => (
            <div key={s.id} style={{ fontSize: 12.5, color: C.text, marginBottom: 3 }}>
              <span style={{ fontWeight: 600 }}>{s.label}</span>
              <span style={{ color: C.text3 }}> — {s.hover}</span>
            </div>
          ))}
        </Block>
      )}

      {avoidLabels.length > 0 && (
        <Block title="Avoid Patterns" color={C.rose}>
          {avoidLabels.map(a => (
            <div key={a.id} style={{ fontSize: 12.5, color: C.text, marginBottom: 3, fontWeight: 600 }}>{a.label}</div>
          ))}
        </Block>
      )}
    </div>
  );
}

function CharacterEditorV2({ char, existingChars, onSave, onOpenWizard }) {
  const [data, setData] = useState({ ...char });
  const [tab,  setTab]  = useState('profile');

  const traitCount  = Object.values(data.traits || {}).flat().length;
  const coreCount   = [data.coreDrive, data.coreFear, data.surfaceMask, data.hiddenTruth].filter(Boolean).length;
  const voiceCount  = [data.sentenceStyle, data.honestyStyle, data.emotionalLeakage, data.actionFingerprint, data.stressPattern].filter(Boolean).length;
  const avoidCount  = (data.avoidPatterns || []).length;
  const relCount    = (data.relationships || []).length;

  const stats = [
    { label:'Traits',    val: traitCount, max: 6,    color: C.amber  },
    { label:'Engine',    val: coreCount,  max: 4,    color: C.violet },
    { label:'Voice',     val: voiceCount, max: 5,    color: C.blue   },
    { label:'Avoids',    val: avoidCount, max: 5,    color: C.rose   },
    { label:'Relations', val: relCount,   max: null },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div style={{ padding: '14px 20px', borderBottom: `1px solid ${C.border}`, display: 'flex', alignItems: 'flex-start', gap: 14, flexShrink: 0 }}>
        <Avatar name={data.name} size={56} />
        <div style={{ flex: 1 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 3 }}>
            <input value={data.name} onChange={e => setData(d => ({ ...d, name: e.target.value }))}
              style={{ background: 'transparent', border: 'none', outline: 'none', fontSize: 17, fontWeight: 700, color: C.text, fontFamily: "'Playfair Display', serif" }} />
            <span style={{ fontSize: 10, fontFamily: "'DM Mono', monospace", padding: '2px 6px', borderRadius: 4, background: `${C.amber}22`, color: C.amber, flexShrink: 0 }}>V2</span>
          </div>
          {data.summary && <div style={{ fontSize: 12, color: C.text3, lineHeight: 1.5, marginBottom: 7 }}>{data.summary}</div>}
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Btn variant="secondary" sz="xs" onClick={onOpenWizard}>
              <Icon name="sliders" size={11} color={C.text2} />Edit in Wizard
            </Btn>
            <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
              <div style={{ width: 6, height: 6, borderRadius: '50%', background: char.inScene ? C.emerald : C.text3 }} />
              <span style={{ fontSize: 11, color: C.text3 }}>{char.inScene ? 'In scene' : 'Off scene'}</span>
            </div>
          </div>
        </div>
      </div>

      <div style={{ padding: '7px 20px', borderBottom: `1px solid ${C.border}`, display: 'flex', gap: 14, flexShrink: 0, background: C.surface2 }}>
        {stats.map(s => (
          <div key={s.label} style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
            <span style={{ fontSize: 10.5, color: C.text3 }}>{s.label}</span>
            <span style={{ fontSize: 10.5, fontFamily: "'DM Mono', monospace", color: s.val > 0 ? (s.color || C.text2) : C.text3 }}>
              {s.val}{s.max ? `/${s.max}` : ''}
            </span>
          </div>
        ))}
      </div>

      <div style={{ display: 'flex', padding: '0 20px', borderBottom: `1px solid ${C.border}`, flexShrink: 0 }}>
        {[
          { id:'profile',       label:'Profile' },
          { id:'relationships', label:'Relationships' },
          { id:'model',         label:'Model Context' },
        ].map(t => (
          <div key={t.id} onClick={() => setTab(t.id)}
            style={{
              padding: '9px 12px', cursor: 'pointer', fontSize: 13,
              fontWeight: tab === t.id ? 600 : 400,
              color: tab === t.id ? C.text : C.text3,
              borderBottom: `2px solid ${tab === t.id ? C.amber : 'transparent'}`,
              transition: 'all 0.12s',
            }}>
            {t.label}
            {t.id === 'relationships' && relCount > 0 && (
              <span style={{ marginLeft: 5, fontSize: 10, fontFamily: "'DM Mono', monospace", color: C.text3 }}>{relCount}</span>
            )}
          </div>
        ))}
      </div>

      <div style={{ flex: 1, overflowY: 'auto', padding: '16px 20px' }}>
        {tab === 'profile' && <V2ProfileView data={data} />}
        {tab === 'relationships' && (
          <RelationshipsStep data={data}
            onChange={patch => setData(d => ({ ...d, ...patch }))}
            existingChars={existingChars} selfId={data.id} />
        )}
        {tab === 'model' && (
          <div>
            <div style={{ fontSize: 11.5, color: C.text3, lineHeight: 1.6, marginBottom: 12 }}>
              Assembled from all taxonomy selections. Sent to the model as character context.
            </div>
            <pre style={{
              background: C.surface2, border: `1px solid ${C.border}`, borderRadius: 8,
              padding: '14px 16px', fontSize: 12, fontFamily: "'DM Mono', monospace",
              color: C.text2, lineHeight: 1.7, whiteSpace: 'pre-wrap', wordBreak: 'break-word',
            }}>{buildV2ModelContext(data)}</pre>
            <Btn variant="ghost" sz="sm" style={{ marginTop: 10, color: C.text3 }}>
              <Icon name="copy" size={12} color={C.text3} />Copy to clipboard
            </Btn>
          </div>
        )}
      </div>

      <div style={{ padding: '11px 20px', borderTop: `1px solid ${C.border}`, display: 'flex', gap: 8, flexShrink: 0 }}>
        <Btn variant="danger" sz="sm"><Icon name="trash" size={12} />Delete</Btn>
        <div style={{ flex: 1 }} />
        <Btn variant="secondary" sz="sm">Discard</Btn>
        <Btn variant="primary" sz="sm" onClick={() => onSave(data)}>
          <Icon name="check" size={12} color="#0d0b09" />Save
        </Btn>
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════════════════════════
// NEW CHARACTER PICKER
// ═══════════════════════════════════════════════════════════════════════════════

function NewCharPickerModal({ onPickV1, onPickV2, onClose }) {
  return (
    <div onClick={e => e.target === e.currentTarget && onClose()}
      style={{
        position: 'fixed', inset: 0, zIndex: 1200,
        background: 'rgba(0,0,0,0.72)', backdropFilter: 'blur(6px)',
        display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 20,
      }}>
      <div style={{
        width: '100%', maxWidth: 460,
        background: C.surface, border: `1px solid ${C.borderMid}`,
        borderRadius: 14, boxShadow: '0 32px 80px rgba(0,0,0,0.7)',
        overflow: 'hidden',
      }}>
        <div style={{
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          padding: '15px 20px', borderBottom: `1px solid ${C.border}`,
        }}>
          <div>
            <span style={{ fontSize: 15, fontWeight: 600, color: C.text }}>Add Character</span>
            <div style={{ fontSize: 11.5, color: C.text3, marginTop: 1 }}>Choose how to build this character.</div>
          </div>
          <Btn variant="ghost" sz="icon" onClick={onClose}><Icon name="x" size={15} /></Btn>
        </div>
        <div style={{ padding: '16px 20px 20px', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
          <PickerOption badge="V1" badgeColor={C.text3} icon="edit"    title="Classic" description="Write the character in your own words. Freeform fields. Full author control." onClick={onPickV1} />
          <PickerOption badge="V2" badgeColor={C.amber} icon="sliders" title="Guided"  description="Trait-based taxonomy wizard. Structured selections that auto-generate model context." highlight onClick={onPickV2} />
        </div>
      </div>
    </div>
  );
}

function PickerOption({ badge, badgeColor, icon, title, description, highlight, onClick }) {
  const [hov, setHov] = useState(false);
  return (
    <div onClick={onClick} onMouseEnter={() => setHov(true)} onMouseLeave={() => setHov(false)}
      style={{
        padding: '18px 16px', borderRadius: 10, cursor: 'pointer',
        border: `1.5px solid ${hov ? (highlight ? C.amber : C.borderMid) : (highlight ? C.amberDim : C.border)}`,
        background: highlight ? `${C.amber}09` : hov ? C.surface3 : C.surface2,
        display: 'flex', flexDirection: 'column', gap: 12, transition: 'all 0.15s',
      }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between' }}>
        <div style={{ width: 36, height: 36, borderRadius: 9, background: highlight ? `${C.amber}22` : C.surface3, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <Icon name={icon} size={16} color={highlight ? C.amber : C.text3} />
        </div>
        <span style={{ fontSize: 10, fontFamily: "'DM Mono', monospace", padding: '2px 6px', borderRadius: 4, background: `${badgeColor}20`, color: badgeColor }}>{badge}</span>
      </div>
      <div>
        <div style={{ fontSize: 14, fontWeight: 700, color: C.text, marginBottom: 5 }}>{title}</div>
        <div style={{ fontSize: 12, color: C.text3, lineHeight: 1.55 }}>{description}</div>
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 4, color: highlight ? C.amber : C.text3 }}>
        <span style={{ fontSize: 12, fontWeight: 500 }}>Choose {title}</span>
        <Icon name="chevron-right" size={12} color={highlight ? C.amber : C.text3} />
      </div>
    </div>
  );
}

Object.assign(window, {
  V2WizardModal,
  CharacterEditorV2,
  NewCharPickerModal,
  emptyV2Char,
  buildV2ModelContext,
});

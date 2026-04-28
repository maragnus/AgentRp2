// sidebar.jsx

function LocationRow({ location, locs, onOpenEntities, onSwitchLocation }) {
  const [hov, setHov] = useState(false);
  const [open, setOpen] = useState(false);
  const rowRef = useRef(null);
  const popupRef = useRef(null);

  useEffect(() => {
    if (!open) return;
    function handleClick(e) {
      if (popupRef.current && !popupRef.current.contains(e.target) &&
          rowRef.current && !rowRef.current.contains(e.target)) {
        setOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [open]);

  return (
    <div style={{ position: 'relative', margin: '1px 4px' }}>
      <div ref={rowRef} onMouseEnter={() => setHov(true)} onMouseLeave={() => setHov(false)}
        onClick={() => setOpen(v => !v)}
        style={{
          display: 'flex', alignItems: 'center', gap: 8, padding: '5px 10px 5px 14px',
          background: hov || open ? C.surface3 : 'transparent',
          transition: 'background 0.1s', borderRadius: 7, cursor: 'pointer',
        }}>
        <div style={{ position: 'relative', flexShrink: 0 }}>
          <div style={{ width: 24, height: 24, borderRadius: '50%', background: C.surface4, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <Icon name="map-pin" size={11} color={C.blue} />
          </div>
        </div>
        <span style={{ flex: 1, fontSize: 12.5, color: C.text, fontWeight: 400, minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{location.name}</span>
        <Icon name={open ? 'chevron-up' : 'chevron-down'} size={11} color={C.text3} style={{ transition: 'color 0.15s', marginRight: 2 }} />
      </div>

      {open && (
        <div ref={popupRef} style={{
          position: 'absolute', top: 'calc(100% + 4px)', left: 0, right: 0,
          background: C.surface3, borderRadius: 9, border: `1px solid ${C.borderMid}`,
          overflow: 'hidden', boxShadow: '0 8px 28px rgba(0,0,0,0.45)',
          zIndex: 100,
        }}>
          <div style={{ padding: '6px 10px 4px', borderBottom: `1px solid ${C.border}` }}>
            <span style={{ fontSize: 10, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: C.text3 }}>Switch Location</span>
          </div>
          {locs.map(l => (
            <div key={l.id}
              onClick={() => { onSwitchLocation(l.id); setOpen(false); }}
              style={{
                display: 'flex', alignItems: 'center', gap: 8, padding: '8px 12px',
                cursor: 'pointer',
                background: l.isActive ? `${C.blue}18` : 'transparent',
                borderLeft: `3px solid ${l.isActive ? C.blue : 'transparent'}`,
                transition: 'background 0.1s',
              }}>
              <Icon name="map-pin" size={10} color={l.isActive ? C.blue : C.text3} />
              <span style={{ fontSize: 12, color: l.isActive ? C.text : C.text2, flex: 1 }}>{l.name}</span>
              {l.isActive && <Icon name="check" size={10} color={C.blue} />}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function CharacterRow({ char, active, onSpeakAs, onToggleScene, onEdit }) {
  const [hov, setHov] = useState(false);
  return (
    <div onMouseEnter={() => setHov(true)} onMouseLeave={() => setHov(false)}
      style={{
        display: 'flex', alignItems: 'center', gap: 8, padding: '5px 10px 5px 14px',
        background: hov ? C.surface3 : 'transparent',
        transition: 'background 0.1s', borderRadius: 7, margin: '1px 4px', cursor: 'default',
      }}>
      {/* Avatar with in-scene indicator */}
      <div style={{ position: 'relative', flexShrink: 0 }}>
        <Avatar name={char.name} size={24} />
        {char.inScene && (
          <div style={{
            position: 'absolute', bottom: 0, right: 0,
            width: 7, height: 7, borderRadius: '50%',
            background: C.emerald, border: `1.5px solid ${C.surface}`,
          }} />
        )}
      </div>
      <span style={{ flex: 1, fontSize: 12.5, color: C.text, fontWeight: 400, minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{char.name}</span>
      {(hov || active) && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Btn variant="ghost" sz="icon" onClick={() => onSpeakAs(char)} title="Speak as" style={{ padding: 4 }}>
            <Icon name="message-sq" size={12} color={C.blue} />
          </Btn>
          <Btn variant="ghost" sz="icon" onClick={() => onToggleScene(char.id)} title={char.inScene ? "Remove from scene" : "Add to scene"} style={{ padding: 4 }}>
            <Icon name="eye" size={12} color={char.inScene ? C.emerald : C.text3} />
          </Btn>
          <Btn variant="ghost" sz="icon" onClick={() => onEdit(char)} title="Edit character" style={{ padding: 4 }}>
            <Icon name="edit" size={12} color={C.text3} />
          </Btn>
        </div>
      )}
    </div>
  );
}

function ChatListItem({ chat, active, onClick }) {
  const [hov, setHov] = useState(false);
  return (
    <div onMouseEnter={() => setHov(true)} onMouseLeave={() => setHov(false)} onClick={onClick}
      style={{
        padding: '7px 12px', cursor: 'pointer', borderRadius: 7, margin: '1px 6px',
        background: active ? C.surface3 : hov ? C.surface2 : 'transparent',
        transition: 'background 0.1s', borderLeft: active ? `2px solid ${C.amber}` : '2px solid transparent',
      }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 5, minWidth: 0 }}>
          {chat.starred && <Icon name="star" size={10} color={C.amber} style={{ flexShrink: 0 }} />}
          <span style={{ fontSize: 12.5, fontWeight: active ? 500 : 400, color: active ? C.text : C.text2, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{chat.title}</span>
        </div>
        <span style={{ fontSize: 10.5, color: C.text3, flexShrink: 0, marginLeft: 6 }}>{chat.updated}</span>
      </div>
      <div style={{ fontSize: 11, color: C.text3, marginTop: 2, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{chat.location}</div>
    </div>
  );
}

function Sidebar({ chars, locs, chats, activeChat, setActiveChat, speakingAs, setSpeakingAs,
    onOpenEntities, onOpenGallery, onOpenPromptLibrary, onOpenModelTuning, onOpenExport, onOpenProviders, onToggleScene, onSwitchLocation, onNewChat }) {
  const [modelOpen, setModelOpen] = useState(false);
  const location = locs.find(l => l.isActive) || locs[0];

  return (
    <div style={{
      width: 256, flexShrink: 0, display: 'flex', flexDirection: 'column',
      background: C.surface, borderRight: `1px solid ${C.border}`,
      height: '100%', overflow: 'hidden',
    }}>
      {/* Header */}
      <div style={{ padding: '14px 14px 10px', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexShrink: 0 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <div style={{
            width: 26, height: 26, borderRadius: 7,
            background: `linear-gradient(135deg, ${C.amber}, oklch(60% 0.16 50))`,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
          }}>
            <Icon name="book-open" size={13} color="#0d0b09" />
          </div>
          <span style={{ fontSize: 14, fontWeight: 700, letterSpacing: '-0.02em', color: C.text }}>AgentRp</span>
        </div>
        <div style={{ display: 'flex', gap: 2 }}>
          <Btn variant="ghost" sz="icon" onClick={onOpenGallery} title="Image gallery">
            <Icon name="image" size={14} color={C.text3} />
          </Btn>
          <Btn variant="ghost" sz="icon" onClick={onOpenPromptLibrary} title="Prompt library">
            <Icon name="feather" size={14} color={C.text3} />
          </Btn>
          <Btn variant="ghost" sz="icon" title="Export" onClick={onOpenExport}>
            <Icon name="download" size={14} color={C.text3} />
          </Btn>
        </div>
      </div>

      {/* New chat */}
      <div style={{ padding: '0 10px 10px', flexShrink: 0 }}>
        <Btn variant="primary" sz="md" onClick={onNewChat} style={{ width: '100%', justifyContent: 'center', borderRadius: 8 }}>
          <Icon name="plus" size={14} color="#0d0b09" />
          New Chat
        </Btn>
      </div>

      <div style={{ flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 0 }}>

        {/* AI Model */}
        <div style={{ flexShrink: 0 }}>
          <div style={{ padding: '8px 14px 4px', display: 'flex', alignItems: 'center', justifyContent: 'space-between', cursor: 'pointer' }}
            onClick={() => setModelOpen(v => !v)}>
            <span style={{ fontSize: 10, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: C.text3 }}>AI Model</span>
            <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
              <Btn variant="ghost" sz="icon" style={{ padding: 2 }} onClick={e => { e.stopPropagation(); onOpenModelTuning(); }} title="Model tuning">
                <Icon name="wrench" size={11} color={C.text3} />
              </Btn>
              <Btn variant="ghost" sz="icon" style={{ padding: 2 }} onClick={e => { e.stopPropagation(); onOpenProviders(); }} title="AI Providers">
                <Icon name="settings" size={11} color={C.text3} />
              </Btn>
              <Icon name={modelOpen ? 'chevron-up' : 'chevron-down'} size={11} color={C.text3} />
            </div>
          </div>
          <div style={{ padding: '2px 14px 6px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <div style={{ width: 6, height: 6, borderRadius: '50%', background: C.emerald, flexShrink: 0 }} />
              <span style={{ fontSize: 12, fontWeight: 500, color: C.text }}>grok-4-1-fast-non-reasoning</span>
            </div>
            <div style={{ fontSize: 11, color: C.text3, marginTop: 2, paddingLeft: 12 }}>Grok / xAI</div>
            {modelOpen && (
              <div style={{ marginTop: 8, padding: '8px 10px', background: C.surface2, borderRadius: 7, border: `1px solid ${C.border}` }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                  <span style={{ fontSize: 11, color: C.text3 }}>Temp</span>
                  <span style={{ fontSize: 11, fontFamily: "'DM Mono', monospace", color: C.text2 }}>0.9</span>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                  <span style={{ fontSize: 11, color: C.text3 }}>Top-P</span>
                  <span style={{ fontSize: 11, fontFamily: "'DM Mono', monospace", color: C.text2 }}>0.9</span>
                </div>
                <Divider margin="6px 0" />
                <div style={{ fontSize: 11, color: C.text3, marginTop: 4 }}>
                  Tok In: <span style={{ color: C.text2, fontFamily: "'DM Mono', monospace" }}>331,152</span> · Out: <span style={{ color: C.text2, fontFamily: "'DM Mono', monospace" }}>18,974</span>
                </div>
              </div>
            )}
          </div>
        </div>

        <Divider />

        {/* Location */}
        <div style={{ flexShrink: 0 }}>
          <div style={{ padding: '8px 14px 4px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: 10, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: C.text3 }}>Location</span>
            <Btn variant="ghost" sz="icon" onClick={() => onOpenEntities('locations')} style={{ padding: 3 }}>
              <Icon name="edit" size={11} color={C.text3} />
            </Btn>
          </div>
          <div style={{ paddingBottom: 4 }}>
            <LocationRow location={location} locs={locs} onOpenEntities={onOpenEntities} onSwitchLocation={onSwitchLocation} />
          </div>
        </div>

        <Divider />

        {/* Characters */}
        <div style={{ flexShrink: 0 }}>
          <div style={{ padding: '8px 14px 4px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: 10, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: C.text3 }}>Characters</span>
            <Btn variant="ghost" sz="icon" onClick={() => onOpenEntities('characters')} style={{ padding: 3 }}>
              <Icon name="edit" size={11} color={C.text3} />
            </Btn>
          </div>
          <div style={{ paddingBottom: 4 }}>
            {/* Narrator */}
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '4px 10px 4px 14px', margin: '1px 4px', borderRadius: 7 }}>
              <div style={{ position: 'relative', flexShrink: 0 }}>
                <div style={{ width: 24, height: 24, borderRadius: '50%', background: C.surface4, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                  <Icon name="book-open" size={11} color={C.text3} />
                </div>
              </div>
              <span style={{ flex: 1, fontSize: 12.5, color: C.text2, fontStyle: 'italic' }}>Narrator</span>
              <Btn variant="ghost" sz="icon" onClick={() => setSpeakingAs(null)} title="Speak as Narrator" style={{ padding: 4 }}>
                <Icon name="message-sq" size={12} color={C.violet} />
              </Btn>
            </div>
            {chars.map(c => (
              <CharacterRow key={c.id} char={c}
                active={speakingAs?.id === c.id}
                onSpeakAs={setSpeakingAs}
                onToggleScene={onToggleScene}
                onEdit={c => onOpenEntities('characters', c.id)} />
            ))}
          </div>
        </div>

        <Divider />

        {/* Scene items */}
        <div style={{ flexShrink: 0 }}>
          <div style={{ padding: '8px 14px 4px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: 10, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: C.text3 }}>Items</span>
            <Btn variant="ghost" sz="icon" onClick={() => onOpenEntities('items')} style={{ padding: 3 }}>
              <Icon name="edit" size={11} color={C.text3} />
            </Btn>
          </div>
          <div style={{ padding: '2px 14px 8px' }}>
            <span style={{ fontSize: 12, color: C.text3, fontStyle: 'italic' }}>No items in scene</span>
          </div>
        </div>

        <Divider />

        {/* Chat list */}
        <div style={{ flex: 1 }}>
          <div style={{ padding: '8px 14px 4px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: 10, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: C.text3 }}>Chats</span>
            <span style={{ fontSize: 10, color: C.text3 }}>Starred first</span>
          </div>
          <div style={{ paddingBottom: 8 }}>
            {chats.map(ch => (
              <ChatListItem key={ch.id} chat={ch}
                active={activeChat?.id === ch.id}
                onClick={() => setActiveChat(ch)} />
            ))}
          </div>
        </div>
      </div>

      {/* Footer */}
      <Divider />
      <div style={{ padding: '8px 14px', display: 'flex', alignItems: 'center', gap: 8, flexShrink: 0 }}>
        <div style={{ width: 26, height: 26, borderRadius: '50%', background: C.surface4, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <Icon name="user" size={13} color={C.text3} />
        </div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: 12, fontWeight: 500, color: C.text }}>Primary User</div>
          <div style={{ fontSize: 10, color: C.text3 }}>Admin</div>
        </div>
        <Btn variant="ghost" sz="icon"><Icon name="settings" size={13} color={C.text3} /></Btn>
      </div>
    </div>
  );
}

Object.assign(window, { Sidebar });

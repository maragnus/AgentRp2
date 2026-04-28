// app.jsx

function App() {
  const [chars, setChars]       = useState(CHARS);
  const [locs, setLocs]         = useState(LOCS);
  const [items, setItems]       = useState(ITEMS);
  const [timeline, setTimeline] = useState(TIMELINE);
  const [chats]                 = useState(CHATS);
  const [activeChat, setActiveChat] = useState(CHATS[0]);
  const [messages, setMessages] = useState(MESSAGES);
  const [speakingAs, setSpeakingAs] = useState(null);

  // Modal state
  const [modal, setModal] = useState(null); // null | 'entities' | 'gallery' | 'generate' | 'aiProviders' | 'promptLibrary' | 'modelTuning'
  const [entityInitType, setEntityInitType] = useState('characters');
  const [entityInitId,   setEntityInitId]   = useState(null);

  function openEntities(type = 'characters', id = null) {
    setEntityInitType(type);
    setEntityInitId(id);
    setModal('entities');
  }

  function handleToggleScene(id) {
    setChars(prev => prev.map(c => c.id === id ? { ...c, inScene: !c.inScene } : c));
  }

  function handleSwitchLocation(id) {
    setLocs(prev => prev.map(l => ({ ...l, isActive: l.id === id })));
  }

  function handleEntityUpdate(type, newList) {
    if (type === 'characters') setChars(newList);
    if (type === 'locations')  setLocs(newList);
    if (type === 'items')      setItems(newList);
    if (type === 'timeline')   setTimeline(newList);
  }

  function handlePost(text, speaker, mode) {
    const author = speaker ? speaker.name : 'Narrator';
    const modeLabel = mode === 'guided' ? 'Guided AI' : mode === 'automatic' ? 'Automatic AI' : 'Manual';
    const newMsg = {
      id: `msg-${Date.now()}`,
      type: 'narrative',
      author,
      mode: modeLabel,
      ts: 'just now',
      body: text,
    };
    setMessages(prev => [...prev, newMsg]);
  }

  function handleDeleteMessage(id) {
    setMessages(prev => prev.filter(m => m.id !== id));
  }

  function handleDeleteBranch(id) {
    setMessages(prev => {
      const idx = prev.findIndex(m => m.id === id);
      return idx === -1 ? prev : prev.slice(0, idx);
    });
  }

  return (
    <div style={{ display: 'flex', height: '100vh', background: C.bg, overflow: 'hidden' }}>
      <Sidebar
        chars={chars}
        locs={locs}
        chats={chats}
        activeChat={activeChat}
        setActiveChat={setActiveChat}
        speakingAs={speakingAs}
        setSpeakingAs={setSpeakingAs}
        onOpenEntities={openEntities}
        onOpenGallery={() => setModal('gallery')}
        onOpenPromptLibrary={() => setModal('promptLibrary')}
        onOpenModelTuning={() => setModal('modelTuning')}
        onOpenExport={() => setModal('export')}
        onOpenProviders={() => setModal('aiProviders')}
        onToggleScene={handleToggleScene}
        onSwitchLocation={handleSwitchLocation}
        onNewChat={() => {}}
      />

      <ChatArea
        chat={activeChat}
        messages={messages}
        chars={chars}
        speakingAs={speakingAs}
        setSpeakingAs={setSpeakingAs}
        onPost={handlePost}
        onDeleteMsg={handleDeleteMessage}
        onDeleteBranch={handleDeleteBranch}
      />

      {modal === 'entities' && (
        <EntityManagerModal
          initialType={entityInitType}
          initialId={entityInitId}
          chars={chars}
          locs={locs}
          items={items}
          timeline={timeline}
          onClose={() => setModal(null)}
          onUpdate={handleEntityUpdate}
        />
      )}

      {modal === 'promptLibrary' && (
        <PromptLibraryModal onClose={() => setModal(null)} />
      )}

      {modal === 'modelTuning' && (
        <ModelTuningModal onClose={() => setModal(null)} />
      )}

      {modal === 'gallery' && (
        <ImageGalleryModal
          onClose={() => setModal(null)}
          onGenerate={() => setModal('generate')}
        />
      )}

      {modal === 'generate' && (
        <GenerateImageModal
          onClose={() => setModal(null)}
          onSaveToGallery={() => {}}
        />
      )}

      {modal === 'aiProviders' && (
        <AIProvidersModal onClose={() => setModal(null)} />
      )}

      {modal === 'export' && (
        <ExportModal
          chars={chars}
          locs={locs}
          items={items}
          timeline={timeline}
          chats={chats}
          activeChat={activeChat}
          onClose={() => setModal(null)}
        />
      )}
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);

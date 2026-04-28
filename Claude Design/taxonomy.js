// taxonomy.js — V2 character taxonomy data (plain JS, no JSX)

window.V2_TAXONOMY = (function () {

  const SCENE_ROLES = [
    { id:'instigator',    label:'Instigator',     hover:'Starts motion.' },
    { id:'anchor',        label:'Anchor',         hover:'Stabilizes others.' },
    { id:'mirror',        label:'Mirror',         hover:'Reveals others by reacting to them.' },
    { id:'complication',  label:'Complication',   hover:'Makes the simple path harder.' },
    { id:'conscience',    label:'Conscience',     hover:'Names the moral cost.' },
    { id:'tempter',       label:'Tempter',        hover:'Offers the risky path.' },
    { id:'wildcard',      label:'Wildcard',       hover:'Breaks the expected rhythm.' },
    { id:'witness',       label:'Witness',        hover:'Notices what others miss.' },
    { id:'protector',     label:'Protector',      hover:'Makes danger personal.' },
    { id:'pressure-valve',label:'Pressure Valve', hover:'Releases tension.' },
    { id:'button-pusher', label:'Button-Pusher',  hover:'Provokes useful reactions.' },
    { id:'mediator',      label:'Mediator',       hover:'Helps others reconnect.' },
  ];

  const TRAIT_CATEGORIES = {
    Conflict: [
      { id:'deadpan-deflector',          label:'Deadpan Deflector',          hover:'Uses calm understatement to deflect pressure.' },
      { id:'bratty-provoker',            label:'Bratty Provoker',            hover:'Teases and tests reactions.' },
      { id:'proud-reactor',              label:'Proud Reactor',              hover:'Takes disrespect seriously.' },
      { id:'soft-avoider',               label:'Soft Avoider',               hover:'Smooths conflict instead of confronting it.' },
      { id:'strategic-de-escalator',     label:'Strategic De-escalator',     hover:'Lowers tension to preserve the goal.' },
      { id:'combative-escalator',        label:'Combative Escalator',        hover:'Pushes harder when pressured.' },
      { id:'passive-aggressive-needler', label:'Passive-Aggressive Needler', hover:'Attacks indirectly through polite barbs.' },
      { id:'boundary-setter',            label:'Boundary Setter',            hover:'Names limits clearly and calmly.' },
    ],
    'Emotional Style': [
      { id:'open-hearted', label:'Open-Hearted', hover:'Shows emotion plainly.' },
      { id:'guarded',      label:'Guarded',      hover:'Feels more than they reveal.' },
      { id:'volatile',     label:'Volatile',     hover:'Reacts quickly and visibly.' },
      { id:'controlled',   label:'Controlled',   hover:'Regulates emotion before acting.' },
      { id:'melodramatic', label:'Melodramatic', hover:'Makes reactions theatrical.' },
      { id:'numb',         label:'Numb',         hover:'Detaches under emotional pressure.' },
    ],
    'Social Style': [
      { id:'charmer',          label:'Charmer',          hover:'Steers with warmth and attention.' },
      { id:'manipulator',      label:'Manipulator',      hover:'Guides choices while hiding the agenda.' },
      { id:'caretaker',        label:'Caretaker',        hover:'Helps, organizes, and comforts.' },
      { id:'commander',        label:'Commander',        hover:'Takes charge under uncertainty.' },
      { id:'observer',         label:'Observer',         hover:'Watches first, acts second.' },
      { id:'social-chameleon', label:'Social Chameleon', hover:'Adapts to the room.' },
      { id:'outsider',         label:'Outsider',         hover:'Does not instinctively follow norms.' },
    ],
    Attachment: [
      { id:'clingy-loyalist',    label:'Clingy Loyalist',    hover:'Seeks closeness and reassurance.' },
      { id:'avoidant-protector', label:'Avoidant Protector', hover:'Cares through action, not admission.' },
      { id:'devoted',            label:'Devoted',            hover:'Prioritizes loyalty even at cost.' },
      { id:'possessive',         label:'Possessive',         hover:'Treats closeness as threatened territory.' },
      { id:'flirtatious',        label:'Flirtatious',        hover:'Turns tension into charged play.' },
      { id:'touch-averse',       label:'Touch-Averse',       hover:'Treats contact as significant.' },
      { id:'touch-affectionate', label:'Touch-Affectionate', hover:'Uses physical closeness to connect.' },
    ],
    Humor: [
      { id:'dry-wit',          label:'Dry Wit',          hover:'Understated, precise humor.' },
      { id:'snarky',           label:'Snarky',           hover:'Sharp sarcasm as armor or attack.' },
      { id:'playful-tease',    label:'Playful Tease',    hover:'Light mockery for connection.' },
      { id:'gallows-humor',    label:'Gallows Humor',    hover:'Jokes when things are bleak.' },
      { id:'self-deprecating', label:'Self-Deprecating', hover:'Defuses tension by mocking self first.' },
    ],
    Agency: [
      { id:'agency-instigator', label:'Instigator',   hover:'Starts motion when scenes stall.' },
      { id:'tester',            label:'Tester',        hover:'Probes people with small challenges.' },
      { id:'fixer',             label:'Fixer',         hover:'Turns emotion into tasks.' },
      { id:'free-spirit',       label:'Free Spirit',   hover:'Resists rigid expectations.' },
      { id:'rule-keeper',       label:'Rule-Keeper',   hover:'Finds safety in structure.' },
      { id:'chaos-gremlin',     label:'Chaos Gremlin', hover:'Disrupts stability for energy or escape.' },
    ],
    'Moral Posture': [
      { id:'principled', label:'Principled', hover:'Acts from firm values.' },
      { id:'pragmatist', label:'Pragmatist', hover:'Chooses what works.' },
      { id:'honorable',  label:'Honorable',  hover:'Cares about fair conduct.' },
      { id:'ruthless',   label:'Ruthless',   hover:'Will pay moral costs to win.' },
      { id:'merciful',   label:'Merciful',   hover:'Looks for the least harmful option.' },
      { id:'cynic',      label:'Cynic',      hover:'Expects selfishness or failure.' },
      { id:'idealist',   label:'Idealist',   hover:'Believes things can be better.' },
    ],
    Vulnerability: [
      { id:'masked-insecure',   label:'Masked Insecure',   hover:'Hides self-doubt behind style.' },
      { id:'approval-seeking',  label:'Approval-Seeking',  hover:'Wants validation and reassurance.' },
      { id:'shame-defensive',   label:'Shame-Defensive',   hover:'Turns embarrassment into defense.' },
      { id:'soft-centered',     label:'Soft-Centered',     hover:'Has an emotional weak point.' },
      { id:'wounded-romantic',  label:'Wounded Romantic',  hover:'Wants connection but expects pain.' },
      { id:'martyr',            label:'Martyr',            hover:'Makes suffering part of usefulness.' },
    ],
  };

  const CORE_DRIVES = [
    { id:'prove-worth',           label:'Prove Worth',           hover:'Needs to feel valuable.' },
    { id:'stay-safe',             label:'Stay Safe',             hover:'Avoids danger and exposure.' },
    { id:'protect-their-people',  label:'Protect Their People',  hover:'Keeps chosen people safe.' },
    { id:'maintain-control',      label:'Maintain Control',      hover:'Prevents chaos and helplessness.' },
    { id:'be-wanted',             label:'Be Wanted',             hover:'Needs to feel chosen.' },
    { id:'be-free',               label:'Be Free',               hover:'Resists confinement and ownership.' },
    { id:'find-truth',            label:'Find Truth',            hover:'Needs to know what is real.' },
    { id:'avoid-shame',           label:'Avoid Shame',           hover:'Hides flaws or failure.' },
    { id:'earn-belonging',        label:'Earn Belonging',        hover:'Tries to become worth keeping.' },
    { id:'win-respect',           label:'Win Respect',           hover:'Wants dignity and competence recognized.' },
    { id:'keep-peace',            label:'Keep Peace',            hover:'Preserves stability.' },
    { id:'experience-life',       label:'Experience Life Fully', hover:'Chases intensity and meaning.' },
    { id:'redeem-themselves',     label:'Redeem Themselves',     hover:'Seeks to make up for guilt.' },
    { id:'preserve-independence', label:'Preserve Independence', hover:'Avoids needing others.' },
  ];

  const CORE_FEARS = [
    { id:'being-abandoned',       label:'Being Abandoned',       hover:'Fears being left behind.' },
    { id:'being-useless',         label:'Being Useless',         hover:'Fears having no value.' },
    { id:'being-controlled',      label:'Being Controlled',      hover:'Fears losing autonomy.' },
    { id:'being-exposed',         label:'Being Exposed',         hover:'Fears being truly seen.' },
    { id:'being-rejected',        label:'Being Rejected',        hover:'Fears not being accepted.' },
    { id:'being-betrayed',        label:'Being Betrayed',        hover:'Fears trust becoming dangerous.' },
    { id:'hurting-others',        label:'Hurting Others',        hover:'Fears causing harm.' },
    { id:'failing-again',         label:'Failing Again',         hover:'Fears repeating old mistakes.' },
    { id:'being-ordinary',        label:'Being Ordinary',        hover:'Fears being forgettable.' },
    { id:'losing-control',        label:'Losing Control',        hover:'Fears unpredictability.' },
    { id:'being-unlovable',       label:'Being Unlovable',       hover:'Fears being too much or not enough.' },
    { id:'depending-on-someone',  label:'Depending on Someone',  hover:'Fears needing others.' },
  ];

  const SURFACE_MASKS = [
    { id:'smug-untouchable',       label:'Smug and Untouchable',       hover:'Acts above it all.' },
    { id:'polite-composed',        label:'Polite and Composed',        hover:'Uses manners as armor.' },
    { id:'charming-effortless',    label:'Charming and Effortless',    hover:'Performs ease and likability.' },
    { id:'cold-detached',          label:'Cold and Detached',          hover:'Keeps emotion distant.' },
    { id:'helpful-capable',        label:'Helpful and Capable',        hover:'Stays useful to feel safe.' },
    { id:'reckless-fearless',      label:'Reckless and Fearless',      hover:'Performs boldness.' },
    { id:'sweet-harmless',         label:'Sweet and Harmless',         hover:'Appears gentle and agreeable.' },
    { id:'funny-unbothered',       label:'Funny and Unbothered',       hover:'Hides behind humor.' },
    { id:'professional-efficient', label:'Professional and Efficient',  hover:'Hides emotion behind competence.' },
    { id:'mysterious-withholding', label:'Mysterious and Withholding', hover:'Reveals little.' },
  ];

  const HIDDEN_TRUTHS = [
    { id:'needs-reassurance',     label:'Needs Reassurance',     hover:'Wants proof they matter.' },
    { id:'feels-too-much',        label:'Feels Too Much',        hover:'Emotion runs deeper than shown.' },
    { id:'wants-to-be-chosen',    label:'Wants to Be Chosen',    hover:'Wants to be preferred, not tolerated.' },
    { id:'afraid-of-being-known', label:'Afraid of Being Known', hover:'Understanding feels risky.' },
    { id:'feels-responsible',     label:'Feels Responsible',     hover:'Carries too much blame.' },
    { id:'craves-freedom',        label:'Craves Freedom',        hover:'Hates being trapped.' },
    { id:'longs-for-rest',        label:'Longs for Rest',        hover:'Wants permission to stop.' },
    { id:'wants-to-trust',        label:'Wants to Trust',        hover:'Hopes someone proves safe.' },
    { id:'fears-their-own-anger', label:'Fears Their Own Anger', hover:'Avoids what rage might reveal.' },
    { id:'still-hopes',           label:'Still Hopes',           hover:'Cynicism is not the whole truth.' },
  ];

  const SENTENCE_STYLES = [
    { id:'terse',      label:'Terse',      hover:'Short and efficient.' },
    { id:'rambling',   label:'Rambling',   hover:'Thinks aloud.' },
    { id:'precise',    label:'Precise',    hover:'Careful and exact.' },
    { id:'blunt',      label:'Blunt',      hover:'Direct, sometimes too direct.' },
    { id:'elegant',    label:'Elegant',    hover:'Polished and rhythmic.' },
    { id:'casual',     label:'Casual',     hover:'Relaxed and everyday.' },
    { id:'formal',     label:'Formal',     hover:'Structured and mannered.' },
    { id:'fragmented', label:'Fragmented', hover:'Broken under pressure.' },
  ];

  const HONESTY_STYLES = [
    { id:'direct',              label:'Direct',              hover:'Says what they mean.' },
    { id:'evasive',             label:'Evasive',             hover:'Dodges direct answers.' },
    { id:'layered',             label:'Layered',             hover:'Speaks with subtext.' },
    { id:'performative',        label:'Performative',        hover:'Shapes answers for effect.' },
    { id:'accidentally-honest', label:'Accidentally Honest', hover:'Truth slips out.' },
  ];

  const EMOTIONAL_LEAKAGES = [
    { id:'gets-quieter', label:'Gets Quieter', hover:'Emotion makes them smaller.' },
    { id:'gets-sharper', label:'Gets Sharper', hover:'Emotion makes them more cutting.' },
    { id:'gets-warmer',  label:'Gets Warmer',  hover:'Emotion makes them softer.' },
    { id:'gets-funnier', label:'Gets Funnier', hover:'Emotion increases humor.' },
    { id:'gets-formal',  label:'Gets Formal',  hover:'Emotion sends them into manners.' },
    { id:'gets-physical',label:'Gets Physical',hover:'Emotion shows through movement.' },
  ];

  const ACTION_FINGERPRINTS = [
    { id:'lounger',          label:'Lounger',          hover:'Claims space casually.' },
    { id:'tidy-avoider',     label:'Tidy Avoider',     hover:'Fidgets through tasks.' },
    { id:'still-watcher',    label:'Still Watcher',    hover:'Goes quiet and observant.' },
    { id:'pacer',            label:'Pacer',            hover:'Moves to think.' },
    { id:'touch-connector',  label:'Touch Connector',  hover:'Communicates through contact.' },
    { id:'space-keeper',     label:'Space Keeper',     hover:'Maintains distance.' },
    { id:'protective-mover', label:'Protective Mover', hover:'Shields others with positioning.' },
    { id:'performer',        label:'Performer',        hover:'Uses expressive movement.' },
    { id:'minimalist',       label:'Minimalist',       hover:'Makes small movements matter.' },
    { id:'restless-spark',   label:'Restless Spark',   hover:'Constant small motion.' },
  ];

  const STRESS_PATTERNS = [
    { id:'sharper-under-pressure',     label:'Sharper Under Pressure',     hover:'Gets more pointed as stress rises.' },
    { id:'quieter-under-pressure',     label:'Quieter Under Pressure',     hover:'Withdraws as stress rises.' },
    { id:'louder-under-pressure',      label:'Louder Under Pressure',      hover:'Gets more expressive as stress rises.' },
    { id:'colder-under-pressure',      label:'Colder Under Pressure',      hover:'Freezes emotion into control.' },
    { id:'funnier-under-pressure',     label:'Funnier Under Pressure',     hover:'Jokes harder as stress rises.' },
    { id:'helpful-under-pressure',     label:'Helpful Under Pressure',     hover:'Converts feelings into tasks.' },
    { id:'controlling-under-pressure', label:'Controlling Under Pressure', hover:'Manages harder as stress rises.' },
    { id:'reckless-under-pressure',    label:'Reckless Under Pressure',    hover:'Acts before thinking.' },
    { id:'appeasing-under-pressure',   label:'Appeasing Under Pressure',   hover:'Smooths and self-erases.' },
    { id:'protective-under-pressure',  label:'Protective Under Pressure',  hover:'Threat makes them decisive.' },
  ];

  const SOFT_SPOTS = [
    { id:'quiet-inclusion',        label:'Quiet Inclusion',        hover:'Being included without pressure.' },
    { id:'practical-care',         label:'Practical Care',         hover:'Help without drama.' },
    { id:'remembered-details',     label:'Remembered Details',     hover:'Someone remembered.' },
    { id:'unasked-loyalty',        label:'Unasked Loyalty',        hover:'Someone stays without being begged.' },
    { id:'gentle-honesty',         label:'Gentle Honesty',         hover:'Truth without cruelty.' },
    { id:'being-trusted',          label:'Being Trusted',          hover:'Someone relies on them.' },
    { id:'being-seen-clearly',     label:'Being Seen Clearly',     hover:'Understood without pressure.' },
    { id:'shared-silence',         label:'Shared Silence',         hover:'Comfort without words.' },
    { id:'competence-recognized',  label:'Competence Recognized',  hover:'Skill is genuinely respected.' },
    { id:'protected-vulnerability',label:'Protected Vulnerability', hover:'Weakness is guarded, not used.' },
  ];

  const AVOID_PATTERNS = [
    { id:'no-random-cruelty',        label:'No Random Cruelty',        hover:'Do not make this character cruel without cause.' },
    { id:'no-instant-vulnerable',    label:'No Instant Vulnerability',  hover:'Do not make this character suddenly confess before the scene earns it.' },
    { id:'no-passive-in-danger',     label:'No Passive in Danger',     hover:'Do not make this character freeze in serious danger unless established.' },
    { id:'no-solve-every-conflict',  label:'No Solving Every Conflict', hover:'Do not make this character immediately repair every emotional conflict.' },
    { id:'no-escalate-every-jab',    label:'No Escalating Every Jab',  hover:'Do not turn casual teasing into a serious fight unless a boundary is crossed.' },
    { id:'no-treat-teasing-injury',  label:'No Treating Teasing as Injury', hover:'Do not make this character wounded by light teasing unless it hits a known vulnerability.' },
    { id:'no-reveal-secrets-early',  label:'No Early Secret Reveals',  hover:'Do not reveal private truths before the narrative hook allows it.' },
    { id:'no-generic-flirty',        label:'No Generic Flirting',      hover:'Do not turn every warm or teasing moment into direct flirting.' },
    { id:'no-ignore-core-kindness',  label:'No Ignoring Core Kindness', hover:'Do not make this character violate their basic loyalty or care for a cheap reaction.' },
    { id:'no-overexplain-feelings',  label:'No Overexplaining Feelings',hover:'Do not have this character narrate emotions plainly if they avoid direct vulnerability.' },
    { id:'no-act-on-unknown-info',   label:'No Psychic Knowledge',     hover:'Do not make this character respond to private information they have not learned.' },
    { id:'no-flatten-into-one-trait',label:'No Flattening',            hover:'Do not reduce this character to only one behavior, joke, emotion, or gimmick.' },
  ];

  const BOND_TYPES = [
    'Close Friend','Romantic Interest','Rival','Mentor','Mentee',
    'Ally','Complicated','Estranged','Family','Colleague','Acquaintance',
  ];

  const DYNAMICS = [
    'Power struggle','Protective','Competitive','Dependent','Avoidant',
    'Charged','Playful rivalry','Unspoken tension','Loyal','Complicated history',
  ];

  const V2_STEPS = [
    { id:'concept',       label:'Concept',      sub:'Name & role' },
    { id:'personality',   label:'Personality',  sub:'Baseline traits' },
    { id:'engine',        label:'Inner Engine', sub:'Drives & fears' },
    { id:'voice',         label:'Voice',        sub:'How they speak' },
    { id:'limits',        label:'Limits',       sub:'Soft spots & avoids' },
    { id:'relationships', label:'Relationships',sub:'Who they know' },
  ];

  function emptyV2Char(id) {
    return {
      id: id || ('c-v2-' + Date.now()),
      name: '', summary: '', version: 'v2', inScene: false,
      sceneRoles: [], traits: {},
      coreDrive: null, coreFear: null, surfaceMask: null, hiddenTruth: null,
      sentenceStyle: null, honestyStyle: null, emotionalLeakage: null,
      actionFingerprint: null, stressPattern: null,
      softSpots: [], avoidPatterns: [], relationships: [],
    };
  }

  function findInTaxonomy(id) {
    const all = [
      ...CORE_DRIVES, ...CORE_FEARS, ...SURFACE_MASKS, ...HIDDEN_TRUTHS,
      ...SENTENCE_STYLES, ...HONESTY_STYLES, ...EMOTIONAL_LEAKAGES,
      ...ACTION_FINGERPRINTS, ...STRESS_PATTERNS, ...SOFT_SPOTS, ...AVOID_PATTERNS,
      ...SCENE_ROLES,
      ...Object.values(TRAIT_CATEGORIES).flat(),
    ];
    return all.find(i => i.id === id) || null;
  }

  return {
    SCENE_ROLES, TRAIT_CATEGORIES,
    CORE_DRIVES, CORE_FEARS, SURFACE_MASKS, HIDDEN_TRUTHS,
    SENTENCE_STYLES, HONESTY_STYLES, EMOTIONAL_LEAKAGES, ACTION_FINGERPRINTS, STRESS_PATTERNS,
    SOFT_SPOTS, AVOID_PATTERNS,
    BOND_TYPES, DYNAMICS,
    V2_STEPS, emptyV2Char, findInTaxonomy,
  };
})();

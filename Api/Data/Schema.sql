CREATE TABLE IF NOT EXISTS users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    first_seen TEXT NOT NULL,
    last_seen TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS facts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER,
    subject TEXT NOT NULL,
    verb TEXT NOT NULL,
    object TEXT NOT NULL,
    predicate_type TEXT NOT NULL,
    sentiment TEXT,
    emotion_intensity INTEGER NOT NULL DEFAULT 0,
    time_context TEXT,
    mentioned_at TEXT,
    created_at TEXT NOT NULL,
    FOREIGN KEY (user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS conversations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER,
    user_input TEXT NOT NULL,
    bot_response TEXT NOT NULL,
    timestamp TEXT NOT NULL,
    session_id TEXT,
    response_category TEXT,
    FOREIGN KEY (user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS greetings (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    text TEXT NOT NULL,
    is_system INTEGER NOT NULL DEFAULT 0,
    persona TEXT,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS greeting_words (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    word TEXT NOT NULL UNIQUE,
    learned_from_user_id INTEGER,
    created_at TEXT NOT NULL,
    FOREIGN KEY (learned_from_user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS response_rules (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    pattern TEXT NOT NULL,
    input_type TEXT NOT NULL,
    is_active INTEGER NOT NULL DEFAULT 1,
    persona TEXT,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS response_rule_responses (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    rule_id INTEGER NOT NULL,
    response_text TEXT NOT NULL,
    FOREIGN KEY (rule_id) REFERENCES response_rules(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS pos_dictionary (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    word TEXT NOT NULL,
    word_type TEXT NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS name_patterns (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    pattern TEXT NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS misspellings (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    wrong_word TEXT NOT NULL UNIQUE,
    correction TEXT NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS bot_responses (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    category TEXT NOT NULL,
    response_text TEXT NOT NULL,
    persona TEXT,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS word_definitions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    word TEXT NOT NULL,
    definition TEXT NOT NULL,
    defined_by_user_id INTEGER,
    created_at TEXT NOT NULL,
    FOREIGN KEY (defined_by_user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS word_links (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    source_word TEXT NOT NULL,
    target_word TEXT NOT NULL,
    link_type TEXT NOT NULL,
    created_by_user_id INTEGER,
    created_at TEXT NOT NULL,
    FOREIGN KEY (created_by_user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS noun_categories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    noun TEXT NOT NULL UNIQUE,
    category TEXT NOT NULL,
    learned_from_user_id INTEGER,
    created_at TEXT NOT NULL,
    FOREIGN KEY (learned_from_user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS bot_commands (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    command TEXT NOT NULL UNIQUE,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS user_bot_names (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL UNIQUE,
    bot_name TEXT NOT NULL,
    created_at TEXT NOT NULL,
    FOREIGN KEY (user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS bot_rename_patterns (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    pattern TEXT NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS emotion_keywords (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    word TEXT NOT NULL UNIQUE,
    sentiment TEXT NOT NULL,
    intensity INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS contractions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    contraction TEXT NOT NULL UNIQUE,
    expansion TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS temporal_expressions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    expression TEXT NOT NULL UNIQUE,
    days_offset INTEGER NOT NULL DEFAULT 0,
    is_range INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS conversation_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_guid TEXT NOT NULL UNIQUE,
    user_id INTEGER,
    started_at TEXT NOT NULL,
    ended_at TEXT,
    turn_count INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (user_id) REFERENCES users(id),
    context_state_json TEXT
);

CREATE TABLE IF NOT EXISTS learned_response_rules (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    pattern TEXT NOT NULL,
    response_template TEXT NOT NULL,
    input_type TEXT NOT NULL,
    learned_from_user_id INTEGER,
    confidence INTEGER NOT NULL DEFAULT 5,
    is_active INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL,
    FOREIGN KEY (learned_from_user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS response_feedback (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    rule_id INTEGER NOT NULL,
    is_learned_rule INTEGER NOT NULL DEFAULT 0,
    user_id INTEGER NOT NULL,
    feedback TEXT NOT NULL,
    correction_text TEXT,
    created_at TEXT NOT NULL,
    FOREIGN KEY (user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS conversation_metrics (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id TEXT NOT NULL,
    user_id INTEGER,
    turn_count INTEGER NOT NULL DEFAULT 0,
    facts_learned INTEGER NOT NULL DEFAULT 0,
    dominant_sentiment TEXT,
    sentiment_trend TEXT,
    topics_discussed INTEGER NOT NULL DEFAULT 0,
    bot_response_stats TEXT,
    avg_response_length INTEGER NOT NULL DEFAULT 0,
    session_length INTEGER NOT NULL DEFAULT 0,
    started_at TEXT NOT NULL,
    ended_at TEXT NOT NULL,
    FOREIGN KEY (user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS response_effectiveness (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    category TEXT NOT NULL,
    avg_session_length_after INTEGER NOT NULL DEFAULT 0,
    used_count INTEGER NOT NULL DEFAULT 0,
    follow_up_rate REAL NOT NULL DEFAULT 0.0,
    last_used TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS story_templates (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    template TEXT NOT NULL,
    category TEXT,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS mad_lib_templates (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    template TEXT NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS jokes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    setup TEXT NOT NULL,
    punchline TEXT NOT NULL,
    category TEXT,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS riddles (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    question TEXT NOT NULL,
    answer TEXT NOT NULL,
    hint TEXT,
    difficulty INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS error_knowledge_entries (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    pattern TEXT NOT NULL,
    suggestion TEXT NOT NULL,
    language TEXT NOT NULL DEFAULT 'general',
    is_learned INTEGER NOT NULL DEFAULT 0,
    used_count INTEGER NOT NULL DEFAULT 0,
    success_count INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS reminders (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    task TEXT NOT NULL,
    due_at TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'pending',
    created_at TEXT NOT NULL,
    FOREIGN KEY (user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS allowed_commands (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    command TEXT NOT NULL UNIQUE,
    is_permanent INTEGER NOT NULL DEFAULT 0,
    expires_at TEXT,
    added_by_user_id INTEGER,
    created_at TEXT NOT NULL,
    FOREIGN KEY (added_by_user_id) REFERENCES users(id)
);

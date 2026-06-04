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
    FOREIGN KEY (user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS greetings (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    text TEXT NOT NULL,
    is_system INTEGER NOT NULL DEFAULT 0,
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
    FOREIGN KEY (user_id) REFERENCES users(id)
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

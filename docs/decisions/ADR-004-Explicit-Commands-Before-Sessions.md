# ADR-004: Explicit commands take priority over active sessions

Status: Accepted

An active dialog previously captured every non-callback message, including
`/start` and `/help`, which left no platform-independent escape path. Registered
slash commands now run before the active session; callback commands remain first,
and unknown slash commands may still be handled by the dialog. Telegram suffixes
such as `/start@bot_name` are removed during normalization.

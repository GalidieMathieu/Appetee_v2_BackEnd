-- Purpose: Enforce unique recovery-token lookup and support per-account issuance throttling.
-- Created: 2026-08-23T00:23:05-06:00
-- Last updated: 2026-08-23T00:23:05-06:00

ALTER TABLE password_reset_tokens
    ADD UNIQUE KEY uq_prt_token_hash (token_hash),
    DROP KEY idx_prt_user_id,
    ADD KEY idx_prt_user_created_at (user_id, created_at);

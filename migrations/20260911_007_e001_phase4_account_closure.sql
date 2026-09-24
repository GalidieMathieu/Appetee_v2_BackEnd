-- Purpose: Adds durable, privacy-minimized cleanup state for account closure.
-- Change reason: Implement E-001 Phase 4 without coupling MySQL deletion to Blob availability.
-- Created: 2026-09-11T00:32:56-06:00
-- Last updated: 2026-09-11T00:32:56-06:00

CREATE TABLE account_closure_cleanup (
    id BIGINT NOT NULL PRIMARY KEY AUTO_INCREMENT,
    former_user_id INT NULL,
    profile_image_url VARCHAR(255) NULL,
    status VARCHAR(16) NOT NULL DEFAULT 'pending',
    attempt_count INT UNSIGNED NOT NULL DEFAULT 0,
    next_attempt_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_error_code VARCHAR(100) NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    completed_at TIMESTAMP NULL,

    KEY idx_account_closure_cleanup_due (status, next_attempt_at, id),

    CONSTRAINT chk_account_closure_cleanup_status
        CHECK (status IN ('pending', 'completed')),
    CONSTRAINT chk_account_closure_cleanup_attempt_count
        CHECK (attempt_count <= 1000000),
    CONSTRAINT chk_account_closure_cleanup_scrubbing
        CHECK (
            (status = 'pending' AND former_user_id IS NOT NULL)
            OR (status = 'completed' AND former_user_id IS NULL AND profile_image_url IS NULL)
        )
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

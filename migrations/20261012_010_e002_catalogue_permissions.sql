-- E-002 additive upgrade. Preserve existing catalogue rows and grant no permissions.
-- MySQL DDL may commit implicitly: backup and use the documented forward-recovery procedure.
ALTER TABLE users
    ADD COLUMN is_active BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN security_version BIGINT NOT NULL DEFAULT 0,
    ADD CONSTRAINT chk_users_security_version CHECK (security_version >= 0);
ALTER TABLE recipes
    ADD COLUMN resource_version BIGINT NOT NULL DEFAULT 1,
    ADD COLUMN created_by_user_id INT NULL,
    ADD COLUMN updated_by_user_id INT NULL,
    ADD CONSTRAINT chk_recipes_resource_version CHECK (resource_version > 0);
ALTER TABLE ingredients
    ADD COLUMN resource_version BIGINT NOT NULL DEFAULT 1,
    ADD COLUMN created_by_user_id INT NULL,
    ADD COLUMN updated_by_user_id INT NULL,
    ADD CONSTRAINT chk_ingredients_resource_version CHECK (resource_version > 0);
CREATE TABLE account_permissions (
    user_id INT NOT NULL,
    permission VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    granted_at DATETIME(6) NOT NULL,
    granted_by_user_id INT NULL,
    PRIMARY KEY (user_id, permission),
    CONSTRAINT fk_account_permissions_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT chk_account_permissions_code CHECK (permission IN ('Catalog.ReadAdmin','Catalog.Write'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
CREATE TABLE administrative_audit (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    actor_user_id INT NULL,
    actor_reference VARCHAR(120) NOT NULL,
    operation VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    resource_type VARCHAR(20) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    resource_id INT NOT NULL,
    resource_version BIGINT NULL,
    permission VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NULL,
    occurred_at DATETIME(6) NOT NULL,
    correlation_id VARCHAR(128) NOT NULL,
    outcome VARCHAR(20) CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'succeeded',
    INDEX idx_administrative_audit_resource (resource_type,resource_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

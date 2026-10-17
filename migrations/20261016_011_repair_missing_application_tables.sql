-- Forward-only repair for databases partially provisioned through migrations 007-010.
-- Run against the explicitly selected application database, never a system database.
-- No USE, resets, catalogue imports, grants, data changes, or migration-history adoption.
-- Existing rows/tables are retained. DDL commits implicitly; retry after a failed run.
-- Review migrations/README.md first. Missing historical data cannot be reconstructed here.

DELIMITER $$

DROP PROCEDURE IF EXISTS repair_missing_application_tables_011$$

CREATE PROCEDURE repair_missing_application_tables_011()
BEGIN
    IF DATABASE() IS NULL OR DATABASE() IN ('mysql', 'sys', 'information_schema', 'performance_schema') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '011: explicitly select the application database';
    END IF;

    -- A repair, not a substitute for the preceding migrations or an empty-database bootstrap.
    IF (SELECT COUNT(*) FROM information_schema.tables
        WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE'
          AND table_name IN ('users', 'meal_plan_meal_items', 'administrative_audit')) <> 3 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '011: required parent/application tables are missing';
    END IF;
    IF (SELECT COUNT(*) FROM information_schema.columns
        WHERE table_schema = DATABASE() AND table_name = 'users'
          AND column_name IN ('calculation_sex', 'birth_year', 'height_cm', 'weight_kg',
                              'is_active', 'security_version')) <> 6 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '011: apply and verify preceding profile/permission migrations first';
    END IF;

    -- Do not silently accept a view or an incompatible existing table.
    IF EXISTS (SELECT 1 FROM information_schema.tables
        WHERE table_schema = DATABASE()
          AND table_name IN ('meal_plan_manual_items', 'account_closure_cleanup', 'account_permissions')
          AND (table_type <> 'BASE TABLE' OR engine <> 'InnoDB'
               OR CAST(table_name AS BINARY) NOT IN ('meal_plan_manual_items', 'account_closure_cleanup', 'account_permissions'))) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '011: incompatible existing repair target; manual schema review required';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables
        WHERE table_schema = DATABASE() AND table_name = 'meal_plan_manual_items')
       AND (SELECT COUNT(*) FROM information_schema.columns
        WHERE table_schema = DATABASE() AND table_name = 'meal_plan_manual_items' AND is_nullable = 'NO'
          AND ((column_name = 'meal_plan_meal_item_id' AND column_type = 'int')
            OR (column_name = 'name' AND column_type = 'varchar(120)')
            OR (column_name IN ('calories_kcal', 'protein_g', 'carbs_g', 'fat_g') AND column_type = 'decimal(10,2)'))) <> 6 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '011: incompatible manual item columns; no tables changed';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables
        WHERE table_schema = DATABASE() AND table_name = 'account_closure_cleanup')
       AND (SELECT COUNT(*) FROM information_schema.columns
        WHERE table_schema = DATABASE() AND table_name = 'account_closure_cleanup'
          AND ((column_name = 'id' AND column_type = 'bigint' AND is_nullable = 'NO' AND extra LIKE '%auto_increment%')
            OR (column_name = 'former_user_id' AND column_type = 'int' AND is_nullable = 'YES')
            OR (column_name = 'profile_image_url' AND column_type = 'varchar(255)' AND is_nullable = 'YES')
            OR (column_name = 'status' AND column_type = 'varchar(16)' AND is_nullable = 'NO')
            OR (column_name = 'attempt_count' AND column_type = 'int unsigned' AND is_nullable = 'NO')
            OR (column_name IN ('next_attempt_at', 'created_at', 'updated_at') AND column_type = 'timestamp' AND is_nullable = 'NO')
            OR (column_name = 'last_error_code' AND column_type = 'varchar(100)' AND is_nullable = 'YES')
            OR (column_name = 'completed_at' AND column_type = 'timestamp' AND is_nullable = 'YES'))) <> 10 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '011: incompatible account cleanup columns; no tables changed';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables
        WHERE table_schema = DATABASE() AND table_name = 'account_permissions')
       AND (SELECT COUNT(*) FROM information_schema.columns
        WHERE table_schema = DATABASE() AND table_name = 'account_permissions'
          AND ((column_name = 'user_id' AND column_type = 'int' AND is_nullable = 'NO')
            OR (column_name = 'permission' AND column_type = 'varchar(40)' AND is_nullable = 'NO' AND collation_name = 'ascii_bin')
            OR (column_name = 'granted_at' AND column_type = 'datetime(6)' AND is_nullable = 'NO')
            OR (column_name = 'granted_by_user_id' AND column_type = 'int' AND is_nullable = 'YES'))) <> 4 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '011: incompatible account permission columns; no tables changed';
    END IF;

    -- Existing security/integrity boundaries must not be silently bypassed by IF NOT EXISTS.
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'meal_plan_manual_items')
       AND ((SELECT COUNT(*) FROM information_schema.table_constraints
             WHERE constraint_schema = DATABASE() AND table_name = 'meal_plan_manual_items'
               AND constraint_type = 'CHECK' AND enforced = 'YES'
               AND constraint_name IN ('chk_meal_plan_manual_items_name', 'chk_meal_plan_manual_items_calories',
                   'chk_meal_plan_manual_items_protein', 'chk_meal_plan_manual_items_carbs', 'chk_meal_plan_manual_items_fat')) <> 5
         OR NOT EXISTS (SELECT 1 FROM information_schema.referential_constraints
             WHERE constraint_schema = DATABASE() AND table_name = 'meal_plan_manual_items'
               AND constraint_name = 'fk_meal_plan_manual_items_parent'
               AND referenced_table_name = 'meal_plan_meal_items' AND delete_rule = 'CASCADE')) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '011: incompatible manual item constraints; no tables changed';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'account_closure_cleanup')
       AND ((SELECT COUNT(*) FROM information_schema.table_constraints
             WHERE constraint_schema = DATABASE() AND table_name = 'account_closure_cleanup'
               AND constraint_type = 'CHECK' AND enforced = 'YES'
               AND constraint_name IN ('chk_account_closure_cleanup_status', 'chk_account_closure_cleanup_attempt_count',
                                      'chk_account_closure_cleanup_scrubbing')) <> 3
         OR NOT EXISTS (SELECT 1 FROM information_schema.statistics
             WHERE table_schema = DATABASE() AND table_name = 'account_closure_cleanup'
               AND index_name = 'idx_account_closure_cleanup_due')) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '011: incompatible account cleanup constraints; no tables changed';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'account_permissions')
       AND (NOT EXISTS (SELECT 1 FROM information_schema.table_constraints
             WHERE constraint_schema = DATABASE() AND table_name = 'account_permissions'
               AND constraint_name = 'chk_account_permissions_code' AND constraint_type = 'CHECK' AND enforced = 'YES')
         OR NOT EXISTS (SELECT 1 FROM information_schema.referential_constraints
             WHERE constraint_schema = DATABASE() AND table_name = 'account_permissions'
               AND constraint_name = 'fk_account_permissions_user'
               AND referenced_table_name = 'users' AND delete_rule = 'CASCADE')) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '011: incompatible account permission constraints; no tables changed';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'meal_plan_manual_items')
       AND (SELECT COUNT(*) FROM information_schema.key_column_usage
            WHERE constraint_schema = DATABASE() AND table_name = 'meal_plan_manual_items'
              AND constraint_name = 'PRIMARY' AND column_name = 'meal_plan_meal_item_id' AND ordinal_position = 1) <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '011: incompatible manual item primary key; no tables changed';
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'account_closure_cleanup')
       AND (SELECT COUNT(*) FROM information_schema.key_column_usage
            WHERE constraint_schema = DATABASE() AND table_name = 'account_closure_cleanup'
              AND constraint_name = 'PRIMARY' AND column_name = 'id' AND ordinal_position = 1) <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '011: incompatible account cleanup primary key; no tables changed';
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'account_permissions')
       AND ((SELECT COUNT(*) FROM information_schema.key_column_usage
             WHERE constraint_schema = DATABASE() AND table_name = 'account_permissions' AND constraint_name = 'PRIMARY') <> 2
         OR (SELECT COUNT(*) FROM information_schema.key_column_usage
             WHERE constraint_schema = DATABASE() AND table_name = 'account_permissions' AND constraint_name = 'PRIMARY'
               AND ((column_name = 'user_id' AND ordinal_position = 1) OR (column_name = 'permission' AND ordinal_position = 2))) <> 2) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '011: incompatible account permission primary key; no tables changed';
    END IF;

    -- All application DDL stays inside this call, so SQL editors cannot bypass preflight.
    CREATE TABLE IF NOT EXISTS meal_plan_manual_items (
        meal_plan_meal_item_id INT NOT NULL PRIMARY KEY,
        name VARCHAR(120) NOT NULL,
        calories_kcal DECIMAL(10,2) NOT NULL,
        protein_g DECIMAL(10,2) NOT NULL,
        carbs_g DECIMAL(10,2) NOT NULL,
        fat_g DECIMAL(10,2) NOT NULL,

        CONSTRAINT fk_meal_plan_manual_items_parent
            FOREIGN KEY (meal_plan_meal_item_id) REFERENCES meal_plan_meal_items(id)
            ON DELETE CASCADE ON UPDATE RESTRICT,
        CONSTRAINT chk_meal_plan_manual_items_name
            CHECK (CHAR_LENGTH(TRIM(name)) > 0),
        CONSTRAINT chk_meal_plan_manual_items_calories
            CHECK (calories_kcal BETWEEN 0.01 AND 20000),
        CONSTRAINT chk_meal_plan_manual_items_protein
            CHECK (protein_g BETWEEN 0.01 AND 5000),
        CONSTRAINT chk_meal_plan_manual_items_carbs
            CHECK (carbs_g BETWEEN 0.01 AND 5000),
        CONSTRAINT chk_meal_plan_manual_items_fat
            CHECK (fat_g BETWEEN 0.01 AND 5000)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

    CREATE TABLE IF NOT EXISTS account_closure_cleanup (
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

    CREATE TABLE IF NOT EXISTS account_permissions (
            user_id INT NOT NULL,
            permission VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
            granted_at DATETIME(6) NOT NULL,
            granted_by_user_id INT NULL,
            PRIMARY KEY (user_id, permission),
            CONSTRAINT fk_account_permissions_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
            CONSTRAINT chk_account_permissions_code CHECK (permission IN ('Catalog.ReadAdmin','Catalog.Write'))
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
END$$

CALL repair_missing_application_tables_011()$$
DROP PROCEDURE repair_missing_application_tables_011$$

DELIMITER ;


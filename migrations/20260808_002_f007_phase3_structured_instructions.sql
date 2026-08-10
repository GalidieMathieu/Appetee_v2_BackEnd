DELIMITER $$

DROP PROCEDURE IF EXISTS migrate_f007_phase3_structured_instructions$$

CREATE PROCEDURE migrate_f007_phase3_structured_instructions()
BEGIN
    DECLARE done INT DEFAULT 0;
    DECLARE recipe_id INT;
    DECLARE existing_instructions JSON;
    DECLARE migrated_instructions JSON;
    DECLARE step_value JSON;
    DECLARE step_type VARCHAR(16);
    DECLARE step_title TEXT;
    DECLARE step_instruction TEXT;
    DECLARE step_index INT;
    DECLARE step_count INT;

    DECLARE recipe_cursor CURSOR FOR
        SELECT id, instructions
        FROM recipes
        ORDER BY id;
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = 1;

    OPEN recipe_cursor;

    recipe_loop: LOOP
        FETCH recipe_cursor INTO recipe_id, existing_instructions;
        IF done = 1 THEN
            LEAVE recipe_loop;
        END IF;

        IF JSON_TYPE(existing_instructions) <> 'ARRAY' THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'F-007 Phase 3 requires recipe instructions to be a JSON array';
        END IF;

        SET migrated_instructions = JSON_ARRAY();
        SET step_index = 0;
        SET step_count = JSON_LENGTH(existing_instructions);

        WHILE step_index < step_count DO
            SET step_value = JSON_EXTRACT(
                existing_instructions,
                CONCAT('$[', step_index, ']'));
            SET step_type = JSON_TYPE(step_value);

            IF step_type = 'STRING' THEN
                SET step_instruction = TRIM(JSON_UNQUOTE(step_value));
                IF step_instruction = '' THEN
                    SET step_title = '';
                ELSE
                    SET step_title = CONCAT('Step ', step_index + 1);
                END IF;
            ELSEIF step_type = 'OBJECT' THEN
                SET step_title = TRIM(COALESCE(
                    JSON_UNQUOTE(JSON_EXTRACT(step_value, '$.title')),
                    ''));
                SET step_instruction = TRIM(COALESCE(
                    JSON_UNQUOTE(JSON_EXTRACT(step_value, '$.instruction')),
                    ''));
            ELSE
                SIGNAL SQLSTATE '45000'
                    SET MESSAGE_TEXT = 'F-007 Phase 3 found an unsupported recipe instruction value';
            END IF;

            IF step_title = '' AND step_instruction = '' THEN
                SET migrated_instructions = migrated_instructions;
            ELSEIF step_title = '' OR step_instruction = '' THEN
                SIGNAL SQLSTATE '45000'
                    SET MESSAGE_TEXT = 'F-007 Phase 3 requires both title and instruction for every nonblank step';
            ELSE
                SET migrated_instructions = JSON_ARRAY_APPEND(
                    migrated_instructions,
                    '$',
                    JSON_OBJECT(
                        'title', step_title,
                        'instruction', step_instruction));
            END IF;

            SET step_index = step_index + 1;
        END WHILE;

        IF JSON_LENGTH(migrated_instructions) = 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'F-007 Phase 3 requires every recipe to retain at least one complete instruction step';
        END IF;

        UPDATE recipes
        SET instructions = migrated_instructions
        WHERE id = recipe_id;
    END LOOP;

    CLOSE recipe_cursor;
END$$

CALL migrate_f007_phase3_structured_instructions()$$
DROP PROCEDURE migrate_f007_phase3_structured_instructions$$

DELIMITER ;

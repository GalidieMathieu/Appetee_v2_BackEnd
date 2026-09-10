-- GENERATED FILE. Source of truth: data/DATASET_SPEC.md and data/tools/shared/diet-compatibility.mjs
INSERT INTO diets (id, name) VALUES
  (1, 'Vegetarian'),
  (2, 'Vegan'),
  (3, 'Pescatarian'),
  (4, 'Keto'),
  (5, 'Paleo'),
  (6, 'Flexitarian'),
  (7, 'Gluten Free'),
  (8, 'Lactose Free')
ON DUPLICATE KEY UPDATE name = VALUES(name);

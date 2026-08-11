-- Appetee shared reference seed data. Dataset JSON remains authoritative for
-- ingredients and recipes; this file contains only stable reference values.
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

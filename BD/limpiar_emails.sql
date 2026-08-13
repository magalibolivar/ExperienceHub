-- Residuo WardrobeFlow: emails con dominio @wardrobeflow.com → @experiencehub.com
USE ExperienceHubDB;
SET NOCOUNT ON;
BEGIN TRAN;

UPDATE Empleado SET Email = REPLACE(Email, '@wardrobeflow.com', '@experiencehub.com')
WHERE Email LIKE '%@wardrobeflow.com';

UPDATE Usuario  SET Email = REPLACE(Email, '@wardrobeflow.com', '@experiencehub.com')
WHERE Email LIKE '%@wardrobeflow.com';

COMMIT;

SELECT 'Empleado' AS Tabla, IdEmpleado AS Id, Email FROM Empleado
UNION ALL
SELECT 'Usuario', IdUsuario, Email FROM Usuario WHERE Email IS NOT NULL AND Email <> ''
ORDER BY Tabla, Id;

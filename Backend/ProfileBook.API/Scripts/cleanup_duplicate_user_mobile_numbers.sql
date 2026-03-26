/*
Run this only after reviewing the mobile numbers with the project owner.
It fixes the duplicate mobile numbers currently present in ProfileBookDB
and then creates the unique mobile index if the duplicates are gone.

Current duplicates found during smoke testing on March 24, 2026:
1. 9000000000 -> users 6012 (u1287054), 6013 (u287062)
2. 9550459945 -> user 5008 (GVSRehan), admin 3 (Gurram Venkata Sai Rehan)
*/

BEGIN TRANSACTION;

SELECT [MobileNumber], COUNT(*) AS [DuplicateCount]
FROM [Users]
GROUP BY [MobileNumber]
HAVING COUNT(*) > 1;

SELECT [UserId], [Username], [Email], [Role], [MobileNumber]
FROM [Users]
WHERE [MobileNumber] IN ('9000000000', '9550459945')
ORDER BY [MobileNumber], [UserId];

/*
Pick the correct mobile number for each account, then replace the placeholders below.
Example:
UPDATE [Users] SET [MobileNumber] = '9000000001' WHERE [UserId] = 6012;
UPDATE [Users] SET [MobileNumber] = '9000000002' WHERE [UserId] = 6013;
UPDATE [Users] SET [MobileNumber] = '9550459946' WHERE [UserId] = 5008;
-- Keep main admin user 3 unchanged if that number is the correct one.
*/

-- UPDATE [Users] SET [MobileNumber] = '__NEW_NUMBER_FOR_6012__' WHERE [UserId] = 6012;
-- UPDATE [Users] SET [MobileNumber] = '__NEW_NUMBER_FOR_6013__' WHERE [UserId] = 6013;
-- UPDATE [Users] SET [MobileNumber] = '__NEW_NUMBER_FOR_5008__' WHERE [UserId] = 5008;
-- UPDATE [Users] SET [MobileNumber] = '__NEW_NUMBER_FOR_3__' WHERE [UserId] = 3;

SELECT [MobileNumber], COUNT(*) AS [DuplicateCount]
FROM [Users]
GROUP BY [MobileNumber]
HAVING COUNT(*) > 1;

IF NOT EXISTS (
    SELECT [MobileNumber]
    FROM [Users]
    GROUP BY [MobileNumber]
    HAVING COUNT(*) > 1
)
AND NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Users_MobileNumber'
      AND object_id = OBJECT_ID(N'[Users]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_MobileNumber] ON [Users] ([MobileNumber]);
END

COMMIT TRANSACTION;

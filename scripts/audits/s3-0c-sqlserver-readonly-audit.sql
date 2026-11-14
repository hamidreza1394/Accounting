/*
S3-0c read-only SQL Server metadata audit
Purpose: inspect the deployed Core and Trade schema plus EF migration history.
Safety: SELECT-only. No DDL, DML, temp tables, or database changes.
Run this while connected to the target database in SSMS.
Do not include connection strings or credentials in shared output.
*/
SET NOCOUNT ON;

-- 1) Database identity and engine version
SELECT
    DB_NAME() AS DatabaseName,
    @@SERVERNAME AS ServerName,
    CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS ProductVersion,
    CAST(SERVERPROPERTY('Edition') AS nvarchar(128)) AS Edition,
    SYSDATETIMEOFFSET() AS CapturedAt;

-- 2) User tables and schemas
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    t.create_date AS CreatedAt,
    t.modify_date AS ModifiedAt
FROM sys.tables AS t
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0
ORDER BY s.name, t.name;

-- 3) Columns, SQL types, nullability, identity, defaults and computed expressions
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    c.column_id AS ColumnOrdinal,
    c.name AS ColumnName,
    ty.name AS SqlType,
    c.max_length AS MaxLengthBytes,
    c.precision AS NumericPrecision,
    c.scale AS NumericScale,
    c.is_nullable AS IsNullable,
    c.is_identity AS IsIdentity,
    dc.definition AS DefaultDefinition,
    cc.definition AS ComputedDefinition
FROM sys.tables AS t
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
INNER JOIN sys.columns AS c ON c.object_id = t.object_id
INNER JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints AS dc
    ON dc.parent_object_id = c.object_id
   AND dc.parent_column_id = c.column_id
LEFT JOIN sys.computed_columns AS cc
    ON cc.object_id = c.object_id
   AND cc.column_id = c.column_id
WHERE t.is_ms_shipped = 0
ORDER BY s.name, t.name, c.column_id;

-- 4) Primary keys, unique constraints, indexes and included columns
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    i.is_primary_key AS IsPrimaryKey,
    i.is_unique AS IsUnique,
    i.is_unique_constraint AS IsUniqueConstraint,
    i.is_disabled AS IsDisabled,
    i.has_filter AS HasFilter,
    i.filter_definition AS FilterDefinition,
    ic.key_ordinal AS KeyOrdinal,
    ic.is_included_column AS IsIncludedColumn,
    c.name AS ColumnName,
    ic.is_descending_key AS IsDescending
FROM sys.tables AS t
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
INNER JOIN sys.indexes AS i ON i.object_id = t.object_id
INNER JOIN sys.index_columns AS ic
    ON ic.object_id = i.object_id
   AND ic.index_id = i.index_id
INNER JOIN sys.columns AS c
    ON c.object_id = ic.object_id
   AND c.column_id = ic.column_id
WHERE t.is_ms_shipped = 0
  AND i.index_id > 0
ORDER BY s.name, t.name, i.name, ic.key_ordinal, ic.index_column_id;

-- 5) Foreign keys, ordered column pairs and referential actions
SELECT
    ps.name AS ParentSchema,
    pt.name AS ParentTable,
    fk.name AS ForeignKeyName,
    fkc.constraint_column_id AS ColumnOrdinal,
    pc.name AS ParentColumn,
    rs.name AS ReferencedSchema,
    rt.name AS ReferencedTable,
    rc.name AS ReferencedColumn,
    fk.delete_referential_action_desc AS OnDelete,
    fk.update_referential_action_desc AS OnUpdate,
    fk.is_disabled AS IsDisabled,
    fk.is_not_trusted AS IsNotTrusted
FROM sys.foreign_keys AS fk
INNER JOIN sys.foreign_key_columns AS fkc
    ON fkc.constraint_object_id = fk.object_id
INNER JOIN sys.tables AS pt ON pt.object_id = fk.parent_object_id
INNER JOIN sys.schemas AS ps ON ps.schema_id = pt.schema_id
INNER JOIN sys.columns AS pc
    ON pc.object_id = fkc.parent_object_id
   AND pc.column_id = fkc.parent_column_id
INNER JOIN sys.tables AS rt ON rt.object_id = fk.referenced_object_id
INNER JOIN sys.schemas AS rs ON rs.schema_id = rt.schema_id
INNER JOIN sys.columns AS rc
    ON rc.object_id = fkc.referenced_object_id
   AND rc.column_id = fkc.referenced_column_id
ORDER BY ps.name, pt.name, fk.name, fkc.constraint_column_id;

-- 6) CHECK constraints
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    cc.name AS ConstraintName,
    cc.definition AS Definition,
    cc.is_disabled AS IsDisabled,
    cc.is_not_trusted AS IsNotTrusted
FROM sys.check_constraints AS cc
INNER JOIN sys.tables AS t ON t.object_id = cc.parent_object_id
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
ORDER BY s.name, t.name, cc.name;

-- 7) EF Core migration history tables (read-only)
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
BEGIN
    SELECT
        N'Core' AS DbContextName,
        N'dbo.__EFMigrationsHistory' AS HistoryTable,
        MigrationId,
        ProductVersion,
        N'FOUND' AS HistoryStatus
    FROM dbo.__EFMigrationsHistory
    ORDER BY MigrationId;
END
ELSE
BEGIN
    SELECT
        N'Core' AS DbContextName,
        N'dbo.__EFMigrationsHistory' AS HistoryTable,
        CAST(NULL AS nvarchar(150)) AS MigrationId,
        CAST(NULL AS nvarchar(32)) AS ProductVersion,
        N'NOT FOUND' AS HistoryStatus;
END;

IF OBJECT_ID(N'dbo.__TradeMigrationsHistory', N'U') IS NOT NULL
BEGIN
    SELECT
        N'Trade' AS DbContextName,
        N'dbo.__TradeMigrationsHistory' AS HistoryTable,
        MigrationId,
        ProductVersion,
        N'FOUND' AS HistoryStatus
    FROM dbo.__TradeMigrationsHistory
    ORDER BY MigrationId;
END
ELSE
BEGIN
    SELECT
        N'Trade' AS DbContextName,
        N'dbo.__TradeMigrationsHistory' AS HistoryTable,
        CAST(NULL AS nvarchar(150)) AS MigrationId,
        CAST(NULL AS nvarchar(32)) AS ProductVersion,
        N'NOT FOUND' AS HistoryStatus;
END;

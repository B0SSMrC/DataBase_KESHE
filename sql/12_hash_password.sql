-- 12_hash_password.sql  把 Admin 表中的明文口令迁移为 SHA2_256 哈希（幂等）
-- 用途：仅针对用旧版 04(明文)建过库的现有实例；新建库(跑更新后的 04)已直接存哈希，无需执行本脚本。
-- 幂等说明：哈希为 64 位十六进制；WHERE 只命中"非 64 位十六进制"的明文行，已是哈希的行会被跳过。
USE DormDB;
GO
UPDATE Admin
SET password = CONVERT(NVARCHAR(100), HASHBYTES('SHA2_256', password), 2)
WHERE LEN(password) <> 64 OR password LIKE '%[^0-9A-Fa-f]%';
GO

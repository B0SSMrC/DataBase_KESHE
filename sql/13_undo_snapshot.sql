-- 13_undo_snapshot.sql  操作撤销用整库快照表（幂等；应用启动也会自愈创建）
USE DormDB;
GO
IF OBJECT_ID('Snap_Meta','U') IS NULL CREATE TABLE Snap_Meta(snap_op_id INT PRIMARY KEY);
IF OBJECT_ID('Snap_Building','U') IS NULL CREATE TABLE Snap_Building(snap_op_id INT, building_id INT, building_no NVARCHAR(20), building_name NVARCHAR(50), gender_type NVARCHAR(10));
IF OBJECT_ID('Snap_ClassInfo','U') IS NULL CREATE TABLE Snap_ClassInfo(snap_op_id INT, class_id INT, class_name NVARCHAR(50), major NVARCHAR(50));
IF OBJECT_ID('Snap_Room','U') IS NULL CREATE TABLE Snap_Room(snap_op_id INT, room_id INT, building_id INT, room_no NVARCHAR(20), capacity INT, occupied_count INT, status NVARCHAR(10));
IF OBJECT_ID('Snap_Bed','U') IS NULL CREATE TABLE Snap_Bed(snap_op_id INT, bed_id INT, room_id INT, bed_no NVARCHAR(10));
IF OBJECT_ID('Snap_Student','U') IS NULL CREATE TABLE Snap_Student(snap_op_id INT, student_id INT, student_no NVARCHAR(20), name NVARCHAR(50), gender NVARCHAR(10), class_id INT, phone NVARCHAR(20));
IF OBJECT_ID('Snap_Allocation','U') IS NULL CREATE TABLE Snap_Allocation(snap_op_id INT, allocation_id INT, student_id INT, bed_id INT, check_in_date DATE);
IF OBJECT_ID('Snap_HousingLog','U') IS NULL CREATE TABLE Snap_HousingLog(snap_op_id INT, log_id INT, student_id INT, bed_id INT, op_type NVARCHAR(10), op_time DATETIME, operator NVARCHAR(50));
GO

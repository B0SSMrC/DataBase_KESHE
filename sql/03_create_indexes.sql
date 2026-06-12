-- 03_create_indexes.sql  在外键列上建非聚集索引，加速连接
USE DormDB;
GO
CREATE INDEX IX_Room_Building ON Room(building_id);
CREATE INDEX IX_Bed_Room      ON Bed(room_id);
CREATE INDEX IX_Student_Class ON Student(class_id);
CREATE INDEX IX_Log_Student   ON HousingLog(student_id);
GO

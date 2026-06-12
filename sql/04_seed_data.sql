-- 04_seed_data.sql  基础测试数据（暂不含住宿分配）
USE DormDB;
GO

INSERT INTO Building(building_no, building_name, gender_type) VALUES
('1号楼','紫荆1号楼','男'),
('2号楼','紫荆2号楼','女');

INSERT INTO ClassInfo(class_name, major) VALUES
('计科2101','计算机科学与技术'),
('软工2101','软件工程');

DECLARE @b1 INT = (SELECT building_id FROM Building WHERE building_no='1号楼');

INSERT INTO Room(building_id, room_no, capacity) VALUES
(@b1,'101',4),
(@b1,'102',4);

DECLARE @r101 INT = (SELECT room_id FROM Room WHERE building_id=@b1 AND room_no='101');
DECLARE @r102 INT = (SELECT room_id FROM Room WHERE building_id=@b1 AND room_no='102');

INSERT INTO Bed(room_id, bed_no) VALUES
(@r101,'1'),(@r101,'2'),(@r101,'3'),(@r101,'4'),
(@r102,'1'),(@r102,'2'),(@r102,'3'),(@r102,'4');

DECLARE @c1 INT = (SELECT class_id FROM ClassInfo WHERE class_name='计科2101');

INSERT INTO Student(student_no, name, gender, class_id, phone) VALUES
('2021001','张三','男',@c1,'13800000001'),
('2021002','李四','男',@c1,'13800000002'),
('2021003','王五','男',@c1,'13800000003');

INSERT INTO Admin(username, password, real_name) VALUES
('admin','123456','系统管理员');   -- 报告中说明：演示用明文，正式应存哈希
GO

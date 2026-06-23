-- 02_create_tables.sql  按外键依赖顺序建表
USE DormDB;
GO

-- ① 管理员
CREATE TABLE Admin (
    admin_id  INT IDENTITY(1,1) PRIMARY KEY,
    username  NVARCHAR(50)  NOT NULL UNIQUE,
    password  NVARCHAR(100) NOT NULL,
    real_name NVARCHAR(50)  NULL
);
GO

-- ② 宿舍楼
CREATE TABLE Building (
    building_id   INT IDENTITY(1,1) PRIMARY KEY,
    building_no   NVARCHAR(20) NOT NULL UNIQUE,
    building_name NVARCHAR(50) NULL,
    gender_type   NVARCHAR(10) NULL
);
GO

-- ③ 班级
CREATE TABLE ClassInfo (
    class_id   INT IDENTITY(1,1) PRIMARY KEY,
    class_name NVARCHAR(50) NOT NULL UNIQUE,
    major      NVARCHAR(50) NOT NULL
);
GO

-- ④ 寝室（occupied_count / status 由触发器维护）
CREATE TABLE Room (
    room_id        INT IDENTITY(1,1) PRIMARY KEY,
    building_id    INT NOT NULL,
    room_no        NVARCHAR(20) NOT NULL,
    capacity       INT NOT NULL,
    occupied_count INT NOT NULL DEFAULT 0,
    status         NVARCHAR(10) NOT NULL DEFAULT '空闲',
    CONSTRAINT FK_Room_Building FOREIGN KEY (building_id) REFERENCES Building(building_id),
    CONSTRAINT CK_Room_Capacity CHECK (capacity > 0),
    CONSTRAINT CK_Room_Occupied CHECK (occupied_count >= 0 AND occupied_count <= capacity),
    CONSTRAINT UQ_Room_BuildingRoomNo UNIQUE (building_id, room_no)
);
GO

-- ⑤ 学生
CREATE TABLE Student (
    student_id INT IDENTITY(1,1) PRIMARY KEY,
    student_no NVARCHAR(20) NOT NULL UNIQUE,
    name       NVARCHAR(50) NOT NULL,
    gender     NVARCHAR(10) NULL,
    class_id   INT NULL,
    phone      NVARCHAR(20) NULL,
    CONSTRAINT FK_Student_Class FOREIGN KEY (class_id) REFERENCES ClassInfo(class_id)
);
GO

-- ⑥ 床位
CREATE TABLE Bed (
    bed_id  INT IDENTITY(1,1) PRIMARY KEY,
    room_id INT NOT NULL,
    bed_no  NVARCHAR(10) NOT NULL,
    CONSTRAINT FK_Bed_Room FOREIGN KEY (room_id) REFERENCES Room(room_id),
    CONSTRAINT UQ_Bed_RoomBedNo UNIQUE (room_id, bed_no)
);
GO

-- ⑦ 住宿分配（两条 UNIQUE 落实"一床一人、一人一寝"）
CREATE TABLE Allocation (
    allocation_id INT IDENTITY(1,1) PRIMARY KEY,
    student_id    INT NOT NULL UNIQUE,
    bed_id        INT NOT NULL UNIQUE,
    check_in_date DATE NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Alloc_Student FOREIGN KEY (student_id) REFERENCES Student(student_id),
    CONSTRAINT FK_Alloc_Bed     FOREIGN KEY (bed_id)     REFERENCES Bed(bed_id)
);
GO

-- ⑧ 住宿变更记录
CREATE TABLE HousingLog (
    log_id     INT IDENTITY(1,1) PRIMARY KEY,
    student_id INT NOT NULL,
    bed_id     INT NULL,
    op_type    NVARCHAR(10) NOT NULL,
    op_time    DATETIME NOT NULL DEFAULT GETDATE(),
    operator   NVARCHAR(50) NULL,
    CONSTRAINT FK_Log_Student FOREIGN KEY (student_id) REFERENCES Student(student_id),
    CONSTRAINT FK_Log_Bed     FOREIGN KEY (bed_id)     REFERENCES Bed(bed_id)
);
GO

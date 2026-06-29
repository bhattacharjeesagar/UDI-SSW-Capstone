-- ==================================================
-- Database Setup Script for UDI-SSW Portal
-- Target Database: SQL Server (LocalDB / Express)
-- Run this script to initialize tables on your SQL Server instance
-- ==================================================

USE tempdb;
GO

-- Create UDI_SSW_Prototype database if it does not exist
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'UDI_SSW_Prototype')
BEGIN
    CREATE DATABASE UDI_SSW_Prototype;
END
GO

USE UDI_SSW_Prototype;
GO

-- 1. Create RoleMaster Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RoleMaster')
BEGIN
    CREATE TABLE RoleMaster (
        RoleId INT PRIMARY KEY,
        RoleName VARCHAR(50) NOT NULL
    );
    PRINT 'Table [RoleMaster] created.';
END
GO

-- 2. Create UserMaster Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserMaster')
BEGIN
    CREATE TABLE UserMaster (
        UserId INT IDENTITY(1,1) PRIMARY KEY,
        Username VARCHAR(100) NOT NULL UNIQUE,
        PasswordHash VARCHAR(256) NOT NULL, -- SHA256 hash
        RoleId INT NOT NULL,
        FullName NVARCHAR(150) NOT NULL,
        AssociatedDid VARCHAR(100) NULL, -- Maps to Citizen DID if user is a Citizen
        IsActive BIT NOT NULL DEFAULT 1,
        FOREIGN KEY (RoleId) REFERENCES RoleMaster(RoleId)
    );
    PRINT 'Table [UserMaster] created.';
END
GO

-- 3. Create Citizens Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Citizens')
BEGIN
    CREATE TABLE Citizens (
        Did VARCHAR(100) PRIMARY KEY,
        Name NVARCHAR(150) NOT NULL,
        Birthdate DATETIME NOT NULL,
        LicenseNumber VARCHAR(50) NOT NULL,
        PublicKeyPem VARCHAR(MAX) NOT NULL,
        PrivateKeyPem VARCHAR(MAX) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        LastActiveTime DATETIME NULL
    );
    PRINT 'Table [Citizens] created.';
END
GO

-- 4. Create Credentials Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Credentials')
BEGIN
    CREATE TABLE Credentials (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        SubjectDid VARCHAR(100) NOT NULL,
        IssuerName VARCHAR(100) NOT NULL,
        ClaimName VARCHAR(100) NOT NULL,
        ClaimValue VARCHAR(100) NOT NULL,
        SignedPayload VARCHAR(MAX) NOT NULL,
        DocumentPath VARCHAR(255) NULL, -- For uploaded credential PDFs
        IsRevoked BIT NOT NULL DEFAULT 0,
        IssuedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
        FOREIGN KEY (SubjectDid) REFERENCES Citizens(Did)
    );
    PRINT 'Table [Credentials] created.';
END
GO

-- 5. Create Keys (Issuer Registries) Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Keys')
BEGIN
    CREATE TABLE Keys (
        IssuerName VARCHAR(100) PRIMARY KEY,
        PublicKeyPem VARCHAR(MAX) NOT NULL,
        PrivateKeyPem VARCHAR(MAX) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1
    );
    PRINT 'Table [Keys] created.';
END
GO

-- ──────────────────────────────────────────────────
-- SEED DATA
-- ──────────────────────────────────────────────────

-- Seed Roles
IF NOT EXISTS (SELECT * FROM RoleMaster WHERE RoleId = 1)
    INSERT INTO RoleMaster (RoleId, RoleName) VALUES (1, 'Issuer');
IF NOT EXISTS (SELECT * FROM RoleMaster WHERE RoleId = 2)
    INSERT INTO RoleMaster (RoleId, RoleName) VALUES (2, 'Citizen');
IF NOT EXISTS (SELECT * FROM RoleMaster WHERE RoleId = 3)
    INSERT INTO RoleMaster (RoleId, RoleName) VALUES (3, 'Verifier');
GO

-- Helper to insert default users (Password hash is SHA256 for 'Test@123': '8776f108e247ab1e2b323042c049c266407c81fbad41bde1e8dfc1bb66fd267e')
-- Issuer User
IF NOT EXISTS (SELECT * FROM UserMaster WHERE Username = 'gov_issuer')
    INSERT INTO UserMaster (Username, PasswordHash, RoleId, FullName, AssociatedDid, IsActive)
    VALUES ('gov_issuer', '8776f108e247ab1e2b323042c049c266407c81fbad41bde1e8dfc1bb66fd267e', 1, 'National Identity Officer', NULL, 1);

-- Verifier User
IF NOT EXISTS (SELECT * FROM UserMaster WHERE Username = 'verifier_user')
    INSERT INTO UserMaster (Username, PasswordHash, RoleId, FullName, AssociatedDid, IsActive)
    VALUES ('verifier_user', '8776f108e247ab1e2b323042c049c266407c81fbad41bde1e8dfc1bb66fd267e', 3, 'Eagle Car Rental Verifier', NULL, 1);
GO

CREATE TABLE dbo.Form
(
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](256) NOT NULL,
	[Base64Image] [nvarchar](MAX) NOT NULL,
	CONSTRAINT [PK_Form] PRIMARY KEY CLUSTERED (Id)
)
GO

CREATE TABLE dbo.FormDetail
(
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](256) NOT NULL,
	[TextData] [nvarchar](1000) NULL,
	[FormId] INT NOT NULL,
	CONSTRAINT [PK_FormDetail] PRIMARY KEY CLUSTERED (Id)
)
GO

ALTER TABLE dbo.FormDetail WITH CHECK ADD CONSTRAINT [FK_FormDetail_Form] FOREIGN KEY(FormId)
REFERENCES dbo.Form (Id)
GO

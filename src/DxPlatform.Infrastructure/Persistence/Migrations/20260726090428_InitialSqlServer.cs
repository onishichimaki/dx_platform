using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DxPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AgentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OwnerDepartment = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ManagerName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    GitHubRepositoryUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    PromptVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PromptSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PromptLocationReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PromptLastReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PublicationScope = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HasWriteOperations = table.Column<bool>(type: "bit", nullable: false),
                    SendsDataExternally = table.Column<bool>(type: "bit", nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EvaluationStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LastEvaluatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TagsText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AssetType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TechnologyStack = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OwnerDepartment = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ManagerName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DevelopmentLead = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    GitHubRepositoryUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    RuntimeEnvironment = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    HealthCheckUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ConfidentialityClassification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastDeployedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TagsText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ActorDisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SystemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OwnerDepartment = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    DataOwner = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    UpdateFrequency = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConfidentialityClassification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RetentionPeriod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConnectionReferenceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TagsText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToolAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ToolType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReferenceUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Deployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GitCommitSha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ExecutedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Result = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RollbackFromDeploymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LogReference = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deployments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deployments_ApplicationAssets_ApplicationAssetId",
                        column: x => x.ApplicationAssetId,
                        principalTable: "ApplicationAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HealthCheckResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseTimeMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ErrorSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthCheckResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HealthCheckResults_ApplicationAssets_ApplicationAssetId",
                        column: x => x.ApplicationAssetId,
                        principalTable: "ApplicationAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentDataAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentDataAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentDataAssets_AgentAssets_AgentAssetId",
                        column: x => x.AgentAssetId,
                        principalTable: "AgentAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentDataAssets_DataAssets_DataAssetId",
                        column: x => x.DataAssetId,
                        principalTable: "DataAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationDataAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationDataAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationDataAssets_ApplicationAssets_ApplicationAssetId",
                        column: x => x.ApplicationAssetId,
                        principalTable: "ApplicationAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApplicationDataAssets_DataAssets_DataAssetId",
                        column: x => x.DataAssetId,
                        principalTable: "DataAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DataAssetRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDataAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetDataAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataAssetRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataAssetRelations_DataAssets_SourceDataAssetId",
                        column: x => x.SourceDataAssetId,
                        principalTable: "DataAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DataAssetRelations_DataAssets_TargetDataAssetId",
                        column: x => x.TargetDataAssetId,
                        principalTable: "DataAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Level = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationPermissions", x => x.Id);
                    table.CheckConstraint("CK_ApplicationPermission_Principal", "([UserId] IS NOT NULL AND [GroupId] IS NULL) OR ([UserId] IS NULL AND [GroupId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ApplicationPermissions_ApplicationAssets_ApplicationAssetId",
                        column: x => x.ApplicationAssetId,
                        principalTable: "ApplicationAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApplicationPermissions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationPermissions_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupMembers_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentToolAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentToolAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentToolAssets_AgentAssets_AgentAssetId",
                        column: x => x.AgentAssetId,
                        principalTable: "AgentAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentToolAssets_ToolAssets_ToolAssetId",
                        column: x => x.ToolAssetId,
                        principalTable: "ToolAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentAssets_AgentType_Status_IsActive",
                table: "AgentAssets",
                columns: new[] { "AgentType", "Status", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentAssets_IsActive",
                table: "AgentAssets",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AgentDataAssets_AgentAssetId_DataAssetId",
                table: "AgentDataAssets",
                columns: new[] { "AgentAssetId", "DataAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentDataAssets_DataAssetId",
                table: "AgentDataAssets",
                column: "DataAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentDataAssets_IsActive",
                table: "AgentDataAssets",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AgentToolAssets_AgentAssetId_ToolAssetId",
                table: "AgentToolAssets",
                columns: new[] { "AgentAssetId", "ToolAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentToolAssets_IsActive",
                table: "AgentToolAssets",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AgentToolAssets_ToolAssetId",
                table: "AgentToolAssets",
                column: "ToolAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationAssets_Code",
                table: "ApplicationAssets",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationAssets_IsActive",
                table: "ApplicationAssets",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationAssets_Status_AssetType_IsActive",
                table: "ApplicationAssets",
                columns: new[] { "Status", "AssetType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDataAssets_ApplicationAssetId_DataAssetId",
                table: "ApplicationDataAssets",
                columns: new[] { "ApplicationAssetId", "DataAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDataAssets_DataAssetId",
                table: "ApplicationDataAssets",
                column: "DataAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDataAssets_IsActive",
                table: "ApplicationDataAssets",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationPermissions_ApplicationAssetId_GroupId",
                table: "ApplicationPermissions",
                columns: new[] { "ApplicationAssetId", "GroupId" },
                unique: true,
                filter: "[GroupId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationPermissions_ApplicationAssetId_UserId",
                table: "ApplicationPermissions",
                columns: new[] { "ApplicationAssetId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationPermissions_GroupId",
                table: "ApplicationPermissions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationPermissions_IsActive",
                table: "ApplicationPermissions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationPermissions_UserId",
                table: "ApplicationPermissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_IsActive",
                table: "AspNetUsers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTags_AssetKind_AssetId_TagId",
                table: "AssetTags",
                columns: new[] { "AssetKind", "AssetId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetTags_IsActive",
                table: "AssetTags",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTags_TagId",
                table: "AssetTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OccurredAt",
                table: "AuditLogs",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TargetType_TargetId",
                table: "AuditLogs",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_DataAssetRelations_IsActive",
                table: "DataAssetRelations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DataAssetRelations_SourceDataAssetId_TargetDataAssetId",
                table: "DataAssetRelations",
                columns: new[] { "SourceDataAssetId", "TargetDataAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataAssetRelations_TargetDataAssetId",
                table: "DataAssetRelations",
                column: "TargetDataAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_DataAssets_DataType_Status_IsActive",
                table: "DataAssets",
                columns: new[] { "DataType", "Status", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DataAssets_IsActive",
                table: "DataAssets",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Deployments_ApplicationAssetId_StartedAt",
                table: "Deployments",
                columns: new[] { "ApplicationAssetId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Deployments_IsActive",
                table: "Deployments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMembers_GroupId_UserId",
                table: "GroupMembers",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupMembers_IsActive",
                table: "GroupMembers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMembers_UserId",
                table: "GroupMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_Code",
                table: "Groups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Groups_IsActive",
                table: "Groups",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_HealthCheckResults_ApplicationAssetId_CheckedAt",
                table: "HealthCheckResults",
                columns: new[] { "ApplicationAssetId", "CheckedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HealthCheckResults_IsActive",
                table: "HealthCheckResults",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_IsActive",
                table: "Tags",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToolAssets_IsActive",
                table: "ToolAssets",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentDataAssets");

            migrationBuilder.DropTable(
                name: "AgentToolAssets");

            migrationBuilder.DropTable(
                name: "ApplicationDataAssets");

            migrationBuilder.DropTable(
                name: "ApplicationPermissions");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AssetTags");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "DataAssetRelations");

            migrationBuilder.DropTable(
                name: "Deployments");

            migrationBuilder.DropTable(
                name: "GroupMembers");

            migrationBuilder.DropTable(
                name: "HealthCheckResults");

            migrationBuilder.DropTable(
                name: "AgentAssets");

            migrationBuilder.DropTable(
                name: "ToolAssets");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "DataAssets");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Groups");

            migrationBuilder.DropTable(
                name: "ApplicationAssets");
        }
    }
}

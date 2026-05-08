using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRp.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiProviders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ManagementApiKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TeamId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastMetricsRefreshUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastMetricsError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    JsonValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Chats",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Updated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Starred = table.Column<bool>(type: "bit", nullable: false),
                    Messages = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ElevenLabsVoiceCatalog",
                columns: table => new
                {
                    VoiceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PublicOwnerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DateUnix = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreviewUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Featured = table.Column<bool>(type: "bit", nullable: false),
                    Accent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Age = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UseCase = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descriptive = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    VerifiedLanguagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsBookmarked = table.Column<bool>(type: "bit", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElevenLabsVoiceCatalog", x => x.VoiceId);
                });

            migrationBuilder.CreateTable(
                name: "ElevenLabsVoiceCatalogStates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    LastRefreshUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRefreshError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    TotalCount = table.Column<int>(type: "int", nullable: false),
                    CachedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElevenLabsVoiceCatalogStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImageAssets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    BlobName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StoredContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OriginalByteLength = table.Column<long>(type: "bigint", nullable: false),
                    StoredByteLength = table.Column<long>(type: "bigint", nullable: false),
                    OptimizationAttempted = table.Column<bool>(type: "bit", nullable: false),
                    OptimizationSucceeded = table.Column<bool>(type: "bit", nullable: false),
                    OptimizationProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OptimizationError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OptimizedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    AvatarFocusXPercent = table.Column<int>(type: "int", nullable: true),
                    AvatarFocusYPercent = table.Column<int>(type: "int", nullable: true),
                    AvatarZoomPercent = table.Column<int>(type: "int", nullable: true),
                    UserPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FinalPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GenerationMetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    ProviderId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProviderModelId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpeechAssets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TurnId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Bytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "Pending"),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProviderId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProviderType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderModelId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InputsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    VoiceIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpeechAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiProviderMetrics",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ProviderId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RefreshedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviderMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiProviderMetrics_AiProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "AiProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiProviderModels",
                columns: table => new
                {
                    ProviderId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Repository = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedUnix = table.Column<long>(type: "bigint", nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    RolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastVoiceRefreshUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastVoiceRefreshError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    VoicesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviderModels", x => new { x.ProviderId, x.Id });
                    table.ForeignKey(
                        name: "FK_AiProviderModels_AiProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "AiProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatDocuments",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CharactersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocationsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimelineJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MessagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StoryAssistantJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChatDirectionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NarratorProfileJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PromptLibraryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CharacterTraitLibraryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelTuningJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatDocuments", x => x.ChatId);
                    table.ForeignKey(
                        name: "FK_ChatDocuments_Chats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "Chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderMetrics_ProviderId_Kind",
                table: "AiProviderMetrics",
                columns: new[] { "ProviderId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderModels_SortOrder",
                table: "AiProviderModels",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_AiProviders_SortOrder",
                table: "AiProviders",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_SortOrder",
                table: "Chats",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_UpdatedUtc",
                table: "Chats",
                column: "UpdatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ElevenLabsVoiceCatalog_IsAvailable",
                table: "ElevenLabsVoiceCatalog",
                column: "IsAvailable");

            migrationBuilder.CreateIndex(
                name: "IX_ElevenLabsVoiceCatalog_IsBookmarked",
                table: "ElevenLabsVoiceCatalog",
                column: "IsBookmarked");

            migrationBuilder.CreateIndex(
                name: "IX_ElevenLabsVoiceCatalog_Name",
                table: "ElevenLabsVoiceCatalog",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ImageAssets_ChatId_CreatedUtc",
                table: "ImageAssets",
                columns: new[] { "ChatId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SpeechAssets_ChatId_CreatedUtc",
                table: "SpeechAssets",
                columns: new[] { "ChatId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SpeechAssets_ChatId_TurnId",
                table: "SpeechAssets",
                columns: new[] { "ChatId", "TurnId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiProviderMetrics");

            migrationBuilder.DropTable(
                name: "AiProviderModels");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "ChatDocuments");

            migrationBuilder.DropTable(
                name: "ElevenLabsVoiceCatalog");

            migrationBuilder.DropTable(
                name: "ElevenLabsVoiceCatalogStates");

            migrationBuilder.DropTable(
                name: "ImageAssets");

            migrationBuilder.DropTable(
                name: "SpeechAssets");

            migrationBuilder.DropTable(
                name: "AiProviders");

            migrationBuilder.DropTable(
                name: "Chats");
        }
    }
}

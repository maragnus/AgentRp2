using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRp.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                name: "CharacterTraitLibraryStates",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterTraitLibraryStates", x => x.ChatId);
                });

            migrationBuilder.CreateTable(
                name: "ChatCharacterRelationships",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CharacterAId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CharacterBId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatCharacterRelationships", x => new { x.ChatId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "ChatCharacters",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ImageId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    InScene = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ProfileJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatCharacters", x => new { x.ChatId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "ChatCurrentSceneCharacters",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CharacterId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatCurrentSceneCharacters", x => new { x.ChatId, x.CharacterId });
                });

            migrationBuilder.CreateTable(
                name: "ChatCurrentSceneItems",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ItemId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatCurrentSceneItems", x => new { x.ChatId, x.ItemId });
                });

            migrationBuilder.CreateTable(
                name: "ChatDirectionStates",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatDirectionStates", x => x.ChatId);
                });

            migrationBuilder.CreateTable(
                name: "ChatItems",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ImageId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    InScene = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatItems", x => new { x.ChatId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "ChatLocations",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ImageId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatLocations", x => new { x.ChatId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "Chats",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Updated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastMessageUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastGeneratedTurnNumber = table.Column<int>(type: "int", nullable: false),
                    Starred = table.Column<bool>(type: "bit", nullable: false),
                    Messages = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActiveLeafTurnId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ActiveTurnCount = table.Column<int>(type: "int", nullable: false),
                    ActiveLocationId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ActiveLocationName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SnapshotCount = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatTimelineEntries",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SnapshotId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DateText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatTimelineEntries", x => new { x.ChatId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "ChatTranscriptStates",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    RootSceneJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkingSceneJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchSelectionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatTranscriptStates", x => x.ChatId);
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
                    Entity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Hue = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                name: "ModelTuningStates",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelTuningStates", x => x.ChatId);
                });

            migrationBuilder.CreateTable(
                name: "NarratorProfileStates",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NarratorProfileStates", x => x.ChatId);
                });

            migrationBuilder.CreateTable(
                name: "PromptLibraryStates",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptLibraryStates", x => x.ChatId);
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
                name: "TranscriptSnapshots",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TurnId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StartTurnId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EndTurnId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ParentBeforeStartTurnId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TurnNumberStart = table.Column<int>(type: "int", nullable: false),
                    TurnNumberEnd = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SceneLocationId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SceneLocationName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SceneJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpeechJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrivateIntentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CharacterAppearancesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TraceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConsumedBySnapshotId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ConsumedBySnapshotOrdinal = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranscriptSnapshots", x => new { x.ChatId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "TranscriptTurns",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ParentTurnId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TurnNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AuthorCharacterId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActorCharacterId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Guidance = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SceneLocationId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SceneLocationName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SceneJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlanJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AppearanceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrivateIntentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpeechJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TraceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConsumedBySnapshotId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ConsumedBySnapshotOrdinal = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranscriptTurns", x => new { x.ChatId, x.Id });
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
                name: "IX_ChatCharacterRelationships_ChatId_CharacterAId",
                table: "ChatCharacterRelationships",
                columns: new[] { "ChatId", "CharacterAId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatCharacterRelationships_ChatId_CharacterAId_CharacterBId",
                table: "ChatCharacterRelationships",
                columns: new[] { "ChatId", "CharacterAId", "CharacterBId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatCharacterRelationships_ChatId_CharacterBId",
                table: "ChatCharacterRelationships",
                columns: new[] { "ChatId", "CharacterBId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatCharacters_ChatId_ImageId",
                table: "ChatCharacters",
                columns: new[] { "ChatId", "ImageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatCharacters_ChatId_SortOrder",
                table: "ChatCharacters",
                columns: new[] { "ChatId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatCurrentSceneCharacters_ChatId_SortOrder",
                table: "ChatCurrentSceneCharacters",
                columns: new[] { "ChatId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatCurrentSceneItems_ChatId_SortOrder",
                table: "ChatCurrentSceneItems",
                columns: new[] { "ChatId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatItems_ChatId_ImageId",
                table: "ChatItems",
                columns: new[] { "ChatId", "ImageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatItems_ChatId_SortOrder",
                table: "ChatItems",
                columns: new[] { "ChatId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatLocations_ChatId_ImageId",
                table: "ChatLocations",
                columns: new[] { "ChatId", "ImageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatLocations_ChatId_SortOrder",
                table: "ChatLocations",
                columns: new[] { "ChatId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Chats_LastMessageUtc",
                table: "Chats",
                column: "LastMessageUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_SortOrder",
                table: "Chats",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_UpdatedUtc",
                table: "Chats",
                column: "UpdatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ChatTimelineEntries_ChatId_SnapshotId",
                table: "ChatTimelineEntries",
                columns: new[] { "ChatId", "SnapshotId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatTimelineEntries_ChatId_SortOrder",
                table: "ChatTimelineEntries",
                columns: new[] { "ChatId", "SortOrder" });

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
                name: "IX_ImageAssets_ChatId_EntityType_Entity",
                table: "ImageAssets",
                columns: new[] { "ChatId", "EntityType", "Entity" });

            migrationBuilder.CreateIndex(
                name: "IX_SpeechAssets_ChatId_CreatedUtc",
                table: "SpeechAssets",
                columns: new[] { "ChatId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SpeechAssets_ChatId_TurnId",
                table: "SpeechAssets",
                columns: new[] { "ChatId", "TurnId" });

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptSnapshots_ChatId_ConsumedBySnapshotId_ConsumedBySnapshotOrdinal",
                table: "TranscriptSnapshots",
                columns: new[] { "ChatId", "ConsumedBySnapshotId", "ConsumedBySnapshotOrdinal" });

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptSnapshots_ChatId_EndTurnId_IsActive",
                table: "TranscriptSnapshots",
                columns: new[] { "ChatId", "EndTurnId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptTurns_ChatId_ConsumedBySnapshotId_ConsumedBySnapshotOrdinal",
                table: "TranscriptTurns",
                columns: new[] { "ChatId", "ConsumedBySnapshotId", "ConsumedBySnapshotOrdinal" });

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptTurns_ChatId_ParentTurnId",
                table: "TranscriptTurns",
                columns: new[] { "ChatId", "ParentTurnId" });
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
                name: "CharacterTraitLibraryStates");

            migrationBuilder.DropTable(
                name: "ChatCharacterRelationships");

            migrationBuilder.DropTable(
                name: "ChatCharacters");

            migrationBuilder.DropTable(
                name: "ChatCurrentSceneCharacters");

            migrationBuilder.DropTable(
                name: "ChatCurrentSceneItems");

            migrationBuilder.DropTable(
                name: "ChatDirectionStates");

            migrationBuilder.DropTable(
                name: "ChatItems");

            migrationBuilder.DropTable(
                name: "ChatLocations");

            migrationBuilder.DropTable(
                name: "Chats");

            migrationBuilder.DropTable(
                name: "ChatTimelineEntries");

            migrationBuilder.DropTable(
                name: "ChatTranscriptStates");

            migrationBuilder.DropTable(
                name: "ElevenLabsVoiceCatalog");

            migrationBuilder.DropTable(
                name: "ElevenLabsVoiceCatalogStates");

            migrationBuilder.DropTable(
                name: "ImageAssets");

            migrationBuilder.DropTable(
                name: "ModelTuningStates");

            migrationBuilder.DropTable(
                name: "NarratorProfileStates");

            migrationBuilder.DropTable(
                name: "PromptLibraryStates");

            migrationBuilder.DropTable(
                name: "SpeechAssets");

            migrationBuilder.DropTable(
                name: "TranscriptSnapshots");

            migrationBuilder.DropTable(
                name: "TranscriptTurns");

            migrationBuilder.DropTable(
                name: "AiProviders");
        }
    }
}

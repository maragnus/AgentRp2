using System;

namespace AgentRp.Session;

public sealed record RoleplayStoreNotification(Guid OriginSessionId, string? ChatId, RoleplayStoreArea Area, long Version);

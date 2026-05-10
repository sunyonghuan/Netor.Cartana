# 03 - 内部事件 WebSocket 协议

> 状态：第一版已落地

## 目标

定义宿主向插件分发对话事实流的内部 WebSocket 协议。

## 区分原则

- 不复用旧聊天 `WsMessage`
- 不与旧 `send` / `stop` / `token` / `done` 混用
- 事件对象必须带标准事件元数据
- 协议必须支持版本化

## 当前地址

- `ws://localhost:{ChatWsPort}/ws/`：聊天协议
- `ws://localhost:{ChatWsPort}/internal/conversation-feed/`：内部事件协议

第一版实际采用同端口不同路径，不新增独立 feed 端口。

## 握手消息

```json
{
	"type": "connected",
	"clientId": "feed-client-id",
	"topics": ["conversation"],
	"protocol": "conversation-feed",
	"version": "1.0.0"
}
```

## 插件订阅请求

```json
{
	"type": "subscribe",
	"topics": ["conversation"],
	"protocol": "conversation-feed",
	"version": "1.0.0"
}
```

当前实现只支持 `conversation` topic。

## 订阅确认

```json
{
	"type": "subscribed",
	"clientId": "feed-client-id",
	"topics": ["conversation"],
	"protocol": "conversation-feed",
	"version": "1.0.0"
}
```

## 错误消息

```json
{
	"type": "error",
	"clientId": "feed-client-id",
	"message": "protocol 不匹配",
	"protocol": "conversation-feed",
	"version": "1.0.0"
}
```

## 事件消息

```json
{
	"type": "event",
	"topic": "conversation",
	"eventType": "conversation.turn.started",
	"payload": {},
	"protocol": "conversation-feed",
	"version": "1.0.0"
}
```

## 当前事件类型

- `conversation.turn.started`
- `conversation.user.message`
- `conversation.assistant.delta`
- `conversation.turn.completed`

## 长期记忆上下文供应控制消息

第一版长期记忆默认注入复用 `/internal/conversation-feed/` 长连接承载轻量控制面请求/响应。该扩展不改变原有 `event` 消息语义：

- 宿主在构建主智能体上下文前向已连接的 Memory 插件 feed 客户端发送 `memory.supply.request`。
- Memory 插件复用 `IMemorySupplyService` 生成结构化供应包。
- 插件返回 `memory.supply.package`；参数缺失、异常或不可处理时返回 `memory.supply.error`。
- 宿主按 `requestId` 等待响应，默认短超时约 250ms，超时或错误时静默降级为空上下文。

请求示例：

```json
{
	"type": "request",
	"protocol": "memory-context-supply",
	"version": "1.0.0",
	"op": "memory.supply.request",
	"requestId": "uuid",
	"agentId": "agent-1",
	"agentName": "默认智能体",
	"workspaceId": "E:\\Netor.me\\Cortana",
	"workspaceDirectory": "E:\\Netor.me\\Cortana",
	"sessionId": "session-1",
	"turnId": "turn-1",
	"messageId": "message-1",
	"scenario": "chat",
	"currentTask": "当前用户问题",
	"recentMessages": [
		{ "messageId": "message-1", "role": "user", "content": "当前用户问题", "createdAt": "2026-05-10T00:00:00Z" }
	],
	"triggerSource": "before-prompt",
	"maxMemoryCount": 8,
	"maxTokenBudget": 1200,
	"timeoutMs": 250,
	"traceId": "trace"
}
```

响应示例：

```json
{
	"type": "response",
	"protocol": "memory-context-supply",
	"version": "1.0.0",
	"op": "memory.supply.package",
	"requestId": "uuid",
	"enabled": true,
	"summary": "命中 2 条长期记忆",
	"confidence": 0.82,
	"groups": [],
	"items": [],
	"budget": { "maxMemoryCount": 8, "usedMemoryCount": 0, "maxTokenBudget": 1200, "estimatedTokens": 0 },
	"appliedPolicy": { "supplyEnabled": true, "maxMemoryCount": 8, "recallMinimumConfidence": 0.2, "ranking": "default", "grouping": "kind" },
	"traceId": "trace",
	"producerVersion": "1.0.0"
}
```

错误示例：

```json
{
	"type": "error",
	"protocol": "memory-context-supply",
	"version": "1.0.0",
	"op": "memory.supply.error",
	"requestId": "uuid",
	"traceId": "trace",
	"code": "INVALID_ARGUMENT",
	"message": "agentId 不能为空。",
	"retryable": false
}
```

约束：`agentId` 必须由宿主显式携带，插件不得回退到 `default`。供应包保持结构化，最终 prompt 拼接由宿主负责。

## 当前实现落点

- `WebSocketServerService`：维护 internal feed 路径、connected/subscribe/subscribed/error 控制消息和订阅客户端集合
- `WebSocketConversationFeedRelayService`：订阅 EventHub Conversation 事件并广播 `event` 消息
- `WebSocketJsonContext`：提供 feed 消息与 Conversation 事件参数的 AOT JSON 源生成支持
- `LongMemoryContextProvider`：在宿主构建主智能体上下文前请求长期记忆供应包并注入 `AIContext.Instructions`
- `MemoryIngestService` / `MemorySupplyControlHandler`：在 Memory 插件内接收 `memory.supply.request` 并返回供应包或错误


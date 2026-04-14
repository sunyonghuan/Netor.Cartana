# WebSocket Client Checklist

1. 使用 ws://host:port/ws/。
2. 连接后先等待 connected。
3. 所有 JSON 使用 JsonSerializerContext。
4. send 完成后持续接收 token。
5. 收到 done 或 error 后结束当前会话。
6. 关闭前发送 stop 仅用于主动中断。
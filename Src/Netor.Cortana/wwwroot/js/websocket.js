/**
 * websocket.js - WebSocket 连接与通信
 */

/**
 * 连接 WebSocket 服务器
 */
function connectWebSocket() {
    try {
        const port = cortana.GetWsPort();
        if (!port || port <= 0) {
            console.warn('WebSocket 端口未就绪，1秒后重试');
            setTimeout(connectWebSocket, 1000);
            return;
        }
        let url = `ws://localhost:${port}/ws/`;
        const ws = new WebSocket(url);
        console.log(url);
        ws.onopen = () => {
            console.log('WebSocket 已连接，端口:', port);
            state.ws = ws;
        };

        ws.onmessage = (event) => {
            try {
                const msg = JSON.parse(event.data);
                handleWsMessage(msg);
            } catch (e) {
                console.error('解析 WS 消息失败:', e);
            }
        };

        ws.onclose = () => {
            console.warn('WebSocket 连接断开，2秒后重连');
            state.ws = null;
            setTimeout(connectWebSocket, 2000);
        };

        ws.onerror = (err) => {
            console.error('WebSocket 错误:', err);
        };
    } catch (e) {
        console.error('建立 WebSocket 连接失败:', e);
        setTimeout(connectWebSocket, 2000);
    }
}

/**
 * 通过 WebSocket 发送消息
 */
function wsSend(type, data, attachments) {
    if (state.ws && state.ws.readyState === WebSocket.OPEN) {
        const msg = { type, data };
        if (attachments && attachments.length > 0) {
            msg.attachments = attachments;
        }
        state.ws.send(JSON.stringify(msg));
    } else {
        console.warn('WebSocket 未连接，消息未发送:', type);
    }
}

/**
 * 处理服务端推送的 WS 消息
 */
function handleWsMessage(msg) {
    switch (msg.type) {
        case 'token':
            appendStreamContent(msg.data);
            break;
        case 'done':
            endStreamMessage();
            setLoading(false);
            if (msg.sessionId) {
                state.currentSessionId = msg.sessionId;
            }
            // 刷新会话列表
            loadSessions();
            break;
        case 'error':
            endStreamMessage();
            setLoading(false);
            appendMessage('assistant', `⚠️ ${msg.data}`);
            break;

        // ── 语音对话同步（MainWindow 可见时由 VoiceChatService 推送） ──
        case 'voice_user':
            // 显示用户的语音输入
            appendMessage('user', msg.data);
            // 创建流式 AI 回复占位
            beginStreamMessage();
            state.voiceStreaming = true;
            forceScrollToBottom();
            break;
        case 'voice_token':
            appendStreamContent(msg.data);
            break;
        case 'voice_done':
            endStreamMessage();
            state.voiceStreaming = false;
            if (msg.data) {
                state.currentSessionId = msg.data;
            }
            // 刷新会话列表
            loadSessions();
            break;

        default:
            console.warn('未知 WS 消息类型:', msg.type);
    }
}

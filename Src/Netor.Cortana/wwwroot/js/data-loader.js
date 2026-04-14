/**
 * data-loader.js - 数据加载（会话、提供商、模型等）
 */

/**
 * 加载会话历史，并自动加载最新会话的消息
 */
function loadSessions() {
    try {
        const raw = cortana.GetSessions();
        const sessions = JSON.parse(raw);
        const items = sessions.map(s => ({
            id: String(s.Id),
            title: s.Title || '未命名会话',
            time: formatTimestamp(s.LastActiveTimestamp)
        }));
        setHistory(items);

        // 自动加载最新一条会话的消息
        if (items.length > 0) {
            const latest = items[0];
            state.currentSessionId = latest.id;
            dom.historyLabel.textContent = latest.title;
            loadSessionMessages(latest.id);
            // loadSessionMessages 内部会调用 forceScrollToBottom
        }
    } catch (e) {
        console.error('加载会话历史失败:', e);
    }
}

/**
 * 加载智能体列表
 */
function loadAgents() {
    try {
        const raw = cortana.GetAgents();
        const agents = JSON.parse(raw);
        setAgents(agents.map(a => ({
            name: a.Name,
            desc: a.Description || '',
            id: String(a.Id),
            isDefault: a.IsDefault
        })));
        // 自动通知后端选中默认智能体
        const defaultAgent = agents.find(a => a.IsDefault) || agents[0];
        if (defaultAgent && typeof cortana !== 'undefined' && cortana.OnAgentChanged) {
            cortana.OnAgentChanged(String(defaultAgent.Id));
        }
    } catch (e) {
        console.error('加载智能体列表失败:', e);
    }
}

/**
 * 加载厂商列表，并自动加载默认/首个厂商的模型
 */
function loadProviders() {
    try {
        const raw = cortana.GetProviders();
        const providers = JSON.parse(raw);
        setProviders(providers.map(p => ({
            name: p.Name,
            desc: p.Description || '',
            id: String(p.Id),
            isDefault: p.IsDefault
        })));
        // 自动选中默认厂商并加载其模型
        const defaultProvider = providers.find(p => p.IsDefault) || providers[0];
        if (defaultProvider) {
            currentProviderId = String(defaultProvider.Id);
            // 通知后端选中默认提供商
            if (typeof cortana !== 'undefined' && cortana.OnProviderChanged) {
                cortana.OnProviderChanged(currentProviderId);
            }
            loadModels(currentProviderId);
        }
    } catch (e) {
        console.error('加载厂商列表失败:', e);
    }
}

/**
 * 加载指定厂商下的模型列表
 */
function loadModels(providerId) {
    try {
        const raw = cortana.GetModels(providerId);
        const models = JSON.parse(raw);
        setModels(models.map(m => ({
            name: m.DisplayName || m.Name,
            desc: m.Description || '',
            id: String(m.Id),
            isDefault: m.IsDefault
        })));
        // 自动通知后端选中默认模型
        const defaultModel = models.find(m => m.IsDefault) || models[0];
        if (defaultModel && typeof cortana !== 'undefined' && cortana.OnModelChanged) {
            cortana.OnModelChanged(String(defaultModel.Id));
        }
    } catch (e) {
        console.error('加载模型列表失败:', e);
    }
}

/**
 * 加载指定会话的聊天消息并渲染到页面
 */
function loadSessionMessages(sessionId) {
    try {
        const raw = cortana.GetMessages(sessionId);
        const messages = JSON.parse(raw);

        // 清空当前聊天区域
        dom.messages.innerHTML = '';

        if (messages.length === 0) {
            showWelcome();
            return;
        }

        // 逐条渲染消息（跳过 system/tool 角色和空内容）
        // 注意：这里不能调用 appendMessage，因为 appendMessage 会调用 scrollToBottom
        // 我们要在所有消息加载完后再统一滚动到底部
        messages.forEach(msg => {
            if (msg.Role === 'system' || msg.Role === 'tool') return;
            if (!msg.Content || !msg.Content.trim()) return;

            // 直接渲染消息，不调用 appendMessage 的滚动逻辑
            const time = new Date().toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' });
            const avatarText = msg.Role === 'user' ? '我' : 'AI';
            const html = marked.parse(msg.Content || '');

            const el = document.createElement('div');
            el.className = `message ${msg.Role}`;
            el.innerHTML =
                `<div class="message-avatar">${avatarText}</div>` +
                `<div class="message-content">` +
                `<div class="message-bubble">${html}</div>` +
                `<div class="message-time">${time}</div>` +
                `</div>`;

            dom.messages.appendChild(el);
            highlightCodeBlocks(el);
        });

        // 加载完所有历史消息后，强制滚动到底部
        forceScrollToBottom();
    } catch (e) {
        console.error('加载会话消息失败:', e);
    }
}

/**
 * 事件驱动刷新（由 C# 通过 ExecuteScriptAsync 调用）
 */
function reloadProviders() { loadProviders(); }
function reloadModels() { if (currentProviderId) loadModels(currentProviderId); }
function reloadAgents() { loadAgents(); }

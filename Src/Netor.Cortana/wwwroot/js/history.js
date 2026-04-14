/**
 * history.js - 会话历史管理
 */

/**
 * 设置会话历史列表
 */
function setHistory(items) {
    dom.historyPanel.innerHTML = items.map((item, i) =>
        `<div class="history-item ${i === 0 ? 'active' : ''}" data-id="${item.id}">` +
        `${item.title}` +
        `<span class="history-time">${item.time}</span>` +
        `</div>`
    ).join('');

    dom.historyPanel.querySelectorAll('.history-item').forEach(el => {
        el.addEventListener('click', () => {
            dom.historyPanel.querySelectorAll('.history-item').forEach(s => s.classList.remove('active'));
            el.classList.add('active');
            dom.historyLabel.textContent = el.textContent.replace(el.querySelector('.history-time').textContent, '').trim();
            dom.historySelector.classList.remove('open');

            // 加载该会话的聊天消息
            const sessionId = el.dataset.id;
            state.currentSessionId = sessionId;
            loadSessionMessages(sessionId);

            // 通知 C#
            if (typeof cortana !== 'undefined' && cortana.OnSessionChanged) {
                cortana.OnSessionChanged(sessionId);
            }
        });
    });
}

/**
 * 初始化历史记录下拉
 */
function initializeHistory() {
    dom.historyBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        dom.historySelector.classList.toggle('open');
        // 关闭其他选择器
        document.querySelectorAll('.selector.open').forEach(s => s.classList.remove('open'));
    });

    // 点击空白也关闭历史面板
    document.addEventListener('click', () => {
        dom.historySelector.classList.remove('open');
    });

    dom.historyPanel.addEventListener('click', e => e.stopPropagation());
}

/**
 * 新建会话：创建新会话、清空聊天区域、刷新历史列表
 */
function initializeNewSessionButton() {
    dom.btnNewSession.addEventListener('click', () => {
        state.currentSessionId = null;
        state.streamingEl = null;
        dom.historyLabel.textContent = '新对话';
        showWelcome();
        // 取消历史面板中的选中状态
        dom.historyPanel.querySelectorAll('.history-item').forEach(s => s.classList.remove('active'));
        // 通知 C# 创建新会话
        if (typeof cortana !== 'undefined' && cortana.NewSession) {
            cortana.NewSession();
        }
    });
}

/**
 * 清空页面：仅清除当前页面消息显示，不影响数据库
 */
function initializeClearButton() {
    dom.btnClearMessages.addEventListener('click', () => {
        showWelcome();
    });
}

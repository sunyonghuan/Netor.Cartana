/**
 * input-handler.js - 输入框事件与发送逻辑
 */

/**
 * 输入框自动增高
 */
function autoResize() {
    const ta = dom.textarea;
    ta.style.height = 'auto';
    ta.style.height = Math.min(ta.scrollHeight, 150) + 'px';
}

/**
 * 发送消息
 */
function sendMessage() {
    if (state.isLoading) {
        stopGeneration();
        return;
    }

    const text = dom.textarea.value.trim();
    if (!text && state.attachments.length === 0) return;

    // 先提取附件路径（必须在 clearAttachments 之前）
    const attachmentPaths = state.attachments.map(a => ({
        path: a.path,
        name: a.name,
        type: a.type
    }));

    appendMessage('user', text, attachmentPaths);
    dom.textarea.value = '';
    autoResize();
    clearAttachments();
    setLoading(true);

    // 创建流式 AI 回复占位消息
    beginStreamMessage();

    // 用户发送消息时，强制滚动到最新的AI回复位置
    // 这样用户能看到自己发出的消息和即将到来的AI回复
    forceScrollToBottom();

    // 通过 WebSocket 发送用户消息（附带附件路径）
    wsSend('send', text, attachmentPaths);
}

/**
 * 停止 AI 生成
 */
function stopGeneration() {
    setLoading(false);
    endStreamMessage();
    wsSend('stop', '');
    if (typeof cortana !== 'undefined' && cortana.StopGeneration) {
        cortana.StopGeneration();
    }
}

/**
 * 初始化输入框事件
 */
function initializeInputHandler() {
    // 自动增高
    dom.textarea.addEventListener('input', autoResize);

    // 键盘事件
    dom.textarea.addEventListener('keydown', function (e) {
        // 命令面板打开时处理上下键和回车
        if (state.commandVisible) {
            if (e.key === 'ArrowUp') {
                e.preventDefault();
                state.commandSelectedIndex = Math.max(0, state.commandSelectedIndex - 1);
                renderCommandPanel();
                return;
            }
            if (e.key === 'ArrowDown') {
                e.preventDefault();
                state.commandSelectedIndex = Math.min(state.commandItems.length - 1, state.commandSelectedIndex + 1);
                renderCommandPanel();
                return;
            }
            if (e.key === 'Enter') {
                e.preventDefault();
                selectCommand(state.commandSelectedIndex);
                return;
            }
            if (e.key === 'Escape') {
                hideCommandPanel();
                return;
            }
        }

        // Enter 发送（Shift+Enter 换行）
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });

    // 发送按钮点击
    dom.sendBtn.addEventListener('click', sendMessage);
}

/**
 * 初始化设置按钮
 */
function initializeSettingsButton() {
    dom.btnSettings.addEventListener('click', () => {
        if (typeof cortana !== 'undefined' && cortana.OpenSettings) {
            cortana.OpenSettings();
        }
    });
}

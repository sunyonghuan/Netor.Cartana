/**
 * message-handler.js - 消息渲染和流式处理
 */

/**
 * 对容器内所有代码块执行语法高亮
 */
function highlightCodeBlocks(container) {
    container.querySelectorAll('pre code').forEach(function (block) {
        hljs.highlightElement(block);
    });
}

/**
 * 显示欢迎页
 */
function showWelcome() {
    dom.messages.innerHTML =
        '<div class="welcome">' +
        '<div class="logo">🤖</div>' +
        '<h2>你好，有什么可以帮你的？</h2>' +
        '<p>输入消息开始对话，按 # 调出快捷指令</p>' +
        '</div>';
}

/**
 * 判断 MIME 类型是否为图片
 */
function isImageType(mimeType) {
    return mimeType && mimeType.startsWith('image/');
}

/**
 * 渲染附件 HTML（图片显示预览，其他文件显示文件名）
 */
function renderAttachmentHtml(attachments) {
    if (!attachments || attachments.length === 0) return '';

    const items = attachments.map(a => {
        if (isImageType(a.type)) {
            // 图片：使用本地文件路径显示预览
            const fileUrl = 'file:///' + a.path.replace(/\\/g, '/');
            return `<div class="msg-attachment msg-attachment-image">` +
                `<img src="${fileUrl}" alt="${a.name}" title="${a.name}" loading="lazy" />` +
                `</div>`;
        } else {
            // 非图片：显示文件图标和文件名
            return `<div class="msg-attachment msg-attachment-file">` +
                `<span class="file-icon">📄</span>` +
                `<span class="file-name" title="${a.path}">${a.name}</span>` +
                `</div>`;
        }
    }).join('');

    return `<div class="msg-attachments">${items}</div>`;
}

/**
 * 追加消息到聊天区域
 */
function appendMessage(role, content, attachments) {
    // 如果是欢迎页则移除
    const welcome = dom.messages.querySelector('.welcome');
    if (welcome) welcome.remove();

    const time = new Date().toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' });
    const avatarText = role === 'user' ? '我' : 'AI';
    const html = marked.parse(content || '');
    const attachmentHtml = renderAttachmentHtml(attachments);

    const el = document.createElement('div');
    el.className = `message ${role}`;
    el.innerHTML =
        `<div class="message-avatar">${avatarText}</div>` +
        `<div class="message-content">` +
        `<div class="message-bubble">${attachmentHtml}${html}</div>` +
        `<div class="message-time">${time}</div>` +
        `</div>`;

    dom.messages.appendChild(el);
    highlightCodeBlocks(el);
    // 新消息到来时，如果原本在底部则自动滚动（智能滚动）
    scrollToBottom();
    return el;
}

/**
 * 创建流式 AI 回复占位元素
 */
function beginStreamMessage() {
    // 移除欢迎页
    const welcome = dom.messages.querySelector('.welcome');
    if (welcome) welcome.remove();

    const time = new Date().toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' });
    const el = document.createElement('div');
    el.className = 'message assistant';
    el.innerHTML =
        '<div class="message-avatar">AI</div>' +
        '<div class="message-content">' +
        '<div class="message-bubble"><span class="stream-cursor">▌</span></div>' +
        '<div class="message-time">' + time + '</div>' +
        '</div>';

    dom.messages.appendChild(el);
    state.streamingEl = el;
    // 新的流式消息开始时，如果原本在底部则自动滚动
    scrollToBottom();
}

/**
 * 向流式占位元素追加文本内容
 */
function appendStreamContent(text) {
    if (!state.streamingEl) return;

    const bubble = state.streamingEl.querySelector('.message-bubble');
    if (!bubble) return;

    // 移除光标
    const cursor = bubble.querySelector('.stream-cursor');
    if (cursor) cursor.remove();

    // 获取或创建原始文本存储
    if (!bubble._rawText) bubble._rawText = '';
    bubble._rawText += text;

    // 渲染 Markdown
    bubble.innerHTML = marked.parse(bubble._rawText) + '<span class="stream-cursor">▌</span>';
    highlightCodeBlocks(bubble);
    // 流式内容追加时使用智能滚动（只在底部时才滚动）
    scrollToBottom();
}

/**
 * 结束流式消息，移除光标
 */
function endStreamMessage() {
    if (!state.streamingEl) return;

    const bubble = state.streamingEl.querySelector('.message-bubble');
    if (bubble) {
        const cursor = bubble.querySelector('.stream-cursor');
        if (cursor) cursor.remove();
        // 最终渲染一次确保完整
        if (bubble._rawText) {
            bubble.innerHTML = marked.parse(bubble._rawText);
            highlightCodeBlocks(bubble);
        }
    }

    state.streamingEl = null;
}
